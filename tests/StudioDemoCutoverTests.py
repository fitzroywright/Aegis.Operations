"""Test the production cutover hook using disposable files and a recorded service manager."""
import getpass
import hashlib
import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest

ROOT = pathlib.Path(__file__).parents[1]
spec = importlib.util.spec_from_file_location('studio_demo_cutover', ROOT / 'scripts/Studio-Demo-Cutover.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class Manager:
    def __init__(self, fixture):
        self.fixture = fixture
        self.state = 'inactive'
        self.calls = []
        self.version = 'a' * 40
        self.applied = []
        self.fail_second = False

    def __call__(self, args, **kwargs):
        self.calls.append(args)
        if args[0] == 'systemctl':
            value = 'recorded service context' if any(x.startswith('--property=User,Group,') for x in args) else self.state if '--property=ActiveState' in args else getpass.getuser() if '--property=User' in args else str(self.fixture.target) if '--property=WorkingDirectory' in args else ''
            return subprocess.CompletedProcess(args, 0, value, '')
        assert args[0] == 'systemd-run'
        environment = pathlib.Path(next(x.split('=', 2)[2] for x in args if x.startswith('--property=EnvironmentFile=')))
        assert environment.stat().st_mode & 0o777 == 0o600
        assert b'EXISTING_CREDENTIAL="retained-fixture"' in environment.read_bytes()
        assert 'retained-fixture' not in str(args)
        if args[-1] == 'version':
            return subprocess.CompletedProcess(args, 0, json.dumps({'informationalVersion': '1.0.0+' + self.version}), '')
        login = args[args.index('--login') + 1]
        if '--apply' in args:
            if self.fail_second and self.applied:
                return subprocess.CompletedProcess(args, 1, '', 'Protected provider error must not be forwarded')
            self.applied.append(login)
        result = {'CreateAccount': '--apply' not in args, 'ApproveAccount': '--apply' not in args, 'GrantAdministrator': '--apply' not in args}
        return subprocess.CompletedProcess(args, 0, json.dumps({'result': result}), '')


class Fixture:
    def __init__(self, root):
        self.root = pathlib.Path(root)
        self.target = self.root / 'studio'
        self.target.mkdir()
        (self.target / 'Aegis.Studio.Web.dll').write_bytes(b'previous package')
        self.environment = self.root / 'etc'
        self.environment.mkdir()
        (self.environment / 'studio.env').write_text('EXISTING_CREDENTIAL="retained-fixture"\n')
        self.settings = self.root / 'settings'
        self.settings.mkdir()
        self.plan = {'expectedSourceCommit': 'a' * 40, 'actorId': 'offline:test', 'isolationConfirmed': True,
                     'expectedServiceContextSha256': hashlib.sha256(b'recorded service context').hexdigest(),
                     'expectedBaseEnvironmentSha256': hashlib.sha256((self.environment / 'studio.env').read_bytes()).hexdigest(),
                     'expectedPreviousPackageSha256': hashlib.sha256((self.target / 'Aegis.Studio.Web.dll').read_bytes()).hexdigest(),
                     'database': {'name': 'reviewed-demo', 'schema': 'public', 'serverAddress': '127.0.0.1', 'port': 5432},
                     'administrators': [{'login': 'chosen.primary', 'subject': 'primary-subject', 'account': 'preserved-primary'},
                                        {'login': 'chosen.secondary', 'subject': 'secondary-subject', 'account': 'preserved-secondary'}]}
        config = {'Demo': {'IsolateExternalIntegrations': True}, 'MockAuthentication': {'Enabled': True, 'IsolationConfirmed': True,
                  'Identities': [{'LoginName': r['login'], 'SubjectId': r['subject']} for r in self.plan['administrators']]}}
        profile = {'ASPNETCORE_ENVIRONMENT': 'Demo', 'DOTNET_ENVIRONMENT': 'Demo', 'Demo__IsolateExternalIntegrations': 'true',
                   'MockAuthentication__Enabled': 'true', 'MockAuthentication__IsolationConfirmed': 'true', 'MockAuthentication__Password': 'synthetic-test-password-with-sufficient-length'}
        self.write('plan.json', json.dumps(self.plan))
        self.write('appsettings.Demo.json', json.dumps(config))
        self.write('studio-demo.env', ''.join(k + '=' + json.dumps(v) + '\n' for k, v in profile.items()))
        self.manager = Manager(self)
        self.operation = module.Cutover(self.settings, self.target, 'studio-demo', self.root / 'backup', self.environment, self.root / 'units', self.manager)

    def write(self, name, text):
        path = self.settings / name
        path.write_text(text)
        path.chmod(0o600)


class StudioDemoCutoverTests(unittest.TestCase):
    def test_bootstrap_runs_all_plans_before_apply_in_the_existing_user_context(self):
        with tempfile.TemporaryDirectory() as directory:
            f = Fixture(directory)
            original = (f.environment / 'studio.env').read_bytes()
            f.operation.snapshot()
            f.operation.install_and_bootstrap()
            self.assertEqual(['chosen.primary', 'chosen.secondary'], f.manager.applied)
            commands = [a for a in f.manager.calls if a[0] == 'systemd-run']
            self.assertEqual([False, False, False, True, True], ['--apply' in a for a in commands])
            self.assertTrue(all('--uid=' + getpass.getuser() in a for a in commands))
            self.assertTrue(all('--expected-database' in a for a in commands[1:]))
            self.assertEqual(original, (f.environment / 'studio.env').read_bytes())
            self.assertIn('EnvironmentFile=', f.operation.dropin.read_text())

    def test_active_service_prevents_any_profile_installation(self):
        with tempfile.TemporaryDirectory() as directory:
            f = Fixture(directory)
            f.operation.snapshot()
            f.manager.state = 'active'
            with self.assertRaises(RuntimeError):
                f.operation.install_and_bootstrap()
            self.assertFalse(f.operation.env.exists())
            self.assertFalse(f.manager.applied)

    def test_package_or_protected_environment_changes_require_a_new_plan(self):
        with tempfile.TemporaryDirectory() as directory:
            f = Fixture(directory)
            (f.target / 'Aegis.Studio.Web.dll').write_bytes(b'another release')
            with self.assertRaises(RuntimeError):
                f.operation.validate_prior()
            (f.target / 'Aegis.Studio.Web.dll').write_bytes(b'previous package')
            (f.environment / 'studio.env').write_text('changed protected configuration')
            with self.assertRaises(RuntimeError):
                f.operation.validate_prior()
            self.assertFalse(f.manager.applied)

    def test_wrong_published_commit_never_applies_an_account(self):
        with tempfile.TemporaryDirectory() as directory:
            f = Fixture(directory)
            f.operation.snapshot()
            f.manager.version = 'b' * 40
            with self.assertRaises(RuntimeError):
                f.operation.install_and_bootstrap()
            self.assertFalse(f.manager.applied)
            f.operation.restore()
            self.assertFalse(f.operation.env.exists())
            self.assertFalse(f.operation.dropin.exists())

    def test_failure_restores_configuration_while_preserving_committed_account_audit(self):
        with tempfile.TemporaryDirectory() as directory:
            f = Fixture(directory)
            old = b'{"retained":"prior configuration"}'
            (f.target / 'appsettings.Demo.json').write_bytes(old)
            f.operation.snapshot()
            f.manager.fail_second = True
            with self.assertRaises(RuntimeError):
                f.operation.install_and_bootstrap()
            f.operation.restore()
            self.assertEqual(['chosen.primary'], f.manager.applied)
            self.assertEqual(old, (f.target / 'appsettings.Demo.json').read_bytes())
            self.assertFalse(f.operation.env.exists())
            self.assertFalse(f.operation.dropin.exists())

    def test_unmanaged_files_and_world_readable_inputs_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            f = Fixture(directory)
            (f.settings / 'studio-demo.env').chmod(0o644)
            with self.assertRaises(ValueError):
                f.operation.validate()
            (f.settings / 'studio-demo.env').chmod(0o600)
            f.operation.env.write_text('operator-owned settings')
            with self.assertRaises(ValueError):
                f.operation.validate()

    def test_live_files_and_sqlite_wal_survive_promotion_and_failed_cutover_rollback(self):
        source = (ROOT / 'scripts/Deploy-Kratos-Server.sh').read_text()
        def function(name):
            return name + '() {' + source.split(name + '() {', 1)[1].split('\n}', 1)[0] + '\n}\n'
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            target, backup = root / 'studio', root / 'backup'
            target.mkdir()
            (backup / 'studio').mkdir(parents=True)
            (target / 'Storage').mkdir()
            (target / 'Storage/original.raw').write_bytes(b'preserve original')
            (target / 'data').mkdir()
            (target / 'data/live.sqlite').write_bytes(b'latest database')
            (target / 'data/live.sqlite-wal').write_bytes(b'latest WAL')
            (target / 'version').write_text('new package')
            (target / 'appsettings.Demo.json').write_text('retained protected profile')
            (backup / 'studio/version').write_text('old package')
            audit = root / 'audit'
            audit.write_text('committed security audit')
            harness = '''set -euo pipefail
studio=0
TARGET=("$1")
BACKUP_DIR="$2"
OWNER=unused
SELECTED=(studio)
is_selected() { [[ "$1" == studio ]]; }
chown() { :; }
stop_selected() { echo stop:studio; }
start_selected() { echo start:studio; }
studio_demo_phase() { echo config:restore; }
'''
            harness += function('hold_studio_runtime') + function('restore_studio_runtime') + function('rollback')
            harness += '''hold_studio_runtime
rm -rf "$1"
mkdir -p "$1"
echo promoted > "$1/version"
restore_studio_runtime
echo latest-after-promotion >> "$1/data/live.sqlite-wal"
rollback
'''
            result = subprocess.run(['bash', '-c', harness, 'test', str(target), str(backup)], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual(b'preserve original', (target / 'Storage/original.raw').read_bytes())
            self.assertIn(b'latest-after-promotion', (target / 'data/live.sqlite-wal').read_bytes())
            self.assertEqual('old package', (target / 'version').read_text())
            self.assertEqual('retained protected profile', (target / 'appsettings.Demo.json').read_text())
            self.assertEqual('committed security audit', audit.read_text())
            self.assertTrue(result.stdout.rstrip().endswith('start:studio'))


if __name__ == '__main__':
    unittest.main()
