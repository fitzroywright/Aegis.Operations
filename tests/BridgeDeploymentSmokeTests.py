import os, pathlib, subprocess, tempfile

source = (pathlib.Path(__file__).resolve().parents[1] / 'scripts/Deploy-RequestPortalBridge-Kratos-Server.sh').read_text()
for scenario in ('success', 'readiness_failure', 'first_install_failure', 'http_rejected', 'preflight_failure', 'install_only', 'install_only_active'):
    with tempfile.TemporaryDirectory() as td:
        root = pathlib.Path(td)
        for folder in ('opt/aegis/backups', 'etc/aegis', 'etc/systemd/system', 'payload', 'bin', 'usr/local/libexec'):
            (root / folder).mkdir(parents=True)
        env = root / 'etc/aegis/ffpmanager-bridge.env'
        env.write_text('DO_NOT_CHANGE=test-only\n'); env.chmod(0o600)
        (root / 'etc/systemd/system/aegis-ffpmanager-bridge.service').touch()
        checker = root / 'usr/local/libexec/aegis-check-requestportal-bridge'
        checker.write_text('import os, sys\nassert "--read-only" in sys.argv\nsys.exit(2 if os.environ.get("TEST_SCENARIO") in ("preflight_failure", "install_only") else 0)\n'); checker.chmod(0o755)
        (root / 'payload/FfpManager.Bridge.dll').write_text('new')
        (root / 'payload/appsettings.Production.json').write_text('{"Bridge":{}}')
        target = root / 'opt/aegis/ffpmanager-bridge'
        existed = scenario not in ('first_install_failure', 'install_only')
        if existed:
            target.mkdir(); (target / 'FfpManager.Bridge.dll').write_text('old')
        active = root / 'active'
        if existed: active.touch()
        mocks = {
            'timeout': '#!/bin/bash\nshift 2\nexec "$@"\n',
            'stat': '''#!/usr/bin/env python3
import pathlib, sys
print('0' if sys.argv[2] == '%u' else format(pathlib.Path(sys.argv[3]).stat().st_mode & 0o777, 'o'))
''',
            'getent': '#!/bin/bash\nexit 0\n',
            'chown': '#!/bin/bash\nexit 0\n',
            'sleep': '#!/bin/bash\nexit 0\n',
            'systemctl': '''#!/bin/bash
echo "$*" >> "$TEST_ROOT/service.log"
case "$1" in
  is-active) test -f "$TEST_ROOT/active" ;;
  stop) rm -f "$TEST_ROOT/active" ;;
  start) touch "$TEST_ROOT/active" ;;
esac
''',
            'curl': '''#!/bin/bash
url=''; output=''
while (($#)); do
 case "$1" in
  -o) output=$2; shift 2 ;;
  https://*) url=$1; shift ;;
  *) shift ;;
 esac
done
if [[ "$url" == */ready && "$TEST_SCENARIO" != success ]]; then exit 22; fi
status=ok
[[ "$url" == */ready ]] && status=ready
printf '{"gateway":"Sql","status":"%s"}' "$status" > "$output"
'''
        }
        for name, content in mocks.items():
            p = root / 'bin' / name; p.write_text(content); p.chmod(0o755)
        adapted = source.replace('/opt/aegis', str(root / 'opt/aegis')).replace('/var/lib/aegis-ffpmanager-bridge', str(root / 'state')).replace('/etc/aegis/', str(root / 'etc/aegis') + '/').replace('/etc/systemd/system/', str(root / 'etc/systemd/system') + '/').replace('/usr/local/libexec/', str(root / 'usr/local/libexec') + '/')
        adapted = adapted.replace('[[ $EUID -eq 0 ]]', 'true')  # Isolated fixture; production still requires root.
        # This fixture cannot impersonate the production service UID or chown.
        # Live install-only verification separately exercises these checks.
        adapted = adapted.replace('python3 - "$target" "$owner" "$state" <<\'PY\'', 'true <<\'PY\'')
        script = root / 'deploy.sh'; script.write_text(adapted)
        url = 'http://bridge.test' if scenario == 'http_rejected' else 'https://bridge.test'
        arguments = ['--install-only'] if scenario.startswith('install_only') else ['--probe-url', url]
        result = subprocess.run(['bash', str(script), '--source', str(root / 'payload')] + arguments, env={**os.environ, 'PATH':str(root/'bin')+':'+os.environ['PATH'], 'TEST_ROOT':td, 'TEST_SCENARIO':scenario}, capture_output=True, text=True)
        if scenario == 'success':
            assert result.returncode == 0, result.stderr
            assert (target / 'FfpManager.Bridge.dll').read_text() == 'new'
            assert active.exists()
        elif scenario == 'first_install_failure':
            assert result.returncode != 0
            assert not target.exists() and not active.exists(), result.stdout + result.stderr
        elif scenario == 'install_only':
            assert result.returncode == 0, result.stderr
            assert (target / 'FfpManager.Bridge.dll').read_text() == 'new'
            assert not active.exists()
            assert 'start' not in (root / 'service.log').read_text()
        else:
            assert result.returncode != 0
            assert (target / 'FfpManager.Bridge.dll').read_text() == 'old'
            assert active.exists()
        if scenario == 'preflight_failure':
            assert not any((root / 'opt/aegis/backups').iterdir())
            assert not (root / 'service.log').exists() or 'stop' not in (root / 'service.log').read_text()
        assert env.read_text() == 'DO_NOT_CHANGE=test-only\n'
        print('PASS', scenario)
