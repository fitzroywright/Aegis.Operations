#!/usr/bin/python3
"""Check configuration without executing it or printing secret values.

With no arguments, inspect inherited systemd environment as the service identity.
--env-file supports the simple one-assignment-per-line template for admin preflight.
"""
import argparse
import os
import pathlib
import re
import shlex
import sys
import tempfile
from urllib.parse import urlsplit

MOUNT = '/mnt/ffpmanager-data'
SOURCE = '//nas01.fftpj.local/ffpmanager_data'

def check_storage(values, mountinfo):
    """Reject a local fallback directory, another share, or a read-only mount."""
    root = pathlib.Path(values.get('Bridge__SupportingDocumentRoot', ''))
    if not root.is_absolute() or not root.resolve().is_relative_to(pathlib.Path(MOUNT).resolve()):
        return ['SupportingDocumentRoot must be beneath ' + MOUNT]
    for line in mountinfo.splitlines():
        fields = line.split()
        if len(fields) < 10 or fields[4] != MOUNT:
            continue
        before, after = line.split(' - ', 1)
        filesystem, source, options = after.split()[:3]
        if filesystem == 'cifs' and source == SOURCE and 'rw' in before.split()[5].split(',') and 'rw' in options.split(','):
            return []
    return ['Required writable NAS01 CIFS share is not mounted at ' + MOUNT]

def check(values, mountinfo=None, read_only=False):
    errors = []
    def require(key):
        value = values.get(key, '').strip()
        if not value or 'CHANGE_ME' in key or 'CHANGE_ME' in value:
            errors.append('Configure ' + key)
        return value
    for key, expected in {
        'ASPNETCORE_ENVIRONMENT': 'Production', 'DOTNET_ENVIRONMENT': 'Production',
        'Bridge__Gateway': 'Sql', 'Bridge__EnableOutboxDispatch': 'true',
        'CommonSecrets__Mode': 'Production',
    }.items():
        if require(key).lower() != expected.lower(): errors.append(key + ' must be ' + expected)
    if require('Bridge__SqlTarget') not in ('Production', 'Rehearsal'):
        errors.append('Bridge__SqlTarget must explicitly select Production (JMDB01/FFPJM) or Rehearsal (JMDB01/FFP)')
    for key in ('Bridge__ServiceIdentity', 'Bridge__InitialApplicationStatus', 'AllowedHosts'):
        require(key)
    if '*' in values.get('AllowedHosts', ''): errors.append('AllowedHosts must be restricted')
    for prefix in ('Bridge__StatusMap__', 'Bridge__DocumentTypeMap__'):
        keys = [k for k in values if k.startswith(prefix)]
        if not keys: errors.append('Configure at least one ' + prefix + ' mapping')
        for key in keys: require(key)
    provider = require('CommonSecrets__ProviderOrder__0')
    settings = 'CommonSecrets__Providers__' + provider + '__Settings__'
    if values.get('CommonSecrets__Providers__' + provider + '__Type') != 'OpenBao':
        errors.append('This bootstrap template requires the approved OpenBao provider')
    for key in ('Bridge__RequestPortalBaseUrl', settings + 'Address'):
        try:
            url = urlsplit(require(key))
            if url.scheme != 'https' or not url.hostname or url.username or url.password:
                raise ValueError()
        except ValueError: errors.append(key + ' must be HTTPS without embedded credentials')
    listeners = require('ASPNETCORE_URLS').split(';')
    if any(not u.startswith('https://') for u in listeners):
        errors.append('This bootstrap requires direct HTTPS Kestrel listeners')
    for suffix in ('Enabled', 'RequireHttps', 'RequireSecretIdFileInProduction'):
        if values.get(settings + suffix, '').lower() != 'true': errors.append(settings + suffix + ' must be true')
    require(settings + 'RoleId'); require(settings + 'MountPath'); require(settings + 'BasePath')
    if values.get(settings + 'DeleteSecretIdFileAfterRead', '').lower() != 'false':
        errors.append('Persistent bootstrap requires DeleteSecretIdFileAfterRead=false, or a separately managed reprovisioning mechanism')
    for key in ('Kestrel__Certificates__Default__Path', 'Kestrel__Certificates__Default__KeyPath', settings + 'SecretIdFile'):
        path = pathlib.Path(require(key))
        if not path.is_absolute() or not path.is_file() or not os.access(path, os.R_OK) or path.stat().st_size == 0:
            errors.append('Missing, empty or unreadable file for ' + key)
        elif key.endswith('KeyPath') or key.endswith('SecretIdFile'):
            if path.stat().st_mode & 0o007: errors.append('Remove world permissions from file for ' + key)
    root = pathlib.Path(require('Bridge__SupportingDocumentRoot'))
    manager_root = require('Bridge__SupportingDocumentManagerRoot')
    if not manager_root.startswith('\\\\') or '/' in manager_root or any(part in ('.', '..') for part in manager_root.split('\\')):
        errors.append('SupportingDocumentManagerRoot must be an approved Windows UNC directory')
    if not root.is_absolute() or not root.is_dir() or not os.access(root, os.R_OK | os.W_OK | os.X_OK):
        errors.append('SupportingDocumentRoot must be an accessible mounted absolute directory')
    try:
        storage_errors = check_storage(values, pathlib.Path('/proc/self/mountinfo').read_text() if mountinfo is None else mountinfo)
        errors.extend(storage_errors)
        if not storage_errors and root.is_dir() and os.access(root, os.R_OK | os.W_OK | os.X_OK) and read_only:
            # Force an actual directory read under the service identity without
            # creating, changing, or deleting any NAS content.
            with os.scandir(root) as entries:
                next(entries, None)
            print('PASS: service identity can read the required CIFS document directory; write access is permission-only, not tested')
        if not storage_errors and root.is_dir() and os.access(root, os.R_OK | os.W_OK | os.X_OK) and not read_only:
            # fsync forces a server round trip: cached mount metadata alone cannot
            # establish that NAS01 is online. Remove only this unique probe file.
            probe = None
            try:
                descriptor, probe = tempfile.mkstemp(prefix='.bridge-preflight-', dir=root)
                with os.fdopen(descriptor, 'wb') as stream:
                    stream.write(b'bridge-storage-preflight\n')
                    stream.flush()
                    os.fsync(stream.fileno())
            finally:
                if probe is not None: os.unlink(probe)
    except (OSError, ValueError):
        errors.append('Unable to verify or write/fsync the required NAS01 CIFS mount')
    for key in ('Bridge__SqlConnectionString', 'Bridge__SharedSecret'):
        if values.get(key): errors.append('Resolve ' + key + ' through Common.Secrets instead')
    return errors

