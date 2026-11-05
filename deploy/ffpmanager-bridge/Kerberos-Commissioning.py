#!/usr/bin/python3
"""Fixed-scope, non-secret Kerberos metadata and AD inspection for the bridge.

Host keytab use is limited to read-only LDAP discovery in a disposable root cache.
It never opens SQL or treats the host identity as the bridge SQL identity.
"""
import base64
import json
import os
import pathlib
import pwd
import re
import runpy
import shutil
import stat
import subprocess
import tempfile

PRINCIPAL='svc_ffpbridge@FFTPJ.LOCAL'
OWNER='aegis-ffpmanager-bridge'
KEYTAB=pathlib.Path('/etc/aegis/ffpmanager-bridge/svc_ffpbridge.keytab')
CACHE=pathlib.Path('/var/lib/aegis-ffpmanager-bridge/krb5cc')

def command(args,env=None):
    return subprocess.run(args,capture_output=True,text=True,timeout=25,env=env)

def metadata(path):
    try:
        info=path.stat()
        return dict(exists=True,uid=info.st_uid,gid=info.st_gid,mode=oct(stat.S_IMODE(info.st_mode)),nonempty=info.st_size>0)
    except FileNotFoundError:return dict(exists=False)

def ldif(text):
    records=[];record={};unfolded=re.sub(r'\n ', '', text)
    for line in unfolded.splitlines()+['']:
        if not line:
            if record:records.append(record);record={}
            continue
        key,sep,value=line.partition(':')
        if not sep or line.startswith('#'):continue
        if value.startswith(':'):value=base64.b64decode(value[1:].strip()).decode('utf-8')
        record.setdefault(key,[]).append(value.strip())
    return records

