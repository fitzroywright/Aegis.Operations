#!/usr/bin/env python3
"""Interactive LDAP login and exposed-token revocation, without credential output.

Run in a user-controlled terminal. Secrets are entered with echo disabled and the
fresh session is saved only in a private directory outside the repository.
"""
import argparse
import getpass
import json
import os
import pathlib
import re
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request
import warnings

ENDPOINT = 'https://openbao.ffpja.org'

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,request,fp,code,message,headers,newurl):
        return None

def api(path, token=None, payload=None):
    headers = {'Content-Type':'application/json'}
    if token: headers['X-Vault-Token'] = token
    request = urllib.request.Request(ENDPOINT+'/v1/'+path,
        data=json.dumps(payload).encode() if payload is not None else None,headers=headers)
    try:
        opener=urllib.request.build_opener(urllib.request.HTTPSHandler(context=ssl.create_default_context()),NoRedirect())
        with opener.open(request,timeout=20) as response:
            raw=response.read()
            return response.status,json.loads(raw) if raw else {}
    except urllib.error.HTTPError as error:
        try: body=json.loads(error.read())
        except (ValueError,OSError): body={}
        return error.code,body

def private_json(path, value):
    descriptor=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
    with os.fdopen(descriptor,'w') as stream:
        json.dump(value,stream);stream.write('\n');stream.flush();os.fsync(stream.fileno())

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--session-dir',required=True)
    parser.add_argument('--auth-mount',default='ldap')
    args=parser.parse_args()
    directory=pathlib.Path(args.session_dir)
    info=directory.lstat()
    if directory.is_symlink() or info.st_uid!=os.getuid() or info.st_mode & 0o077:
        raise RuntimeError('A private session directory is required')
    if not sys.stdin.isatty(): raise RuntimeError('An interactive terminal is required')
    warnings.simplefilter('error',getpass.GetPassWarning)
    print('OpenBao secure administrator login. No passwords, tokens or accessors are displayed.')
    username=input('LDAP username [fwright]: ').strip() or 'fwright'
    accessor=getpass.getpass('Exposed token ACCESSOR (not the token; hidden input): ').strip()
    if not re.fullmatch(r'[A-Za-z0-9_-]{8,128}',accessor):
        raise RuntimeError('A valid known accessor is required; obtain it from the exposing operator')
    password=getpass.getpass('LDAP password (hidden input): ')
    code,body=api('auth/'+urllib.parse.quote(args.auth_mount.strip('/'),safe='')+'/login/'+urllib.parse.quote(username,safe=''),payload={'password':password})
    password=None
    if code!=200:
        raise RuntimeError('LDAP authentication failed (HTTP '+str(code)+'); no credential details emitted')
    auth=body.get('auth',{})
    token=auth.get('client_token')
    if not token:raise RuntimeError('LDAP did not return an administrator session')
    retained=False
    try:
        if 'it-admin' not in auth.get('policies',[]):
            raise RuntimeError('Fresh LDAP session does not have the required it-admin policy')
        if accessor==auth.get('accessor'):
            raise RuntimeError('Refusing to revoke the fresh independent session as the exposed token')
        code,lookup=api('auth/token/lookup-accessor',token,{'accessor':accessor})
        absent=code in (400,404) and 'invalid accessor' in json.dumps(lookup).lower()
        if not absent:
            if code!=200:raise RuntimeError('Cannot verify exposed accessor (HTTP '+str(code)+')')
            code,_=api('auth/token/revoke-accessor',token,{'accessor':accessor})
            if code!=204:raise RuntimeError('Accessor revocation failed (HTTP '+str(code)+')')
            code,lookup=api('auth/token/lookup-accessor',token,{'accessor':accessor})
            absent=code in (400,404) and 'invalid accessor' in json.dumps(lookup).lower()
            if not absent:raise RuntimeError('Revocation could not be independently verified')
        private_json(directory/'admin-session.json',{'endpoint':ENDPOINT,'token':token,'source':'fresh-independent-ldap','exposed_accessor_revocation_verified':True})
        retained=True
        summary={'status':'ready','ldap_authenticated':True,'it_admin_policy_present':True,
            'exposed_accessor_revocation_verified':True,'session_outside_repository':True,'endpoint':ENDPOINT,
            'lease_seconds':auth.get('lease_duration'),'renewable':auth.get('renewable')}
        private_json(directory/'status.json',summary)
        print('SUCCESS: Independent LDAP authentication and exposed-accessor revocation verified.')
        print('Secure session is ready. Return to Codex; no credentials need to be copied.')
    finally:
        if not retained:
            try:api('auth/token/revoke-self',token,{})
            except Exception:pass

if __name__=='__main__':
    try:main()
    except KeyboardInterrupt:
        print('\nCancelled; no credential values emitted.');sys.exit(2)
    except Exception as error:
        # Only our fixed error messages are safe; never print provider bodies.
        message=str(error) if type(error) is RuntimeError else type(error).__name__
        print('STOPPED: '+message)
        sys.exit(2)
