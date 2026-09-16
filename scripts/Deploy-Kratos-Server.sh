#!/usr/bin/env bash
set -Eeuo pipefail

SOURCE_ROOT="/tmp/aegis-deploy/payload"
APPS="operations,configuration,diagnostics"
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
  --apps LIST         Comma-separated: operations,configuration,diagnostics,requestportal
                      Default: operations,configuration,diagnostics
  --backup-root PATH  Backup root. Default: /opt/aegis/backups
  --owner USER:GROUP  Installed file owner. Default: aegis:aegis
  --help              Show this help

Expected staging layout:
  SOURCE/operations/Aegis.Operations.dll
  SOURCE/configuration/Aegis.Configuration.Web.dll
  SOURCE/diagnostics/Aegis.Diagnostics.dll
  SOURCE/requestportal/RequestPortal.Web.dll

This script never overwrites /etc/aegis/*.env or /etc/aegis/secrets.
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
  [requestportal]="/opt/aegis/requestportal"
)
declare -A SERVICE=(
  [operations]="aegis-operations"
  [configuration]="aegis-configuration"
  [diagnostics]="aegis-diagnostics"
  [requestportal]="aegis-requestportal"
)
declare -A DLL=(
  [operations]="Aegis.Operations.dll"
  [configuration]="Aegis.Configuration.Web.dll"
  [diagnostics]="Aegis.Diagnostics.dll"
  [requestportal]="RequestPortal.Web.dll"
)
declare -A HEALTH=(
  [operations]="http://127.0.0.1:5100/health"
  [configuration]="http://127.0.0.1:5200/health"
  [diagnostics]="http://127.0.0.1:5300/health"
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
  for app in requestportal operations diagnostics configuration; do
    if is_selected "$app"; then
      systemctl stop "${SERVICE[$app]}" || true
    fi
  done
}

start_selected() {
  # Registry first, then diagnostics/operations, then downstream applications.
  for app in configuration diagnostics operations requestportal; do
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

deploy_payload() {
  for app in "${SELECTED[@]}"; do
    target="${TARGET[$app]}"
    log "Deploying $app to $target"
    rm -rf "$target"
    mkdir -p "$target"
    cp -a "$SOURCE_ROOT/$app/." "$target/"
    chown -R "$OWNER" "$target"
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

log "Backing up current deployment to $BACKUP_DIR"
backup_current

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
