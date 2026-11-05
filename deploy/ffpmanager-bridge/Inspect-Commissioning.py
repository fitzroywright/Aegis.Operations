#!/usr/bin/python3
"""Fixed-scope commissioning inventory. Never prints credential or journal values.

Reads host configuration, then drops irreversibly to the bridge identity for
network/access checks. The only storage write is a unique, removed NAS probe.
Does not log into OpenBao, open SQL connections, or start any service.
"""
import argparse
import hashlib
import json
import os
import pathlib
import pwd
import re
import runpy
import shlex
import ssl
import subprocess
import tempfile
import urllib.error
import urllib.request
from urllib.parse import urlsplit

OWNER = 'aegis-ffpmanager-bridge'
ROOT = pathlib.Path('/opt/aegis/ffpmanager-bridge')
ENV = pathlib.Path('/etc/aegis/ffpmanager-bridge.env')
result = {}

def command(args):
    return subprocess.run(args, capture_output=True, text=True, timeout=20)

def metadata(path):
    path = pathlib.Path(path)
    try:
        st = path.stat()
        return dict(path=str(path), exists=True, uid=st.st_uid, gid=st.st_gid,
                    mode=oct(st.st_mode & 0o777), size=st.st_size)
    except FileNotFoundError:
        return dict(path=str(path), exists=False)
    except PermissionError:
        return dict(path=str(path), accessible=False)

def configured(value):
    return bool(value and value.strip() and 'CHANGE_ME' not in value)

def origin(value):
    parsed = urlsplit(value)
    if parsed.scheme == 'https' and parsed.hostname and not parsed.username and not parsed.password:
        return 'https://' + parsed.netloc
    return 'invalid-or-unconfigured'

def environment_shape(path):
    # Inventory only: allow unquoted spaces in existing systemd values. Never
    # evaluate substitutions, load another app's provider, or emit credentials.
    values = {}
    for line in path.read_text().splitlines():
        key, separator, raw = line.strip().partition('=')
        if not separator or not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*', key): continue
        try:
            parts = shlex.split(raw, comments=False, posix=True)
            values[key] = parts[0] if len(parts) == 1 else raw
        except ValueError:
            continue
    return values

if os.geteuid() != 0:
    raise SystemExit('Run through the root-owned bridge deployer.')
parser = argparse.ArgumentParser()
parser.add_argument('--prepare-tls-request', action='store_true')
parser.add_argument('--sql-read-only', action='store_true')
args = parser.parse_args()
if args.prepare_tls_request:
    active = command(['systemctl','is-active','--quiet','aegis-ffpmanager-bridge.service'])
    if active.returncode == 0: raise SystemExit('TLS preparation requires the bridge stopped.')
    tls = pathlib.Path('/etc/aegis-ffpmanager-bridge/tls')
    key, csr = tls / 'privatekey.pem', tls / 'kratos.fftpj.local.csr'
    account = pwd.getpwnam(OWNER)
    os.umask(0o077)
    if key.is_symlink() or csr.is_symlink(): raise SystemExit('Refusing symlink TLS output.')
    for destination, operation in (
        (key, lambda path: ['openssl','genpkey','-algorithm','RSA','-pkeyopt','rsa_keygen_bits:3072','-out',path]),
        (csr, lambda path: ['openssl','req','-new','-key',str(key),'-out',path,'-subj','/CN=kratos.fftpj.local','-addext','subjectAltName=DNS:kratos.fftpj.local']),
    ):
        if destination.exists(): continue
        descriptor, temporary = tempfile.mkstemp(prefix='.bridge-tls-',dir=tls)
        os.close(descriptor)
        try:
            operation_result = command(operation(temporary))
            if operation_result.returncode: raise SystemExit('Unable to prepare TLS request; no key contents emitted.')
            os.chown(temporary,0,account.pw_gid)
            os.chmod(temporary,0o640)
            os.link(temporary,destination)  # Exclusive creation; never replace existing TLS material.
        finally:
            os.unlink(temporary)
    request_check = command(['openssl','req','-in',str(csr),'-noout','-verify'])
    public_key = command(['openssl','pkey','-in',str(key),'-pubout'])
    request_key = command(['openssl','req','-in',str(csr),'-noout','-pubkey'])
    if request_check.returncode or public_key.returncode or request_key.returncode or public_key.stdout != request_key.stdout:
        raise SystemExit('TLS CSR signature/key verification failed.')
    result['tls_request'] = dict(csr_path=str(csr),key_path=str(key),
        csr_pem=csr.read_text(),csr_sha256=hashlib.sha256(csr.read_bytes()).hexdigest(),
        csr_signature_and_key_match=True,issued_certificate=False)
