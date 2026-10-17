"""Isolated first-install/configuration tests; no host services or accounts changed."""
import importlib.util
import os
import pathlib
import subprocess
import tempfile

repo = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('bridge_check', repo / 'deploy/ffpmanager-bridge/Check-Configuration.py')
checker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checker)
template = repo / 'deploy/ffpmanager-bridge/environment.example'
assert checker.check(checker.read_environment(template)), 'Template must not pass commissioning'
print('PASS incomplete template rejected')
with tempfile.TemporaryDirectory() as td:
    root = pathlib.Path(td)
    values = checker.read_environment(template)
    values = {k:v for k,v in values.items() if 'CHANGE_ME' not in k}
    values.update({
        'AllowedHosts':'bridge.test', 'Bridge__RequestPortalBaseUrl':'https://portal.test',
        'Bridge__SupportingDocumentRoot':td, 'Bridge__InitialApplicationStatus':'RECEIVED',
        'Bridge__StatusMap__New':'RECEIVED', 'Bridge__DocumentTypeMap__Photo':'Photo',
        'CommonSecrets__Providers__PrimarySecrets__Settings__Address':'https://bao.test',
        'CommonSecrets__Providers__PrimarySecrets__Settings__RoleId':'test-role',
        'CommonSecrets__Providers__PrimarySecrets__Settings__BasePath':'aegis',
    })
    for key in ('Kestrel__Certificates__Default__Path','Kestrel__Certificates__Default__KeyPath','CommonSecrets__Providers__PrimarySecrets__Settings__SecretIdFile'):
        p=root/key; p.write_text('fixture'); p.chmod(0o640); values[key]=str(p)
    assert not checker.check(values), checker.check(values)
    print('PASS complete configuration shape accepted (not live credentials or TLS)')
    for key, bad in [('ASPNETCORE_URLS','http://0.0.0.0:5443'), ('Bridge__RequestPortalBaseUrl','http://portal.test'), ('CommonSecrets__Providers__PrimarySecrets__Settings__DeleteSecretIdFileAfterRead','true')]:
        assert checker.check({**values,key:bad})
    print('PASS insecure listeners/callback and destructive restart bootstrap rejected')

with tempfile.TemporaryDirectory() as td:
    root=pathlib.Path(td); bindir=root/'bin'; bindir.mkdir()
    # Keep the installer next to real repository assets; rewrite host paths only
    # in an isolated copy, and stub all account/systemd mutations.
    code=(repo/'scripts/Initialize-RequestPortalBridge-Kratos.sh').read_text()
    code=code.replace('repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}\")/.." && pwd)', 'repo="'+str(repo)+'"')
    for prefix in ('/etc/aegis','/etc/systemd/system','/opt/aegis','/var/lib/aegis-ffpmanager-bridge','/usr/local/libexec','/usr/local/sbin'):
        code=code.replace(prefix,str(root)+prefix)
    (root/'etc/aegis').mkdir(parents=True); (root/'etc/systemd/system').mkdir(parents=True)
    (root/'usr/local/sbin').mkdir(parents=True)
    for name in ('systemctl','useradd','groupadd'):
        p=bindir/name; p.write_text('#!/bin/bash\nexit 0\n'); p.chmod(0o755)
    p=bindir/'getent'; p.write_text('#!/bin/bash\nexit 0\n'); p.chmod(0o755)
    # Preserve install's normal modes/copies while replacing ownership for tests.
    p=bindir/'install'; p.write_text('''#!/bin/bash
args=()
while (($#)); do
 case "$1" in -o|-g) shift 2 ;; *) args+=("$1"); shift ;; esac
done
exec /usr/bin/install "${args[@]}"
'''); p.chmod(0o755)
    script=root/'initialize.sh'; script.write_text(code)
    env={**os.environ,'PATH':str(bindir)+':'+os.environ['PATH']}
    result=subprocess.run(['bash',str(script)],env=env,capture_output=True,text=True)
    assert result.returncode == 0, result.stderr
    config=root/'etc/aegis/ffpmanager-bridge.env'
    unit=root/'etc/systemd/system/aegis-ffpmanager-bridge.service'
    assert config.stat().st_mode & 0o777 == 0o600
    config.write_text('CUSTOM_CONFIG=preserved\n'); unit.write_text('custom unit\n')
    result=subprocess.run(['bash',str(script)],env=env,capture_output=True,text=True)
    assert result.returncode == 0, result.stderr
    assert config.read_text()=='CUSTOM_CONFIG=preserved\n' and unit.read_text()=='custom unit\n'
    print('PASS bootstrap repeat preserves customized environment and unit')
