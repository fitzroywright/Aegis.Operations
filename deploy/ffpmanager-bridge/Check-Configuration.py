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
from urllib.parse import urlsplit

def check(values):
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
    if not root.is_absolute() or not root.is_dir() or not os.access(root, os.R_OK | os.W_OK | os.X_OK):
        errors.append('SupportingDocumentRoot must be an accessible mounted absolute directory')
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
    args = parser.parse_args()
    try:
        problems = check(read_environment(args.env_file) if args.env_file else os.environ)
    except (OSError, ValueError):
        print('Unable to parse configuration; check the environment template and file permissions.', file=sys.stderr)
        sys.exit(2)
    for problem in problems: print(problem, file=sys.stderr)
    sys.exit(2 if problems else 0)