checker = runpy.run_path('/usr/local/libexec/aegis-check-requestportal-bridge')
values = environment_shape(ENV)
overlay = pathlib.Path('/etc/aegis/ffpmanager-bridge-production.env')
if overlay.exists(): values.update(environment_shape(overlay))
service_environment = command(['systemctl','show','aegis-ffpmanager-bridge.service','-p','Environment','--value'])
for assignment in shlex.split(service_environment.stdout):
    key, sep, value = assignment.partition('=')
    if sep and key in ('KRB5CCNAME','KRB5_CLIENT_KTNAME'): values[key] = value
result['service_user'] = command(['systemctl','show','aegis-ffpmanager-bridge.service','-p','User','--value']).stdout.strip()
if result['service_user'] != OWNER: raise SystemExit('Unexpected bridge service user')
safe_keys = ('ASPNETCORE_URLS', 'AllowedHosts', 'Bridge__Gateway', 'Bridge__SqlTarget',
             'Bridge__RequestPortalBaseUrl', 'Bridge__SupportingDocumentRoot',
             'Bridge__SupportingDocumentManagerRoot', 'Bridge__InitialApplicationStatus',
             'Bridge__SqlConnectionStringSecretName', 'Bridge__SharedSecretSecretName',
             'CommonSecrets__WorkloadName', 'CommonSecrets__EnvironmentName')
result['bridge_config'] = {k: values.get(k, '') for k in safe_keys}
result['bridge_config']['Bridge__RequestPortalBaseUrl'] = origin(values.get('Bridge__RequestPortalBaseUrl', ''))
result['mapping_config'] = {k: v for k, v in values.items() if k.startswith(('Bridge__StatusMap__', 'Bridge__DocumentTypeMap__'))}
result['environment_file'] = metadata(ENV)
result['application'] = metadata(ROOT)
journal = command(['journalctl','-u','aegis-requestportal.service','-u','aegis-ffpmanager-bridge.service','-n','50','-o','json','--no-pager'])
entries = [json.loads(line) for line in journal.stdout.splitlines() if line.startswith('{')]
result['journal_summary'] = dict(read_exit=journal.returncode, entries_up_to_50=len(entries), errors=sum(int(entry.get('PRIORITY',6)) <= 3 for entry in entries))
result['dll'] = {**metadata(ROOT / 'FfpManager.Bridge.dll'), 'sha256': hashlib.sha256((ROOT / 'FfpManager.Bridge.dll').read_bytes()).hexdigest()}
result['deployment'] = json.loads((ROOT / 'DEPLOYMENT.json').read_text())
result['checksums_exit'] = command(['sha256sum', '--check', '--status', 'SHA256SUMS']).returncode if pathlib.Path.cwd() == ROOT else subprocess.run(['sha256sum', '--check', '--status', 'SHA256SUMS'], cwd=ROOT, capture_output=True).returncode

# Only configuration shape from other applications, never their credentials.
providers = []
for path in sorted(pathlib.Path('/etc/aegis').glob('*.env')):
    try:
        other = environment_shape(path)
    except (OSError, ValueError):
        providers.append(dict(file=str(path), parseable=False)); continue
    for key, value in other.items():
        if key.endswith('__Address') and ('CommonSecrets' in key or 'OpenBao' in key):
            prefix = key[:-len('Address')]
            providers.append(dict(file=str(path), address=origin(value),
                role_configured=configured(other.get(prefix+'RoleId')),
                base_path_configured=configured(other.get(prefix+'BasePath')),
                secret_file=metadata(other[prefix+'SecretIdFile']) if other.get(prefix+'SecretIdFile') else None))
result['existing_provider_shapes'] = providers
result['operator_token_files'] = [metadata(p) for p in ('/root/.vault-token','/root/.bao-token','/home/fwright/.vault-token','/home/fwright/.bao-token')]
result['credential_file_inventory'] = [str(p) for base in ('/etc/aegis','/etc/ssl/private') for p in pathlib.Path(base).rglob('*') if p.is_file()]
proxy = pathlib.Path('/etc/nginx/sites-enabled/openbao.ffpja.org')
if proxy.exists():
    proxy_text = proxy.read_text()
    result['openbao_proxy_upstreams'] = re.findall(r'\bproxy_pass\s+(https?://[A-Za-z0-9_.:-]+)', proxy_text)
    certificate_paths_from_proxy = re.findall(r'\bssl_certificate\s+([^;\s]+)', proxy_text)
