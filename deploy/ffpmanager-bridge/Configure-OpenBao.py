#!/usr/bin/python3
"""Fixed-scope bridge OpenBao audit/setup. Never emits credential values.

Does not create OpenBao resources, consume SecretIds, open SQL, or start services.
Only --configure updates supplied public settings and bridge directory traversal.
"""
import argparse
import ctypes
import ctypes.util
import json
import os
import pathlib
import pwd
import re
import runpy
import shutil
import socket
import ssl
import stat
import subprocess
import tempfile
import urllib.error
import urllib.request
import sys
import uuid
import hashlib

ENV = pathlib.Path('/etc/aegis/ffpmanager-bridge.env')
ENDPOINT = 'https://openbao.ffpja.org'
ROLE = 'aegis-ffpmanager-bridge'
ROLE_ID = '6b91b7c2-c97a-1841-787c-8ec5647f6670'
SECRET_FILE = pathlib.Path('/etc/aegis/ffpmanager-bridge/openbao-secret-id')
PREFIX = 'CommonSecrets__Providers__PrimarySecrets__Settings__'

def request(path, token=None):
    headers = {'X-Vault-Token':token} if token else {}
    req = urllib.request.Request(ENDPOINT+'/v1/'+path,headers=headers)
    try:
        with urllib.request.urlopen(req,context=ssl.create_default_context(),timeout=10) as response:
            return response.status,json.load(response)
    except urllib.error.HTTPError as error:
        return error.code,{}
    except Exception as error:
        return type(error).__name__,{}

def meta(path):
    try:
        info=path.stat()
        return dict(exists=True,uid=info.st_uid,gid=info.st_gid,mode=oct(stat.S_IMODE(info.st_mode)),nonempty=info.st_size>0)
    except FileNotFoundError:
        return dict(exists=False)

def grant_search(uid):
    # Preserve existing ACL entries and grant this one numeric UID search only.
    lib=ctypes.CDLL(ctypes.util.find_library('acl') or 'libacl.so.1',use_errno=True)
    lib.acl_get_file.argtypes=[ctypes.c_char_p,ctypes.c_int];lib.acl_get_file.restype=ctypes.c_void_p
    lib.acl_to_text.argtypes=[ctypes.c_void_p,ctypes.c_void_p];lib.acl_to_text.restype=ctypes.c_void_p
    lib.acl_from_text.argtypes=[ctypes.c_char_p];lib.acl_from_text.restype=ctypes.c_void_p
    lib.acl_calc_mask.argtypes=[ctypes.POINTER(ctypes.c_void_p)];lib.acl_calc_mask.restype=ctypes.c_int
    lib.acl_set_file.argtypes=[ctypes.c_char_p,ctypes.c_int,ctypes.c_void_p];lib.acl_set_file.restype=ctypes.c_int
    lib.acl_valid.argtypes=[ctypes.c_void_p];lib.acl_valid.restype=ctypes.c_int
    lib.acl_free.argtypes=[ctypes.c_void_p]
    original=lib.acl_get_file(b'/etc/aegis',0x8000)
    if not original: raise RuntimeError('Unable to read parent ACL')
    text=lib.acl_to_text(original,None)
    if not text: raise RuntimeError('Unable to serialize parent ACL')
    try:
        acl_text=ctypes.string_at(text).decode()
    finally:
        lib.acl_free(text);lib.acl_free(original)
    lines=acl_text.splitlines()
    matches=[line for line in lines if line.startswith((f'user:{uid}:',f'user:{ROLE}:'))]
    if matches:
        if len(matches)!=1 or matches[0].split(':')[2].split()[0]!='--x':
            raise RuntimeError('Unexpected existing bridge parent ACL; review required')
        return False
    if any(re.match(r'(?:user|group):[^:]+:',line) for line in lines):
        raise RuntimeError('Existing named parent ACL requires review before changing mask')
    backup=pathlib.Path('/var/backups/aegis-bridge-openbao')
    backup.mkdir(mode=0o700,exist_ok=True)
    os.chmod(backup,0o700)
    fd,saved=tempfile.mkstemp(prefix='parent-acl-',dir=backup)
    with os.fdopen(fd,'w') as stream:stream.write(acl_text)
    mask='' if any(line.startswith('mask:') for line in lines) else 'mask::--x\n'
    acl=ctypes.c_void_p(lib.acl_from_text((acl_text+f'user:{uid}:--x\n'+mask).encode()))
    if not acl: raise RuntimeError('Unable to prepare narrow parent ACL')
    try:
        if lib.acl_calc_mask(ctypes.byref(acl)) or lib.acl_valid(acl) or lib.acl_set_file(b'/etc/aegis',0x8000,acl):
            raise RuntimeError('Unable to install narrow parent ACL')
    finally:
        lib.acl_free(acl)
    return True

