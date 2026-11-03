#!/usr/bin/env bash
set -Eeuo pipefail

source_root=''
probe_url=''
target=/opt/aegis/ffpmanager-bridge
state=/var/lib/aegis-ffpmanager-bridge
environment=/etc/aegis/ffpmanager-bridge.env
service=aegis-ffpmanager-bridge.service
owner=aegis-ffpmanager-bridge
check_only=false
install_only=false
commissioning_check=false
prepare_tls_request=false
openbao_action=''

while (($#)); do
  case "$1" in
    --check) check_only=true; shift ;;
    --install-only) install_only=true; shift ;;
    --commissioning-check) commissioning_check=true; shift ;;
    --prepare-tls-request) commissioning_check=true; prepare_tls_request=true; shift ;;
    --openbao-check) openbao_action=check; shift ;;
    --configure-openbao) openbao_action=configure; shift ;;
    --secure-openbao-proxy) openbao_action=proxy; shift ;;
    --install-openbao-secretid) openbao_action=secretid; shift ;;
    --rotate-openbao-secretid) openbao_action=rotate; shift ;;
    --configure-openbao-scope) openbao_action=scope; shift ;;
    --probe-openbao) openbao_action=probe; shift ;;
    --kerberos-check) openbao_action=kerberos; shift ;;
    --source) source_root="${2:?Missing source}"; shift 2 ;;
    --probe-url) probe_url="${2:?Missing HTTPS origin}"; shift 2 ;;
    --help|-h) echo 'Usage: deployer --check | --commissioning-check | --prepare-tls-request | --openbao-check | --configure-openbao | --secure-openbao-proxy | --install-openbao-secretid | --install-only --source PUBLISH_DIRECTORY | --source PUBLISH_DIRECTORY --probe-url https://bridge-host[:port]'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ $EUID -eq 0 ]] || { echo 'Run as root.' >&2; exit 1; }
if [[ -n "$openbao_action" ]]; then
  [[ -z "$source_root" && -z "$probe_url" ]] && ! $commissioning_check && ! $install_only && ! $check_only || { echo 'OpenBao action cannot be combined with deployment.' >&2; exit 2; }
  if [[ "$openbao_action" == configure ]]; then
    exec /usr/bin/python3 /usr/local/libexec/aegis-configure-requestportal-bridge-openbao --configure
  fi
  if [[ "$openbao_action" == scope ]]; then
    exec /usr/bin/python3 /usr/local/libexec/aegis-configure-requestportal-bridge-openbao --configure --verified-scope
  fi
  if [[ "$openbao_action" == rotate ]]; then
    exec /usr/bin/python3 /usr/local/libexec/aegis-configure-requestportal-bridge-openbao --rotate-secretid
  fi
  if [[ "$openbao_action" == probe ]]; then
    exec /usr/bin/python3 /usr/local/libexec/aegis-configure-requestportal-bridge-openbao --probe
  fi
  if [[ "$openbao_action" == kerberos ]]; then
    exec /usr/bin/python3 /usr/local/libexec/aegis-check-requestportal-bridge-kerberos
  fi
  if [[ "$openbao_action" == proxy ]]; then
    exec /usr/bin/python3 /usr/local/libexec/aegis-configure-requestportal-bridge-openbao --secure-proxy
  fi
  if [[ "$openbao_action" == secretid ]]; then
    exec /usr/bin/python3 /usr/local/libexec/aegis-configure-requestportal-bridge-openbao --install-secretid
  fi
  exec /usr/bin/python3 /usr/local/libexec/aegis-configure-requestportal-bridge-openbao