def main():
    if os.geteuid()!=0:raise RuntimeError('Root-owned deployment entrypoint required')
    account=pwd.getpwnam(OWNER)
    result={'intended_principal':PRINCIPAL,'bridge_keytab':metadata(KEYTAB),'private_cache':metadata(CACHE)}
    if KEYTAB.is_file():
        entries=command(['/usr/bin/klist','-k','-e',str(KEYTAB)])
        result['bridge_keytab_entry_metadata']=entries.stdout.strip() if entries.returncode==0 else 'unreadable'
    if CACHE.is_file():
        listing=command(['/usr/bin/klist','-c','FILE:'+str(CACHE)])
        match=re.search(r'Default principal:\s*(\S+)',listing.stdout)
        result['cache_principal']=match[1] if match else None
        result['private_cache_valid']=command(['/usr/bin/klist','-s','-c','FILE:'+str(CACHE)]).returncode==0
    result['kerberos_package']=command(['/usr/bin/dpkg-query','-W','-f=${Version}','krb5-user']).stdout.strip()
    result['clock_sync']=command(['/usr/bin/timedatectl','show','-p','NTPSynchronized','--value']).stdout.strip()
    result['nss_account_resolution']=command(['/usr/bin/getent','passwd','svc_ffpbridge@fftpj.local']).returncode==0
    configuration=pathlib.Path('/etc/sssd/sssd.conf')
    if configuration.is_file():
        result['domain_membership_settings']={key:value.strip() for key,value in re.findall(r'(?m)^\s*(ad_domain|krb5_realm|id_provider|auth_provider)\s*=\s*([^\n]+)',configuration.read_text())}
    env_file=pathlib.Path('/etc/aegis/ffpmanager-bridge.env')
    checker=runpy.run_path('/usr/local/libexec/aegis-check-requestportal-bridge')
    values=checker['read_environment'](env_file)
    result['existing_cache_configuration']=values.get('KRB5CCNAME')
    result['existing_client_keytab_configuration']=values.get('KRB5_CLIENT_KTNAME')
    result['sql_secret_name']=values.get('Bridge__SqlConnectionStringSecretName','FfpManagerBridge:SqlConnectionString')
    prefix='CommonSecrets__Providers__PrimarySecrets__Settings__'
    result['openbao_public_settings']={key:values.get(prefix+key) for key in ('Address','MountPath','BasePath')}
    result['existing_kerberos_units']=list(pathlib.Path('/etc/systemd/system').glob('*ffpmanager*krb*'))+list(pathlib.Path('/etc/systemd/system').glob('*ffpmanager*kerberos*'))
    result['existing_kerberos_units']=[str(path) for path in result['existing_kerberos_units']]
    # Directory reads use only the existing host machine credential, never a
    # personal or application account, and leave no global root ticket behind.
    temp=pathlib.Path(tempfile.mkdtemp(prefix='aegis-bridge-ad-read-',dir='/run'))
    cc='FILE:'+str(temp/'host-cache')
    env={**os.environ,'KRB5CCNAME':cc,'LDAPTLS_REQCERT':'demand'}
    try:
        acquired=command(['/usr/bin/kinit','-k','-t','/etc/krb5.keytab','-c',cc,'KRATOS$@FFTPJ.LOCAL'],env)
        result['host_ldap_tgt_exit']=acquired.returncode
        if acquired.returncode==0:
            queries={
                'account':('DC=fftpj,DC=local','(&(objectClass=user)(sAMAccountName=svc_ffpbridge))',['distinguishedName','sAMAccountName','userPrincipalName','userAccountControl','msDS-User-Account-Control-Computed','msDS-SupportedEncryptionTypes','msDS-KeyVersionNumber','pwdLastSet','accountExpires','msDS-UserPasswordExpiryTimeComputed','memberOf','servicePrincipalName']),
                'sql_spn':('DC=fftpj,DC=local','(servicePrincipalName=MSSQLSvc/JMDB01*)',['distinguishedName','sAMAccountName','userPrincipalName','servicePrincipalName']),
                'netbios':('CN=Partitions,CN=Configuration,DC=fftpj,DC=local','(&(objectClass=crossRef)(nCName=DC=fftpj,DC=local))',['nETBIOSName'])}
            for label,(base,query,attrs) in queries.items():
                response=command(['/usr/bin/ldapsearch','-LLL','-Q','-Y','GSSAPI','-o','nettimeout=8','-l','15','-H','ldaps://jmad01.fftpj.local','-b',base,query,*attrs],env)
                result[label+'_ldap_exit']=response.returncode
                if response.returncode==0:result[label+'_records']=ldif(response.stdout)
            records=result.get('account_records',[])
            if len(records)==1:
                record=records[0];uac=int(record.get('userAccountControl',['0'])[0]);encryption=int(record.get('msDS-SupportedEncryptionTypes',['0'])[0])
                result['ad_account_summary']={'exists':True,'enabled':not bool(uac&2),'password_not_required':bool(uac&32),'password_never_expires':bool(uac&65536),'preauthentication_required':not bool(uac&4194304),'aes128_enabled':bool(encryption&8),'aes256_enabled':bool(encryption&16),'principal_matches':record.get('userPrincipalName',[''])[0].lower()==PRINCIPAL.lower()}
            elif 'account_records' in result:result['ad_account_summary']={'exists':False if not records else 'ambiguous'}
    finally:
        command(['/usr/bin/kdestroy','-c',cc],env)
        shutil.rmtree(temp)
    os.initgroups(account.pw_name,account.pw_gid);os.setgid(account.pw_gid);os.setuid(account.pw_uid)
    result['effective_bridge_uid']=os.geteuid()
    result['keytab_readable_as_bridge']=os.access(KEYTAB,os.R_OK)
    result['bridge_private_ticket_valid']=command(['/usr/bin/klist','-s','-c','FILE:'+str(CACHE)]).returncode==0
    result['sql_connection_attempted']=False
    print(json.dumps(result,indent=2))

if __name__=='__main__':
    try:main()
    except Exception as error:
        print(json.dumps({'status':'blocked','failure_type':type(error).__name__}))
        raise SystemExit(2)
