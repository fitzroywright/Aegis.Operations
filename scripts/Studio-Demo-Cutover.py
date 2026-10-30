#!/usr/bin/env python3
"""Offline Studio-only cutover hook. Secrets stay in protected files, never argv/stdout."""
import argparse
import json
import hashlib
import os
from pathlib import Path
import pwd
import re
import shutil
import subprocess
import tempfile

MARKER = '# Managed Studio Demo security cutover\n'


class Cutover:
    def __init__(self, settings, target, service, backup, environment_directory='/etc/aegis', unit_directory='/etc/systemd/system', runner=subprocess.run):
        self.settings, self.target, self.backup = Path(settings), Path(target), Path(backup)
        if not re.fullmatch(r'[A-Za-z0-9_.@-]+', service):
            raise ValueError('Invalid service identifier')
        self.service = service.removesuffix('.service')
        self.environment_directory = Path(environment_directory)
        self.unit_directory = Path(unit_directory)
        self.runner = runner
        self.env = self.environment_directory / 'studio-demo.env'
        self.dropin = self.unit_directory / (self.service + '.service.d') / '90-demo-security.conf'
        self.state = self.backup / 'studio-demo-cutover'

    def run(self, *args):
        result = self.runner(list(args), capture_output=True, text=True, check=False)
        if result.returncode:
            # Provider failures may include credentials. Do not forward subprocess output.
            raise RuntimeError('Protected maintenance subprocess failed')
        return result.stdout.strip()

    def validate(self):
        for name in ('appsettings.Demo.json', 'studio-demo.env', 'plan.json'):
            file = self.settings / name
            if not file.is_file() or file.is_symlink() or file.stat().st_mode & 0o077:
                raise ValueError('Cutover inputs must be owner-only regular files')
        self.plan = json.loads((self.settings / 'plan.json').read_text())
        self.config = json.loads((self.settings / 'appsettings.Demo.json').read_text())
        if not re.fullmatch(r'[a-f0-9]{40}', self.plan['expectedSourceCommit']):
            raise ValueError('A reviewed source commit is required')
        if set(self.plan.get('database', {})) != {'name', 'schema', 'serverAddress', 'port'}:
            raise ValueError('An exact reviewed database identity is required')
        if not self.plan.get('actorId') or not self.plan.get('isolationConfirmed'):
            raise ValueError('Operator isolation and audit actor are required')
        mock = self.config['MockAuthentication']
        if not (mock.get('Enabled') is True and mock.get('IsolationConfirmed') is True and self.config['Demo']['IsolateExternalIntegrations'] is True):
            raise ValueError('The prepared Demo profile must enforce isolation')
        definitions = mock['Identities']
        if len({x['LoginName'].casefold() for x in definitions}) != len(definitions) or len({x['SubjectId'] for x in definitions}) != len(definitions):
            raise ValueError('Mock identity definitions must be unique')
        identities = {(x['LoginName'], x['SubjectId']) for x in definitions}
        rows = self.plan['administrators']
        if len(rows) < 2 or len({x['account'] for x in rows}) != len(rows) or len({x['login'].casefold() for x in rows}) != len(rows):
            raise ValueError('Select at least two distinct reviewed administrators')
        for row in rows:
            if (row['login'], row['subject']) not in identities or not row['account']:
                raise ValueError('A selected immutable identity is not in the server allowlist')
        values = {}
        for line in (self.settings / 'studio-demo.env').read_text().splitlines():
            if not line or line.startswith('#'):
                continue
            key, raw = line.split('=', 1)
            if key in values:
                raise ValueError('Duplicate profile setting')
            values[key] = json.loads(raw)
        expected = {'ASPNETCORE_ENVIRONMENT': 'Demo', 'DOTNET_ENVIRONMENT': 'Demo',
                    'Demo__IsolateExternalIntegrations': 'true', 'MockAuthentication__Enabled': 'true',
                    'MockAuthentication__IsolationConfirmed': 'true'}
        if set(values) != set(expected) | {'MockAuthentication__Password'} or any(values[k] != v for k, v in expected.items()):
            raise ValueError('Unexpected environment profile setting')
        if not isinstance(values['MockAuthentication__Password'], str) or len(values['MockAuthentication__Password']) < 24:
            raise ValueError('A protected strong Demo password is required')
        if self.env.is_symlink() or self.dropin.is_symlink():
            raise ValueError('Managed configuration paths must not be links')
        for file in (self.env, self.dropin):
            if file.exists() and not file.read_text().startswith(MARKER):
                raise ValueError('An unmanaged configuration file conflicts with this cutover')

    def verify_base(self):
        expected = self.plan['expectedBaseEnvironmentSha256']
        if hashlib.sha256((self.environment_directory / 'studio.env').read_bytes()).hexdigest() != expected:
            raise RuntimeError('Protected service configuration changed; prepare a new plan')

    def validate_prior(self):
        self.validate()
        self.verify_base()
        context = self.run('systemctl', 'show', self.service,
                           '--property=User,Group,WorkingDirectory,EnvironmentFiles,Environment,FragmentPath,DropInPaths')
        if hashlib.sha256(context.encode()).hexdigest() != self.plan['expectedServiceContextSha256']:
            raise RuntimeError('The actual service context changed; prepare a new plan')
        if hashlib.sha256((self.target / 'Aegis.Studio.Web.dll').read_bytes()).hexdigest() != self.plan['expectedPreviousPackageSha256']:
            raise RuntimeError('Existing package changed; prepare a new plan')

    def snapshot(self):
        self.validate_prior()
        self.state.mkdir(mode=0o700, parents=True, exist_ok=False)
        rows = []
        for index, file in enumerate((self.env, self.dropin, self.target / "appsettings.Demo.json")):
            row = {'path': str(file), 'existed': file.exists(), 'copy': str(index)}
            if file.is_symlink():
                raise ValueError('Configuration links require explicit operator review')
            if row['existed']:
                row.update(uid=file.stat().st_uid, gid=file.stat().st_gid, mode=file.stat().st_mode & 0o777)
                shutil.copy2(file, self.state / str(index))
            rows.append(row)
        self.atomic(self.state / 'files.json', json.dumps(rows).encode())

    @staticmethod
    def atomic(file, content, uid=None, gid=None):
        file.parent.mkdir(mode=0o755, parents=True, exist_ok=True)
        fd, temporary = tempfile.mkstemp(prefix='.studio-demo-', dir=file.parent)
        try:
            with os.fdopen(fd, 'wb') as output:
                os.fchmod(output.fileno(), 0o600)
                if uid is not None:
                    os.fchown(output.fileno(), uid, gid)
                output.write(content)
                output.flush()
                os.fsync(output.fileno())
            os.replace(temporary, file)
        finally:
            if os.path.exists(temporary):
                os.unlink(temporary)

    def stopped(self):
        state = self.run('systemctl', 'show', self.service, '--property=ActiveState', '--value')
        if state not in ('inactive', 'failed'):
            raise RuntimeError('Studio must be stopped for offline maintenance')

    def install_and_bootstrap(self):
        self.validate()
        self.verify_base()
        self.stopped()
        if not (self.state / 'files.json').exists():
            raise RuntimeError('Configuration rollback snapshot is missing')
        user = self.run('systemctl', 'show', self.service, '--property=User', '--value')
        working = self.run('systemctl', 'show', self.service, '--property=WorkingDirectory', '--value')
        if not user or Path(working).resolve() != self.target.resolve():
            raise RuntimeError('Service maintenance context does not match the deployment')
        owner = pwd.getpwnam(user)
        self.atomic(self.target / 'appsettings.Demo.json', (self.settings / 'appsettings.Demo.json').read_bytes(), owner.pw_uid, owner.pw_gid)
        self.atomic(self.env, MARKER.encode() + (self.settings / 'studio-demo.env').read_bytes())
        dropin = MARKER + '[Service]\nEnvironmentFile=' + str(self.env) + '\n'
        self.atomic(self.dropin, dropin.encode())
        self.run('systemctl', 'daemon-reload')
        # systemd parses its own EnvironmentFile syntax. A protected combined copy avoids
        # shell sourcing and never places bootstrap credentials on command arguments.
        base = self.environment_directory / 'studio.env'
        with tempfile.TemporaryDirectory(prefix='studio-demo-maintenance-') as name:
            temporary = Path(name) / 'maintenance.env'
            self.atomic(temporary, base.read_bytes() + b'\n\n' + self.env.read_bytes())
            def command(arguments):
                self.stopped()
                cmd = ['systemd-run', '--quiet', '--wait', '--pipe', '--collect', '--service-type=exec',
                       '--uid=' + user, '--gid=' + str(owner.pw_gid), '--working-directory=' + str(self.target),
                       '--property=UMask=0077', '--property=EnvironmentFile=' + str(temporary),
                       '/usr/bin/dotnet', str(self.target / 'Aegis.Studio.Web.dll'), 'maintenance'] + arguments
                # systemd expands dollar characters in command arguments, independently of a shell.
                return self.run(*(arg.replace('$', '$$') for arg in cmd))
            version = json.loads(command(['version']))['informationalVersion']
            if version.split('+')[-1] != self.plan['expectedSourceCommit']:
                raise RuntimeError('Published package does not match the reviewed source')
            selections = []
            for row in self.plan['administrators']:
                arguments = ['bootstrap-admin', '--login', row['login'], '--subject', row['subject'], '--account-id', row['account'],
                             '--actor', self.plan['actorId'], '--confirm-isolation', '--expected-database', json.dumps(self.plan['database'])]
                json.loads(command(arguments))
                selections.append(arguments)
            for arguments in selections:
                result = json.loads(command(arguments + ['--apply', '--confirm-offline']))['result']
                if any(result[key] for key in ('CreateAccount', 'ApproveAccount', 'GrantAdministrator')):
                    raise RuntimeError('Administrator bootstrap did not complete')
        print('PASS: isolated Demo configuration and reviewed administrators applied while Studio was stopped.')

    def restore(self):
        if not (self.state / 'files.json').exists():
            return
        for row in json.loads((self.state / 'files.json').read_text()):
            file = Path(row['path'])
            if row['existed']:
                self.atomic(file, (self.state / row['copy']).read_bytes(), row['uid'], row['gid'])
                file.chmod(row['mode'])
            elif file.exists():
                file.unlink()
        self.run('systemctl', 'daemon-reload')
        print('PASS: prior Studio environment/drop-in restored; database and audit records retained.')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('phase', choices=['validate', 'backup', 'apply', 'restore'])
    for name in ['settings', 'target', 'service', 'backup']:
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    try:
        if os.geteuid() != 0:
            raise PermissionError('Authorized root maintenance is required')
        operation = Cutover(args.settings, args.target, args.service, args.backup)
        {'validate': operation.validate_prior, 'backup': operation.snapshot, 'apply': operation.install_and_bootstrap, 'restore': operation.restore}[args.phase]()
        return 0
    except Exception as error:
        print('Studio Demo cutover phase failed (' + type(error).__name__ + '); no credentials printed.', file=__import__('sys').stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