else:
    certificate_paths_from_proxy = []

# Public certificate inventory; never emit a PEM body or private key.
certificate_paths = set(pathlib.Path('/usr/local/share/ca-certificates').glob('*.crt'))
certificate_paths.update(pathlib.Path(p) for p in certificate_paths_from_proxy)
for directory in [pathlib.Path('/etc/aegis'), *pathlib.Path('/etc').glob('aegis-*'), pathlib.Path('/etc/letsencrypt/live')]:
    if directory.is_dir():
        for path in directory.rglob('*'):
            if path.is_file() and path.suffix.lower() in ('.crt', '.pem', '.cer') and not any(x in path.name.lower() for x in ('key', 'secret')):
                certificate_paths.add(path)
certificates = []
for path in sorted(certificate_paths):
    cert = command(['openssl','x509','-in',str(path),'-noout','-subject','-issuer','-dates','-ext','subjectAltName'])
    if cert.returncode == 0:
        hostcheck = command(['openssl','x509','-in',str(path),'-noout','-checkhost','kratos.fftpj.local'])
        matches = hostcheck.returncode == 0 and 'does match certificate' in hostcheck.stdout
        certificates.append(dict(path=str(path), metadata=cert.stdout.strip(), covers_bridge_host=matches))
result['certificates'] = certificates
result['bridge_secret_files'] = [metadata(p) for p in pathlib.Path('/etc/aegis-ffpmanager-bridge').rglob('*') if p.is_file()]
kt = command(['klist','-k','/etc/krb5.keytab'])
result['host_keytab_principals'] = kt.stdout.strip() if kt.returncode == 0 else 'unavailable'
result['bridge_keytabs'] = [metadata(p) for p in pathlib.Path('/etc/aegis-ffpmanager-bridge').rglob('*.keytab')]

# Portal configuration is summarized without credential values or recipients.
portal_root = pathlib.Path('/opt/aegis/requestportal')
portal_values = environment_shape(pathlib.Path('/etc/aegis/requestportal.env'))
portal_configs = []
for filename in ('appsettings.json', 'appsettings.Production.json'):
    path = portal_root / filename
    if path.exists(): portal_configs.append(json.loads(path.read_text()))
def flatten(values, prefix=''):
    result = {}
    for key,value in values.items():
        name = prefix + key
        if isinstance(value,dict): result.update(flatten(value,name+'__'))
        else: result[name] = str(value).lower() if isinstance(value,bool) else str(value)
    return result
portal_effective = {}
for config in portal_configs: portal_effective.update(flatten(config))
portal_effective.update(portal_values)
portal_keys = ('FfpManagerIntegration__Enabled','FfpManagerIntegration__BaseUrl',
    'FfpManagerIntegration__PollIntervalSeconds','Notifications__Enabled','Notifications__WhatsAppEnabled',
    'Messaging__Outbox__Enabled','Messaging__MicrosoftGraph__Enabled')
result['portal_config'] = {k:portal_effective.get(k) for k in portal_keys}
result['portal_messaging_configured'] = {k:configured(portal_effective.get(k)) for k in
    ('Notifications__QuickIntakeClientRelationsEmail','Messaging__MicrosoftGraph__ClientId',
     'Messaging__MicrosoftGraph__TenantId','Notifications__WhatsAppPhoneNumberId')}
