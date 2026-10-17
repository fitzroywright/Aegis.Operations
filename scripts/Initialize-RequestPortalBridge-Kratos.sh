#!/usr/bin/env bash
set -Eeuo pipefail

# Run on Kratos from this repository checkout, using sudo. No network installs,
# SQL migrations, secret creation, service start, or replacement of existing config.
check_only=false
case "${1:-}" in
  '') ;;
  --check) check_only=true ;;
  --help|-h) echo 'Usage: sudo bash scripts/Initialize-RequestPortalBridge-Kratos.sh [--check]'; exit 0 ;;
  *) echo 'Unsupported argument.' >&2; exit 2 ;;
esac
[[ $# -le 1 ]] || { echo 'Too many arguments.' >&2; exit 2; }
[[ $EUID -eq 0 ]] || { echo 'Run using sudo on Kratos.' >&2; exit 1; }
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
assets="$repo/deploy/ffpmanager-bridge"
identity=aegis-ffpmanager-bridge
config=/etc/aegis/ffpmanager-bridge.env
unit=/etc/systemd/system/aegis-ffpmanager-bridge.service

if $check_only; then
  [[ -f "$config" && ! -L "$config" ]] || { echo 'Missing regular environment file; run initialization first.' >&2; exit 2; }
  [[ $(stat -c %u "$config") == 0 && $(stat -c %a "$config") == 600 ]] || { echo 'Environment file must be root-owned mode 600.' >&2; exit 2; }
  /usr/bin/python3 /usr/local/libexec/aegis-check-requestportal-bridge --env-file "$config"
  /usr/bin/dotnet --list-runtimes | grep -q 'Microsoft.AspNetCore.App 10\.' || { echo '.NET 10 ASP.NET runtime is required.' >&2; exit 2; }
  echo 'Local configuration checks passed. SQL, OpenBao, callbacks and HTTPS delivery still require live commissioning.'
  exit 0
fi

for command in python3 systemctl useradd groupadd getent install; do command -v "$command" >/dev/null; done
for file in "$assets/environment.example" "$assets/Check-Configuration.py" "$assets/aegis-ffpmanager-bridge.service" "$repo/scripts/Deploy-RequestPortalBridge-Kratos-Server.sh"; do
  [[ -f "$file" ]] || { echo "Missing installation asset: $file" >&2; exit 2; }
done
getent group "$identity" >/dev/null || groupadd --system "$identity"
getent passwd "$identity" >/dev/null || useradd --system --gid "$identity" --home-dir /var/lib/aegis-ffpmanager-bridge --shell /usr/sbin/nologin "$identity"
install -d -o root -g "$identity" -m 0750 /etc/aegis/ffpmanager-bridge /etc/aegis/ffpmanager-bridge/tls
install -d -o "$identity" -g "$identity" -m 0750 /var/lib/aegis-ffpmanager-bridge
install -d -o root -g root -m 0755 /opt/aegis /opt/aegis/backups /usr/local/libexec
if [[ -e "$config" || -L "$config" ]]; then
  [[ -f "$config" && ! -L "$config" ]] || { echo 'Refusing non-regular environment file.' >&2; exit 2; }
  echo "Preserved existing $config"
else
  install -o root -g root -m 0600 "$assets/environment.example" "$config"
  echo "Created $config; replace CHANGE_ME values before deployment."
fi
if [[ -e "$unit" || -L "$unit" ]]; then
  [[ -f "$unit" && ! -L "$unit" ]] || { echo 'Refusing non-regular systemd unit.' >&2; exit 2; }
  if ! cmp -s "$assets/aegis-ffpmanager-bridge.service" "$unit"; then
    echo "Preserved customized $unit; compare it with the repository template before deployment."
  else echo 'Existing service unit matches.'; fi
else install -o root -g root -m 0644 "$assets/aegis-ffpmanager-bridge.service" "$unit"; fi
install -o root -g root -m 0755 "$assets/Check-Configuration.py" /usr/local/libexec/aegis-check-requestportal-bridge
install -o root -g root -m 0755 "$repo/scripts/Deploy-RequestPortalBridge-Kratos-Server.sh" /usr/local/sbin/aegis-deploy-requestportal-bridge
systemctl daemon-reload
echo 'Bootstrap complete; service was not started or enabled.'
echo 'Complete environment, TLS, OpenBao and test DB/share setup; then rerun with --check.'
echo 'An administrator must configure deployment-account sudo access separately if needed.'
