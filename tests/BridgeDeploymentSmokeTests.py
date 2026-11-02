import os, pathlib, subprocess, tempfile

source = (pathlib.Path(__file__).resolve().parents[1] / 'scripts/Deploy-RequestPortalBridge-Kratos-Server.sh').read_text()
# Account/systemd operations are mocked below; only the temporary copy simulates root.
source = source.replace('[[ $EUID -eq 0 ]]', 'true')
for scenario in ('success', 'readiness_failure', 'first_install_failure', 'http_rejected'):
    with tempfile.TemporaryDirectory() as td:
        root = pathlib.Path(td)
        for folder in ('opt/aegis/backups', 'etc/aegis', 'etc/systemd/system', 'payload', 'bin', 'usr/local/libexec'):
            (root / folder).mkdir(parents=True)
        env = root / 'etc/aegis/ffpmanager-bridge.env'
        env.write_text('DO_NOT_CHANGE=test-only\n'); env.chmod(0o600)
        (root / 'etc/systemd/system/aegis-ffpmanager-bridge.service').touch()
        checker = root / 'usr/local/libexec/aegis-check-requestportal-bridge'
        checker.write_text('import sys\nsys.exit(0)\n'); checker.chmod(0o755)
        (root / 'payload/FfpManager.Bridge.dll').write_text('new')
        (root / 'payload/appsettings.Production.json').write_text('{"Bridge":{}}')
        target = root / 'opt/aegis/ffpmanager-bridge'
        existed = scenario != 'first_install_failure'
        if existed:
            target.mkdir(); (target / 'FfpManager.Bridge.dll').write_text('old')
        active = root / 'active'
        if existed: active.touch()
        mocks = {
            'stat': '#!/usr/bin/env python3\nimport os,stat,sys\nprint(0 if sys.argv[2] == "%u" else format(stat.S_IMODE(os.stat(sys.argv[3]).st_mode), "o"))\n',
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
        script = root / 'deploy.sh'; script.write_text(adapted)
        url = 'http://bridge.test' if scenario == 'http_rejected' else 'https://bridge.test'
        result = subprocess.run(['bash', str(script), '--source', str(root / 'payload'), '--probe-url', url], env={**os.environ, 'PATH':str(root/'bin')+':'+os.environ['PATH'], 'TEST_ROOT':td, 'TEST_SCENARIO':scenario}, capture_output=True, text=True)
        if scenario == 'success':
            assert result.returncode == 0, result.stderr
            assert (target / 'FfpManager.Bridge.dll').read_text() == 'new'
            assert active.exists()
        elif scenario == 'first_install_failure':
            assert result.returncode != 0
            assert not target.exists() and not active.exists(), result.stdout + result.stderr
        else:
            assert result.returncode != 0
            assert (target / 'FfpManager.Bridge.dll').read_text() == 'old'
            assert active.exists()
        assert env.read_text() == 'DO_NOT_CHANGE=test-only\n'
        print('PASS', scenario)
