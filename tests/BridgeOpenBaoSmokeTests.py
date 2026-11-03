"""New credential-installer invariants, using disposable paths and synthetic input.

No host configuration, OpenBao API, or real credential is accessed.
"""
import contextlib
import io
import os
import pathlib
import tempfile
import types
from unittest.mock import patch

source = (pathlib.Path(__file__).resolve().parents[1] / 'deploy/ffpmanager-bridge/Configure-OpenBao.py').read_text()
# Fixture ownership uses the caller UID. Production still requires UID 0.
source = source.replace('info.st_uid!=0', 'info.st_uid!=os.getuid()')
source = source.replace('os.chown(temporary,0,account.pw_gid)', 'os.chown(temporary,os.getuid(),account.pw_gid)')
module = types.ModuleType('isolated_openbao_fixture')
exec(compile(source, '<isolated fixture>', 'exec'), module.__dict__)
synthetic = b'00000000-0000-4000-8000-000000000001\n'
account = types.SimpleNamespace(pw_gid=os.getgid())

with tempfile.TemporaryDirectory() as td:
    directory = pathlib.Path(td)
    directory.chmod(0o750)
    module.SECRET_FILE = directory / 'secret-id'
    output = io.StringIO()
    with patch.object(module.subprocess, 'run', return_value=types.SimpleNamespace(returncode=3)), \
         patch.object(module.sys, 'stdin', types.SimpleNamespace(buffer=io.BytesIO(synthetic))), \
         contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
        module.install_secretid(account)
    assert module.SECRET_FILE.read_bytes() == synthetic
    assert module.SECRET_FILE.stat().st_mode & 0o777 == 0o640
    assert not list(directory.glob('.openbao-secretid-*'))
    assert synthetic.strip().decode() not in output.getvalue()
    print('PASS protected stdin installer: mode640, no credential echo, temporary removed')

    with patch.object(module.subprocess, 'run', return_value=types.SimpleNamespace(returncode=3)):
        try:
            module.install_secretid(account)
            raise AssertionError('Existing credential must be preserved')
        except RuntimeError:
            pass
    assert module.SECRET_FILE.read_bytes() == synthetic
    print('PASS existing credential refused and preserved')

    module.SECRET_FILE = directory / 'active-service-secret'
    with patch.object(module.subprocess, 'run', return_value=types.SimpleNamespace(returncode=0)):
        try:
            module.install_secretid(account)
            raise AssertionError('Active service must block installation')
        except RuntimeError:
            pass
    assert not module.SECRET_FILE.exists()
    print('PASS active-service rotation refused before reading stdin')

    with patch.object(module.subprocess, 'run', return_value=types.SimpleNamespace(returncode=3)), \
         patch.object(module.sys, 'stdin', types.SimpleNamespace(buffer=io.BytesIO(b'not a credential'))):
        try:
            module.install_secretid(account)
            raise AssertionError('Invalid input must not create a credential')
        except RuntimeError:
            pass
    assert not module.SECRET_FILE.exists()
    print('PASS invalid input refused without creating a credential')
