#!/usr/bin/env bash
set -Eeuo pipefail

source_root=''
probe_url=''
target=/opt/aegis/ffpmanager-bridge
state=/var/lib/aegis-ffpmanager-bridge
environment=/etc/aegis/ffpmanager-bridge.env
service=aegis-ffpmanager-bridge.service
owner=aegis-ffpmanager-bridge

while (($#)); do
  case "$1" in
    --source) source_root="${2:?Missing source}"; shift 2 ;;
    --probe-url) probe_url="${2:?Missing HTTPS origin}"; shift 2 ;;
    --help|-h) echo 'Usage: deployer --source PUBLISH_DIRECTORY --probe-url https://bridge-host[:port]'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ $EUID -eq 0 ]] || { echo 'Run as root.' >&2; exit 1; }
[[ -n "$source_root" && -f "$source_root/FfpManager.Bridge.dll" ]] || { echo 'Missing bridge publish output.' >&2; exit 2; }
[[ "$probe_url" =~ ^https://[A-Za-z0-9.-]+(:[0-9]+)?/?$ ]] || { echo 'A trusted HTTPS origin is required.' >&2; exit 2; }
probe_url="${probe_url%/}"
for command in curl systemctl cp stat getent python3; do command -v "$command" >/dev/null; done
getent passwd "$owner" >/dev/null || { echo 'Provision the dedicated service identity first.' >&2; exit 2; }
[[ -f "$environment" && ! -L "$environment" ]] || { echo 'Provision the root-owned environment file first.' >&2; exit 2; }
[[ "$(stat -c %u "$environment")" == 0 && "$(stat -c %a "$environment")" == 600 ]] || { echo 'Environment file must be root-owned mode 600.' >&2; exit 2; }
[[ -f /etc/systemd/system/$service ]] || { echo 'Provision the bridge systemd unit first.' >&2; exit 2; }

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