result['portal_dll_sha256'] = hashlib.sha256((portal_root/'RequestPortal.Web.dll').read_bytes()).hexdigest()
manifest = portal_root/'DEPLOYMENT.json'
result['portal_deployment'] = json.loads(manifest.read_text()) if manifest.exists() else 'No manifest'
if args.sql_read_only:
    probe = '/usr/local/libexec/aegis-bridge-commissioning-probe/BridgeCommissioningProbe.dll'
    def probe_as(user, environment, root, mode):
        account = pwd.getpwnam(user)
        def drop():
            os.initgroups(account.pw_name,account.pw_gid)
            os.setgid(account.pw_gid)
            os.setuid(account.pw_uid)
        response = subprocess.run(['/usr/bin/dotnet',probe,mode], cwd=root,
            env={**os.environ, **environment, 'DOTNET_ENVIRONMENT':'Production','ASPNETCORE_ENVIRONMENT':'Production'},
            preexec_fn=drop, capture_output=True,text=True,timeout=90)
        try: return dict(exit=response.returncode, data=json.loads(response.stdout))
        except ValueError: return dict(exit=response.returncode, parseable=False)
    result['portal_database_preflight'] = probe_as('aegis',portal_values,portal_root,'--portal')
    result['ffpjm_database_preflight'] = probe_as(OWNER,values,ROOT,'--ffpjm')

account = pwd.getpwnam(OWNER)
os.initgroups(account.pw_name, account.pw_gid)
os.setgid(account.pw_gid)
os.setuid(account.pw_uid)
result['effective_uid'] = os.geteuid()
os.environ.update({k:v for k,v in values.items() if k in ('KRB5CCNAME','KRB5_CLIENT_KTNAME')})
result['bridge_has_kerberos_ticket'] = command(['klist','-s']).returncode == 0
result['kerberos_renewal'] = command(['systemctl','show','aegis-ffpbridge-kerberos.service','-p','Result','-p','ExecMainStartTimestamp']).stdout.strip()
result['runtime'] = command(['/usr/bin/dotnet','--list-runtimes']).stdout.strip()
result['bridge_file_access'] = {}
provider = values.get('CommonSecrets__ProviderOrder__0', '')
prefix = 'CommonSecrets__Providers__'+provider+'__Settings__'
for key in ('Kestrel__Certificates__Default__Path','Kestrel__Certificates__Default__KeyPath',prefix+'SecretIdFile'):
    path = values.get(key, '')
    result['bridge_file_access'][key] = dict(configured=configured(path), readable=bool(path and os.access(path,os.R_OK)))
result['openbao_configured'] = {suffix: configured(values.get(prefix+suffix)) for suffix in ('RoleId','BasePath','SecretIdFile')}
address = origin(values.get(prefix+'Address', ''))
result['openbao_origin'] = address
if address != 'invalid-or-unconfigured':
    try:
        # Health only. No auth, SecretId consumption, or secret reads.
        with urllib.request.urlopen(address+'/v1/sys/health', timeout=10, context=ssl.create_default_context()) as response:
            body = json.load(response)
            result['openbao_health'] = dict(http=response.status, initialized=body.get('initialized'), sealed=body.get('sealed'))
    except urllib.error.HTTPError as error:
        result['openbao_health'] = dict(http=error.code)
    except Exception as error:
        result['openbao_health'] = dict(error_type=type(error).__name__)
result['read_only_network_checks'] = {}
for label, url in [('portal_health', origin(values.get('Bridge__RequestPortalBaseUrl', ''))+'/health'),
                   ('bridge_health','https://kratos.fftpj.local:5440/health'),
                   ('bridge_ready','https://kratos.fftpj.local:5440/ready')]:
    try:
        with urllib.request.urlopen(url, timeout=10, context=ssl.create_default_context()) as response:
            result['read_only_network_checks'][label] = dict(http=response.status, tls_verified=True)
    except urllib.error.HTTPError as error:
        result['read_only_network_checks'][label] = dict(http=error.code)
    except Exception as error:
        result['read_only_network_checks'][label] = dict(error_type=type(error).__name__)
root = pathlib.Path(values.get('Bridge__SupportingDocumentRoot', ''))
storage_errors = checker['check_storage'](values, pathlib.Path('/proc/self/mountinfo').read_text())
result['storage_mount_errors'] = storage_errors
if not storage_errors:
    probe = None
    try:
        with os.scandir(root) as entries: next(entries, None)
        descriptor, probe = tempfile.mkstemp(prefix='.bridge-commissioning-',dir=root)
        with os.fdopen(descriptor,'wb') as stream:
            stream.write(b'synthetic bridge commissioning access probe\n'); stream.flush(); os.fsync(stream.fileno())
        result['nas_service_read_write_fsync'] = 'passed'
    except Exception as error:
        result['nas_service_read_write_fsync'] = dict(error_type=type(error).__name__)
    finally:
        if probe is not None:
            os.unlink(probe)
            result['nas_probe_removed'] = True
print(json.dumps(result, indent=2))