def secure_proxy():
    # Validate the actual existing listener before requiring upstream verification.
    with socket.create_connection(('loki.fftpj.local',8200),timeout=10) as sock:
        with ssl.create_default_context().wrap_socket(sock,server_hostname='openbao.ffpja.org') as tls:
            tls.getpeercert()
    enabled=pathlib.Path('/etc/nginx/sites-enabled/openbao.ffpja.org')
    target=enabled.resolve()
    if target.parent not in (pathlib.Path('/etc/nginx/sites-available'),pathlib.Path('/etc/nginx/sites-enabled')):
        raise RuntimeError('Unexpected OpenBao proxy configuration path')
    original=target.read_text()
    if re.findall(r'proxy_pass\s+([^;\s]+)',original)!=['https://loki.fftpj.local:8200']:
        raise RuntimeError('Unexpected OpenBao proxy upstream')
    hosts=re.findall(r'(?m)^\s*server_name\s+([^;\n]+)',original)
    if not hosts or not all(set(value.split())=={'openbao.ffpja.org'} for value in hosts):
        raise RuntimeError('Proxy file also contains an unrelated host')
    updated,count=re.subn(r'(?m)^(\s*)proxy_ssl_name\s+[^;]+;',r'\1proxy_ssl_name openbao.ffpja.org;',original)
    if count!=1:raise RuntimeError('Unexpected TLS-name directive count')
    for name,value in [('proxy_ssl_verify','on'),('proxy_ssl_trusted_certificate','/etc/ssl/certs/ca-certificates.crt'),('proxy_ssl_verify_depth','4')]:
        if re.search(r'\b'+name+r'\s+',updated):
            updated=re.sub(r'(?m)^(\s*)'+name+r'\s+[^;]+;',lambda match:match[1]+name+' '+value+';',updated)
        else:
            updated=re.sub(r'(?m)^(\s*)proxy_ssl_name\s+[^;]+;',lambda match:match[0]+'\n'+match[1]+name+' '+value+';',updated,count=1)
    if updated==original:return {'changed':False,'upstream_verification':True}
    backup=pathlib.Path('/var/backups/aegis-bridge-openbao');backup.mkdir(mode=0o700,exist_ok=True);os.chmod(backup,0o700)
    fd,saved=tempfile.mkstemp(prefix='nginx-openbao-',dir=backup);os.close(fd);shutil.copyfile(target,saved)
    reload_attempted=False
    try:
        target.write_text(updated)
        check=subprocess.run(['/usr/sbin/nginx','-t'],capture_output=True)
        if check.returncode:raise RuntimeError('OpenBao proxy configuration test failed')
        reload_attempted=True
        reload=subprocess.run(['/usr/bin/systemctl','reload','nginx'],capture_output=True)
        if reload.returncode:raise RuntimeError('OpenBao proxy reload failed')
        code,body=request('sys/health')
        if code!=200 or body.get('sealed'):raise RuntimeError('OpenBao proxy health failed after reload')
    except Exception:
        target.write_text(original)
        if reload_attempted:subprocess.run(['/usr/bin/systemctl','reload','nginx'],capture_output=True)
        raise
    return {'changed':True,'upstream_verification':True,'configuration_test_exit':0,'reload_exit':0,'health_http':code,'backup':saved}