def read_environment(path):
    result = {}
    for number, line in enumerate(pathlib.Path(path).read_text().splitlines(), 1):
        line = line.strip()
        if not line or line.startswith('#'): continue
        key, separator, raw = line.partition('=')
        if not separator or not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*', key):
            raise ValueError('Invalid assignment on line ' + str(number))
        parts = shlex.split(raw, comments=False, posix=True)
        if len(parts) > 1: raise ValueError('Quote values containing spaces on line ' + str(number))
        if key in result: raise ValueError('Duplicate setting on line ' + str(number))
        result[key] = parts[0] if parts else ''
    return result

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--env-file')
    parser.add_argument('--read-only', action='store_true', help='Do not create or delete NAS probe files')
    parser.add_argument('--service-user', help='When root, drop privileges before checking file access')
    args = parser.parse_args()
    try:
        values = read_environment(args.env_file) if args.env_file else os.environ
        if args.read_only:
            info = pathlib.Path(args.env_file).stat() if args.env_file else None
            if info:
                print(f'Environment owner UID={info.st_uid}; mode={info.st_mode & 0o777:o}')
        if args.service_user:
            import pwd
            account = pwd.getpwnam(args.service_user)
            os.initgroups(account.pw_name, account.pw_gid)
            os.setgid(account.pw_gid)
            os.setuid(account.pw_uid)
        problems = check(values, read_only=args.read_only)
    except (OSError, ValueError):
        print('Unable to parse configuration; check the environment template and file permissions.', file=sys.stderr)
        sys.exit(2)
    for problem in problems: print(problem, file=sys.stderr)
    sys.exit(2 if problems else 0)