fi
if $commissioning_check; then
  [[ $# -eq 0 && -z "$source_root" && -z "$probe_url" ]] && ! $install_only && ! $check_only || { echo 'Commissioning check cannot be combined with deployment.' >&2; exit 2; }
  if $prepare_tls_request; then
    exec /usr/bin/python3 /usr/local/libexec/aegis-inspect-requestportal-bridge --prepare-tls-request
  fi
  exec /usr/bin/python3 /usr/local/libexec/aegis-inspect-requestportal-bridge
fi
if ! $check_only; then
  [[ -n "$source_root" && -f "$source_root/FfpManager.Bridge.dll" ]] || { echo 'Missing bridge publish output.' >&2; exit 2; }
  $install_only || [[ "$probe_url" =~ ^https://[A-Za-z0-9.-]+(:[0-9]+)?/?$ ]] || { echo 'A trusted HTTPS origin is required.' >&2; exit 2; }
fi
$check_only && $install_only && { echo 'Choose --check or --install-only.' >&2; exit 2; }
probe_url="${probe_url%/}"
for command in curl systemctl cp stat getent python3 timeout; do command -v "$command" >/dev/null; done
getent passwd "$owner" >/dev/null || { echo 'Provision the dedicated service identity first.' >&2; exit 2; }
[[ -f "$environment" && ! -L "$environment" ]] || { echo 'Provision the root-owned environment file first.' >&2; exit 2; }
[[ "$(stat -c %u "$environment")" == 0 && "$(stat -c %a "$environment")" == 600 ]] || { echo 'Environment file must be root-owned mode 600.' >&2; exit 2; }
[[ -f /etc/systemd/system/$service ]] || { echo 'Provision the bridge systemd unit first.' >&2; exit 2; }
[[ -x /usr/local/libexec/aegis-check-requestportal-bridge ]] || { echo 'Run the current bridge bootstrap before deployment.' >&2; exit 2; }
# Check access as the actual service user without writing NAS probe files.
if $install_only && systemctl is-active --quiet "$service"; then
  echo 'Install-only requires an already stopped service; refusing to interrupt it.' >&2; exit 2
fi
if ! timeout --kill-after=5 30 /usr/bin/python3 /usr/local/libexec/aegis-check-requestportal-bridge --env-file "$environment" --service-user "$owner" --read-only; then
  $install_only || exit 2
  echo 'Configuration is incomplete; installing binaries only. Startup remains blocked.' >&2
fi
if $check_only; then echo 'Bridge configuration preflight passed; no application or database changes.'; exit 0; fi
systemctl daemon-reload

# Prevent publishing local settings or development secrets into the installed app.
[[ ! -f "$source_root/appsettings.Production.json" ]] || 
  python3 - "$source_root/appsettings.Production.json" <<'PY'
import json, sys
config = json.load(open(sys.argv[1]))
bridge = config.get('Bridge', {})
if bridge.get('SqlConnectionString') or bridge.get('SharedSecret'):
    raise SystemExit('Refusing embedded bridge SQL credentials or shared secret.')
PY

mkdir -p /opt/aegis/backups
backup=$(mktemp -d /opt/aegis/backups/requestportal-bridge-XXXXXXXX)
chmod 700 "$backup"
had_target=false
was_active=false
systemctl is-active --quiet "$service" && was_active=true
if [[ -d "$target" ]]; then
  cp -a "$target" "$backup/application"
  had_target=true
fi
mkdir -p "$state"
chown "$owner:$owner" "$state"
chmod 750 "$state"

rollback() {
  local rc=$?
  trap - EXIT
  if ((rc != 0)); then
    echo "Deployment failed; restoring application from $backup" >&2
    systemctl stop "$service" || true
    rm -rf -- "$target"
    if $had_target; then cp -a "$backup/application" "$target"; fi
    if $was_active; then systemctl start "$service" || true; fi
    echo 'Configuration, secrets, database and document storage were not changed.' >&2
  fi
  exit "$rc"
}
trap rollback EXIT
systemctl stop "$service"
rm -rf -- "$target"
mkdir -p "$target"
cp -a "$source_root/." "$target/"
rm -f "$target/appsettings.Development.json"
# Read-only application binaries: persistent diagnostics belong in StateDirectory.
chown -R root:"$owner" "$target"
chmod -R u=rwX,g=rX,o= "$target"
if $install_only; then
  python3 - "$target" "$owner" "$state" <<'PY'
import json, os, pathlib, pwd, subprocess, sys
root, owner, state = pathlib.Path(sys.argv[1]), sys.argv[2], pathlib.Path(sys.argv[3])
account = pwd.getpwnam(owner)
for path in (root, root / 'FfpManager.Bridge.dll', state):
    info = path.stat()
    print(f'Installed permissions: uid={info.st_uid} gid={info.st_gid} mode={info.st_mode & 0o777:o} {path}')
journal = subprocess.run(['journalctl', '-u', 'aegis-ffpmanager-bridge.service', '-n', '50', '-o', 'json', '--no-pager'], capture_output=True, text=True)
entries = [json.loads(line) for line in journal.stdout.splitlines() if line.startswith('{')]
# Never emit journal messages: exception text can contain credentials or PII.
print(json.dumps({'journal_read_exit': journal.returncode, 'journal_entries_up_to_50': len(entries), 'journal_errors': sum(int(entry.get('PRIORITY', 6)) <= 3 for entry in entries)}))
os.initgroups(account.pw_name, account.pw_gid)
os.setgid(account.pw_gid)
os.setuid(account.pw_uid)
for path in root.rglob('*'):
    if path.is_symlink(): raise SystemExit('Unexpected symlink in installed application')
    if not os.access(path, os.R_OK): raise SystemExit('Service identity cannot read installed application')
    if os.access(path, os.W_OK): raise SystemExit('Service identity must not be able to modify application files')
if not os.access(state, os.R_OK | os.W_OK | os.X_OK): raise SystemExit('Service state directory is inaccessible')
runtime = subprocess.run(['/usr/bin/dotnet', '--list-runtimes'], capture_output=True, text=True, check=True)
for framework in ('Microsoft.NETCore.App 10.0.', 'Microsoft.AspNetCore.App 10.0.'):
    if framework not in runtime.stdout: raise SystemExit('Required .NET 10 runtime unavailable to service user')
print(runtime.stdout.strip())
if (root / 'SHA256SUMS').is_file():
    result = subprocess.run(['sha256sum', '--check', '--status', 'SHA256SUMS'], cwd=root)
    if result.returncode: raise SystemExit('Installed artifact checksum verification failed')
for library in root.rglob('*.so'):
    result = subprocess.run(['ldd', str(library)], capture_output=True, text=True)
    if result.returncode or 'not found' in result.stdout:
        raise SystemExit('Missing native runtime dependency for ' + library.name)
print('PASS: installed artifact checksums, service-user read access, immutable binaries, state permissions and native library dependencies')
PY
  trap - EXIT
  echo "Binaries installed only; service remains stopped. Rollback copy: $backup"
  echo 'SQL target, queue impact and configuration must be verified before enabling or starting.'
  exit 0
fi
systemctl start "$service"

probe() {
  local endpoint=$1 attempt body
  body=$(mktemp)
  for ((attempt=1; attempt<=15; attempt++)); do
    if systemctl is-active --quiet "$service" &&
       curl --noproxy '*' --proto '=https' --fail --silent --show-error --max-time 10 \
         "$probe_url/$endpoint" -o "$body" &&
       python3 - "$body" "$endpoint" <<'PY'
import json, sys
try:
    value = json.load(open(sys.argv[1]))
    assert value.get('gateway', '').lower() == 'sql'
    assert value.get('status') == ('ok' if sys.argv[2] == 'health' else 'ready')
except (ValueError, AssertionError, AttributeError):
    raise SystemExit(1)
PY
    then
      rm -f "$body"
      echo "PASS: HTTPS $endpoint"
      return 0
    fi
    sleep 2
  done
  rm -f "$body"
  echo "FAIL: HTTPS $endpoint; inspect journalctl -u $service locally." >&2
  return 1
}
probe health
probe ready
trap - EXIT
echo "Bridge installed; rollback copy: $backup"
echo 'No SQL migrations, configuration updates, portal changes, or end-to-end acceptance were performed.'
