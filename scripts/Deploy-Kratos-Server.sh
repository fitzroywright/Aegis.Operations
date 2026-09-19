#!/usr/bin/env bash
set -Eeuo pipefail

SOURCE_ROOT="/tmp/aegis-deploy/payload"
APPS="operations,configuration,diagnostics,hello"
BACKUP_ROOT="/opt/aegis/backups"
OWNER="aegis:aegis"
HEALTH_RETRIES=10
HEALTH_DELAY=2

usage() {
  cat <<'EOF'
Usage: Deploy-Kratos-Server.sh [options]

Promotes already-staged Aegis publish outputs into /opt/aegis,
restarts the selected systemd services, validates local health endpoints, and
rolls back automatically if deployment health validation fails.

Options:
  --source PATH       Staging payload root. Default: /tmp/aegis-deploy/payload
  --apps LIST         Comma-separated: operations,configuration,diagnostics,hello,requestportal
                      Default: operations,configuration,diagnostics,hello
  --backup-root PATH  Backup root. Default: /opt/aegis/backups
  --owner USER:GROUP  Installed file owner. Default: aegis:aegis
  --help              Show this help

Expected staging layout:
  SOURCE/operations/Aegis.Operations.dll
  SOURCE/configuration/Aegis.Configuration.Web.dll
  SOURCE/diagnostics/Aegis.Diagnostics.dll
  SOURCE/hello/Aegis.Hello.dll
  SOURCE/requestportal/RequestPortal.Web.dll

This script preserves application environment files and secrets. During registration-lifecycle migration it only renames/removes obsolete registration keys after backing the files up.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source) SOURCE_ROOT="$2"; shift 2 ;;
    --apps) APPS="$2"; shift 2 ;;
    --backup-root) BACKUP_ROOT="$2"; shift 2 ;;
    --owner) OWNER="$2"; shift 2 ;;
    --help|-h) usage; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

if [[ ${EUID:-$(id -u)} -ne 0 ]]; then
  echo "ERROR: run this script as root." >&2
  exit 1
fi

IFS=',' read -r -a REQUESTED <<< "$APPS"

declare -A TARGET=(
  [operations]="/opt/aegis/operations"
  [configuration]="/opt/aegis/configuration"
  [diagnostics]="/opt/aegis/diagnostics"
  [hello]="/opt/aegis/hello"
  [requestportal]="/opt/aegis/requestportal"
)
declare -A SERVICE=(
  [operations]="aegis-operations"
  [configuration]="aegis-configuration"
  [diagnostics]="aegis-diagnostics"
  [hello]="aegis-hello"
  [requestportal]="aegis-requestportal"
)
declare -A DLL=(
  [operations]="Aegis.Operations.dll"
  [configuration]="Aegis.Configuration.Web.dll"
  [diagnostics]="Aegis.Diagnostics.dll"
  [hello]="Aegis.Hello.dll"
  [requestportal]="RequestPortal.Web.dll"
)
declare -A HEALTH=(
  [operations]="http://127.0.0.1:5100/health"
  [configuration]="http://127.0.0.1:5200/health"
  [diagnostics]="http://127.0.0.1:5300/health"
  [hello]="http://127.0.0.1:5400/health"
  [requestportal]="http://127.0.0.1:5401/health"
)

SELECTED=()
for raw in "${REQUESTED[@]}"; do
  app="$(printf '%s' "$raw" | tr '[:upper:]' '[:lower:]' | xargs)"
  [[ -n "$app" ]] || continue
  if [[ -z "${TARGET[$app]+x}" ]]; then
    echo "ERROR: unsupported application '$app'." >&2
    exit 2
  fi
  SELECTED+=("$app")
done