def install_secretid(account, rotate=False):
    if subprocess.run(['/usr/bin/systemctl','is-active','--quiet',ROLE+'.service'],capture_output=True).returncode==0:
        raise RuntimeError('SecretId installation requires the bridge stopped')
    if SECRET_FILE.is_symlink():raise RuntimeError('Refusing a credential symlink')
    exists=SECRET_FILE.exists()
    if exists and not rotate:raise RuntimeError('Existing SecretId requires explicit rotation review')
    if exists:
        previous=SECRET_FILE.stat()
        if previous.st_uid!=0 or previous.st_gid!=account.pw_gid or stat.S_IMODE(previous.st_mode)!=0o640:
            raise RuntimeError('Existing credential ownership must be reviewed')
    directory=SECRET_FILE.parent
    info=directory.stat()
    if directory.is_symlink() or info.st_uid!=0 or info.st_gid!=account.pw_gid or stat.S_IMODE(info.st_mode)!=0o750:
        raise RuntimeError('Credential directory must first be prepared')
    value=sys.stdin.buffer.read(129).strip()
    if len(value)!=36:raise RuntimeError('Expected generated SecretId on protected stdin')
    uuid.UUID(value.decode('ascii'))
    fd,temporary=tempfile.mkstemp(prefix='.openbao-secretid-',dir=directory)
    try:
        with os.fdopen(fd,'wb') as stream:stream.write(value+b'\n');stream.flush();os.fsync(stream.fileno())
        os.chown(temporary,0,account.pw_gid);os.chmod(temporary,0o640)
        if exists:
            backup=pathlib.Path('/var/backups/aegis-bridge-openbao');backup.mkdir(mode=0o700,exist_ok=True);os.chmod(backup,0o700)
            fd,saved=tempfile.mkstemp(prefix='secretid-before-rotation-',dir=backup)
            with os.fdopen(fd,'wb') as stream:stream.write(SECRET_FILE.read_bytes());stream.flush();os.fsync(stream.fileno())
            current=SECRET_FILE.stat()
            if (current.st_ino,current.st_mtime_ns)!=(previous.st_ino,previous.st_mtime_ns):
                raise RuntimeError('Credential changed concurrently; rotation cancelled')
            os.replace(temporary,SECRET_FILE)
        else:
            os.link(temporary,SECRET_FILE)
    finally:
        if os.path.exists(temporary):os.unlink(temporary)
    value=None

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--configure',action='store_true')
    parser.add_argument('--secure-proxy',action='store_true')
    parser.add_argument('--install-secretid',action='store_true')
    parser.add_argument('--rotate-secretid',action='store_true')
    parser.add_argument('--verified-scope',action='store_true')
    parser.add_argument('--probe',action='store_true')
    args=parser.parse_args()
    if os.geteuid()!=0: raise RuntimeError('Use the root-owned deployment entrypoint')
    if ENV.is_symlink() or ENV.stat().st_uid!=0 or stat.S_IMODE(ENV.stat().st_mode)!=0o600:
        raise RuntimeError('Environment must be a regular root-owned mode600 file')
    checker=runpy.run_path('/usr/local/libexec/aegis-check-requestportal-bridge')
    values=checker['read_environment'](ENV)
    account=pwd.getpwnam(ROLE)
    if args.probe:
        probe=pathlib.Path('/opt/aegis/ffpmanager-bridge-probe')
        for name in ('Common.Secrets.dll','Common.Diagnostics.dll','FfpManager.Bridge.dll'):
            if hashlib.sha256((probe/name).read_bytes()).digest()!=hashlib.sha256((pathlib.Path('/opt/aegis/ffpmanager-bridge')/name).read_bytes()).digest():
                raise RuntimeError('Probe must use the exact deployed application assemblies')
        executable=probe/'BridgeCommissioningProbe.dll'
        if executable.is_symlink() or executable.stat().st_uid!=0:
            raise RuntimeError('Probe must be root-owned')
        os.initgroups(account.pw_name,account.pw_gid);os.setgid(account.pw_gid);os.setuid(account.pw_uid)
        environment={'PATH':'/usr/bin:/bin','HOME':'/var/lib/aegis-ffpmanager-bridge',**values}
        os.execve('/usr/bin/dotnet',['/usr/bin/dotnet',str(executable)],environment)
    if args.install_secretid or args.rotate_secretid:install_secretid(account,rotate=args.rotate_secretid)
    result={'endpoint':ENDPOINT,'new_secret_path':str(SECRET_FILE),'secret_file':meta(SECRET_FILE)}
    if args.secure_proxy:result['proxy_update']=secure_proxy()
    proxy=pathlib.Path('/etc/nginx/sites-enabled/openbao.ffpja.org').read_text()
    directives={}
    for name in ('listen','server_name','proxy_pass','proxy_ssl_server_name','proxy_ssl_name','proxy_ssl_verify','proxy_ssl_trusted_certificate','ssl_certificate'):
        directives[name]=re.findall(r'(?m)^\s*'+name+r'\s+([^;\n]+);',proxy)
    result['proxy_directives']=directives
    # Public consumer configuration only; never load another application's identity.
    portal_file=pathlib.Path('/etc/aegis/requestportal.env')
    portal={}
    if portal_file.is_file():
        for line in portal_file.read_text().splitlines():
            key,sep,raw=line.strip().partition('=')
            if sep and re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*',key):
                import shlex
                try:
                    parts=shlex.split(raw,comments=False,posix=True);portal[key]=parts[0] if len(parts)==1 else raw
                except ValueError:pass
    result['portal_public_secret_configuration']={k:v for k,v in portal.items() if
        (k.startswith('CommonSecrets__') and k.endswith(('__BasePath','__MountPath','__Address','__Type','__WorkloadName','__EnvironmentName')))
        or k in ('FfpManagerIntegration__SharedSecretName','FfpManagerIntegration__SharedSecretSecretName')}
    portal_json={}
    def merge(left,right):
        for key,value in right.items():
            if isinstance(value,dict) and isinstance(left.get(key),dict):merge(left[key],value)
            else:left[key]=value
    for name in ('appsettings.json','appsettings.Production.json'):
        path=pathlib.Path('/opt/aegis/requestportal')/name
        if path.is_file():merge(portal_json,json.loads(path.read_text()))
    settings=portal_json.get('CommonSecrets',{}).get('Providers',{}).get('PrimarySecrets',{}).get('Settings',{})
    portal_prefix='CommonSecrets__Providers__PrimarySecrets__Settings__'
    integration=portal_json.get('FfpManagerIntegration',{})
    result['portal_effective_public_reference']={
        'MountPath':portal.get(portal_prefix+'MountPath',settings.get('MountPath','secret')),
        'BasePath':portal.get(portal_prefix+'BasePath',settings.get('BasePath','aegis')),
        'SharedSecretName':portal.get('FfpManagerIntegration__SharedSecretName',integration.get('SharedSecretName','FfpManagerIntegration:SharedSecret')),
        'IntegrationEnabled':str(portal.get('FfpManagerIntegration__Enabled',integration.get('Enabled',False))).lower()=='true'}
    http,health=request('sys/health')
    result['health']={'http':http,'initialized':health.get('initialized'),'sealed':health.get('sealed')}
    homes={pathlib.Path('/root')}
    operator=os.environ.get('SUDO_USER')
    if operator:
        homes.add(pathlib.Path(pwd.getpwnam(operator).pw_dir))
    token=None
    for home in sorted(homes):
        for filename in ('.bao-token','.vault-token'):
            path=home/filename
            if path.is_file() and not path.is_symlink() and not path.stat().st_mode & 0o077:
                token=path.read_text().strip()
                if token: break
        if token: break
    result['operator_session_found']=bool(token)
    if token:
        calls={'role':'auth/approle/role/'+ROLE,'role_id':'auth/approle/role/'+ROLE+'/role-id','policy':'sys/policies/acl/'+ROLE,'mounts':'sys/mounts'}
        data={}
        for label,path in calls.items():
            code,body=request(path,token)
            result[label+'_read_http']=code
            data[label]=body.get('data',{})
        role=data['role']
        result['role_id_matches_supplied']=data['role_id'].get('role_id')==ROLE_ID
        result['role_settings']={k:role.get(k) for k in ('bind_secret_id','token_policies','token_no_default_policy','token_ttl','token_max_ttl','token_num_uses','secret_id_ttl','secret_id_num_uses')}
        policy=data['policy'].get('policy','')
        result['policy_rules']=[{'path':path,'capabilities':re.findall(r'"([a-z]+)"',body)} for path,body in re.findall(r'path\s+"([^"\n]+)"\s*\{([^}]+)\}',policy)]
        result['kv_mounts']={k:{'type':v.get('type'),'options':v.get('options')} for k,v in data['mounts'].items() if isinstance(v,dict) and v.get('type')=='kv'}
        # No resources or TTL/use limits are changed, even with an admin session.
    if args.configure:
        if http!=200 or health.get('sealed') or not health.get('initialized'):
            raise RuntimeError('Trusted endpoint health must pass before configuration')
        if any(path.is_symlink() for path in (pathlib.Path('/etc/aegis'),SECRET_FILE.parent,SECRET_FILE)):
            raise RuntimeError('Refusing symlink credential paths')
        updates={'CommonSecrets__ProviderOrder__0':'PrimarySecrets','CommonSecrets__Providers__PrimarySecrets__Type':'OpenBao',
            PREFIX+'Enabled':'true',PREFIX+'Address':ENDPOINT,PREFIX+'AuthMountPath':'approle',PREFIX+'RoleId':ROLE_ID,
            PREFIX+'SecretIdFile':str(SECRET_FILE),PREFIX+'RequireHttps':'true',PREFIX+'RequireSecretIdFileInProduction':'true',
            PREFIX+'DeleteSecretIdFileAfterRead':'false',PREFIX+'RenewTokens':'true',PREFIX+'RevokeTokenOnDispose':'true'}
        if args.verified_scope:
            # Verified via independently authenticated LDAP administrator reads:
            # KV v2 mount secret/ and existing policy secret/data/aegis/ffpmanager-bridge/*.
            updates.update({PREFIX+'MountPath':'secret',PREFIX+'BasePath':'aegis/ffpmanager-bridge'})
        # MountPath/BasePath are intentionally preserved until server policy and
        # mount evidence verify them. Never turn an unknown path into a guess.
        lines=ENV.read_text().splitlines(); seen=set(); changed=[]
        for i,line in enumerate(lines):
            key,sep,raw=line.strip().partition('=')
            if sep and key in updates:
                if key in seen: raise RuntimeError('Duplicate configuration key')
                seen.add(key)
                if values.get(key)!=updates[key]:
                    lines[i]=key+'='+updates[key];changed.append(key)
        for key in updates.keys()-seen:
            lines.append(key+'='+updates[key]);changed.append(key)
        backup=pathlib.Path('/var/backups/aegis-bridge-openbao')
        backup.mkdir(mode=0o700,exist_ok=True)
        os.chmod(backup,0o700)
        if changed:
            fd,saved=tempfile.mkstemp(prefix='environment-',dir=backup);os.close(fd)
            shutil.copyfile(ENV,saved);os.chmod(saved,0o600)
            fd,temporary=tempfile.mkstemp(prefix='.bridge-env-',dir=ENV.parent)
            try:
                with os.fdopen(fd,'w') as stream:
                    stream.write('\n'.join(lines)+'\n');stream.flush();os.fsync(stream.fileno())
                os.chmod(temporary,0o600);os.chown(temporary,0,0)
                checker['read_environment'](temporary)
                os.replace(temporary,ENV)
            finally:
                if os.path.exists(temporary):os.unlink(temporary)
            result['environment_backup']=saved
        SECRET_FILE.parent.mkdir(mode=0o750,exist_ok=True)
        os.chown(SECRET_FILE.parent,0,account.pw_gid);os.chmod(SECRET_FILE.parent,0o750)
        result['parent_search_acl_added']=grant_search(account.pw_uid)
        result['updated_keys']=sorted(changed)
        values=checker['read_environment'](ENV)
    result['mount_path_configured']=values.get(PREFIX+'MountPath')
    base=values.get(PREFIX+'BasePath','')
    result['base_path_configured']=bool(base and 'CHANGE_ME' not in base)
    result['role_id_in_env_matches_supplied']=values.get(PREFIX+'RoleId')==ROLE_ID
    result['env_secret_file_matches_requested']=values.get(PREFIX+'SecretIdFile')==str(SECRET_FILE)
    result['environment_file']=meta(ENV)
    os.initgroups(account.pw_name,account.pw_gid);os.setgid(account.pw_gid);os.setuid(account.pw_uid)
    result['effective_uid']=os.geteuid()
    result['service_can_search_parent']=os.access('/etc/aegis',os.X_OK)
    result['service_can_list_parent']=os.access('/etc/aegis',os.R_OK)
    result['secret_readable_as_service']=os.access(SECRET_FILE,os.R_OK)
    code,body=request('sys/health')
    result['service_https_health']={'http':code,'initialized':body.get('initialized'),'sealed':body.get('sealed')}
    result['approle_login_attempted']=False
    result['sql_connection_attempted']=False
    print(json.dumps(result,indent=2))

if __name__=='__main__':
    try: main()
    except Exception as error:
        # Neither provider response bodies nor exception text may reach output.
        safe_reasons={'Unexpected OpenBao proxy configuration path','Unexpected OpenBao proxy upstream','Proxy file also contains an unrelated host','Unexpected TLS-name directive count','OpenBao proxy configuration test failed','OpenBao proxy reload failed','OpenBao proxy health failed after reload','Existing named parent ACL requires review before changing mask','Unable to prepare narrow parent ACL','Unable to install narrow parent ACL'}
        print(json.dumps({'failed':True,'failure_type':type(error).__name__,
            'reason':str(error) if type(error) is RuntimeError and str(error) in safe_reasons else 'Details suppressed to protect credentials'}))
        raise SystemExit(2)