if [[ ${#SELECTED[@]} -eq 0 ]]; then
  echo "ERROR: no applications selected." >&2
  exit 2
fi

for app in "${SELECTED[@]}"; do
  staged="$SOURCE_ROOT/$app"
  if [[ ! -d "$staged" ]]; then
    echo "ERROR: staging directory missing: $staged" >&2
    exit 1
  fi
  if [[ ! -f "$staged/${DLL[$app]}" ]]; then
    echo "ERROR: expected publish artifact missing: $staged/${DLL[$app]}" >&2
    exit 1
  fi
done

STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_DIR="$BACKUP_ROOT/$STAMP"
mkdir -p "$BACKUP_DIR"

log() { printf '\n==> %s\n' "$*"; }

is_selected() {
  local wanted="$1"
  local current
  for current in "${SELECTED[@]}"; do
    [[ "$current" == "$wanted" ]] && return 0
  done
  return 1
}

stop_selected() {
  # Dependents first; RequestPortal is downstream of the control-plane services.
  for app in requestportal hello operations diagnostics configuration; do
    if is_selected "$app"; then
      systemctl stop "${SERVICE[$app]}" || true
    fi
  done
}

start_selected() {
  # Operations is the registration authority; start it before Configuration and Diagnostics.
  for app in operations configuration diagnostics hello requestportal; do
    if is_selected "$app"; then
      systemctl start "${SERVICE[$app]}"
    fi
  done
}

backup_current() {
  for app in "${SELECTED[@]}"; do
    target="${TARGET[$app]}"
    if [[ -d "$target" ]]; then
      mkdir -p "$BACKUP_DIR/$app"
      cp -a "$target/." "$BACKUP_DIR/$app/"
    else
      touch "$BACKUP_DIR/$app.__absent__"
    fi
  done

  mkdir -p "$BACKUP_DIR/etc-aegis"
  for app in "${SELECTED[@]}"; do
    env_file="/etc/aegis/$app.env"
    [[ -f "$env_file" ]] && cp -a "$env_file" "$BACKUP_DIR/etc-aegis/"
  done
}

cleanup_legacy_registration() {
  log "Removing obsolete ordinary-application registration keys"

  shopt -s nullglob
  for env_file in /etc/aegis/*.env; do
    # Normal applications no longer consume any pre-shared registration key.
    # Control-plane bootstrap credentials are file-backed and are not stored here.
    sed -i '/^AEGIS_REGISTRATION_KEY=/d' "$env_file"
    sed -i '/^AEGIS_CONFIGURATION_REGISTRATION_KEY=/d' "$env_file"
    sed -i '/^Operations__ConfigurationRegistrationKey=/d' "$env_file"
    sed -i '/^AegisConfiguration__RegistrationKey=/d' "$env_file"
    sed -i '/^Aegis__Registration__Key=/d' "$env_file"
  done
  shopt -u nullglob

  rm -f /var/lib/aegis/configuration/bootstrap-credentials.json
}

deploy_payload() {
  for app in "${SELECTED[@]}"; do
    target="${TARGET[$app]}"
    log "Deploying $app to $target"
    rm -rf "$target"
    mkdir -p "$target"
    cp -a "$SOURCE_ROOT/$app/." "$target/"
    if [[ "$app" == "configuration" ]]; then
      # Runtime configuration contracts are persistent state, never deployment content.
      rm -rf "$target/contracts"
    fi
    chown -R "$OWNER" "$target"
    if [[ -d "$target/scripts" ]]; then
      find "$target/scripts" -maxdepth 1 -type f \( -name '*.sh' -o -name 'aegis-hello' \) -exec chmod 0755 {} +
    fi
  done
}

health_check() {
  local app="$1"
  local url="${HEALTH[$app]}"
  local attempt
  for ((attempt=1; attempt<=HEALTH_RETRIES; attempt++)); do
    if curl --fail --silent --show-error --max-time 5 "$url" >/dev/null; then
      echo "PASS: $app health $url"
      return 0
    fi
    sleep "$HEALTH_DELAY"
  done
  echo "FAIL: $app health $url" >&2
  return 1
}

rollback() {
  echo >&2
  echo "DEPLOYMENT FAILED - ROLLING BACK TO $BACKUP_DIR" >&2
  stop_selected
  for app in "${SELECTED[@]}"; do
    target="${TARGET[$app]}"
    rm -rf "$target"
    if [[ -f "$BACKUP_DIR/$app.__absent__" ]]; then
      continue
    fi
    mkdir -p "$target"
    cp -a "$BACKUP_DIR/$app/." "$target/"
    chown -R "$OWNER" "$target"
  done
  start_selected || true
  echo "Rollback attempted. Inspect systemctl/journalctl before retrying." >&2
}

log "Ensuring persistent control-plane state storage"
mkdir -p /var/lib/aegis/operations/registration
mkdir -p /var/lib/aegis/configuration/contracts/retired
mkdir -p /var/lib/aegis/diagnostics
mkdir -p /var/lib/aegis/hello
chown -R "$OWNER" /var/lib/aegis/operations /var/lib/aegis/configuration /var/lib/aegis/diagnostics /var/lib/aegis/hello
chmod 700 /var/lib/aegis/operations/registration /var/lib/aegis/configuration /var/lib/aegis/diagnostics /var/lib/aegis/hello

if is_selected hello; then
  log "Ensuring Aegis.Hello systemd service"
  mkdir -p /etc/aegis
  touch /etc/aegis/hello.env
  chmod 600 /etc/aegis/hello.env
  if [[ ! -f /etc/systemd/system/aegis-hello.service ]]; then
    cat >/etc/systemd/system/aegis-hello.service <<'EOF'
[Unit]
Description=Aegis Hello
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=aegis
Group=aegis
WorkingDirectory=/opt/aegis/hello
ExecStart=/usr/bin/dotnet /opt/aegis/hello/Aegis.Hello.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5400
EnvironmentFile=/etc/aegis/hello.env
Restart=always
RestartSec=5
SyslogIdentifier=aegis-hello

[Install]
WantedBy=multi-user.target
EOF
    systemctl daemon-reload
    systemctl enable aegis-hello.service
  fi
fi

log "Backing up current deployment to $BACKUP_DIR"
backup_current

cleanup_legacy_registration

# Only enable automatic rollback after the backup has completed successfully.
trap 'rc=$?; if [[ $rc -ne 0 ]]; then rollback; fi; exit $rc' EXIT

log "Stopping selected services"
stop_selected

log "Promoting staged publish outputs"
deploy_payload

log "Starting selected services"
start_selected

log "Validating services"
for app in "${SELECTED[@]}"; do
  systemctl --no-pager --full status "${SERVICE[$app]}" >/dev/null
  health_check "$app"
done

trap - EXIT

log "Deployment succeeded"
echo "Backup: $BACKUP_DIR"
echo "Preserved: /etc/aegis/*.env and /etc/aegis/secrets"
for app in "${SELECTED[@]}"; do
  echo "  ${SERVICE[$app]} -> ${HEALTH[$app]}"
done
