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
  --apps LIST         Comma-separated: operations,configuration,diagnostics,hello,requestportal,studio,publicsite
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
  SOURCE/studio/Aegis.Studio.Web.dll\n  SOURCE/cafeteria/Aegis.Cafeteria.Services.dll\n  SOURCE/publicsite/server.mjs + package.json + public/

This script preserves application environment files and secrets. During registration-lifecycle migration it removes obsolete ordinary-application registration keys after backing the files up.
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
  [studio]="/opt/aegis/studio"
  [cafeteria]="/opt/aegis-cafeteria-services"\n  [publicsite]="/opt/aegis/public-site"
)
declare -A SERVICE=(
  [operations]="aegis-operations"
  [configuration]="aegis-configuration"
  [diagnostics]="aegis-diagnostics"
  [hello]="aegis-hello"
  [requestportal]="aegis-requestportal"
  [studio]="aegis-studio"
  [cafeteria]="aegis-cafeteria-services"\n  [publicsite]="aegis-public-site"
)
declare -A DLL=(
  [operations]="Aegis.Operations.dll"
  [configuration]="Aegis.Configuration.Web.dll"
  [diagnostics]="Aegis.Diagnostics.dll"
  [hello]="Aegis.Hello.dll"
  [requestportal]="RequestPortal.Web.dll"
  [studio]="Aegis.Studio.Web.dll"
  [cafeteria]="Aegis.Cafeteria.Services.dll"\n  [publicsite]="server.mjs"
)
declare -A HEALTH=(
  [operations]="http://127.0.0.1:5100/health"
  [configuration]="http://127.0.0.1:5200/health"
  [diagnostics]="http://127.0.0.1:5300/health"
  [hello]="http://127.0.0.1:5400/health"
  [requestportal]="http://127.0.0.1:5401/health"
  [studio]="http://127.0.0.1:5402/health"
  [cafeteria]="http://127.0.0.1:5088/health"\n  [publicsite]="http://127.0.0.1:5403/health"
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
  if [[ "$app" == "publicsite" && ( ! -f "$staged/package.json" || ! -d "$staged/public" ) ]]; then
    echo "ERROR: publicsite staging requires package.json and public/ assets." >&2
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
  for app in publicsite cafeteria studio requestportal hello operations diagnostics configuration; do
    if is_selected "$app"; then
      systemctl stop "${SERVICE[$app]}" || true
    fi
  done
}

start_selected() {
  # Operations provides restricted control-plane bootstrap; Configuration is the ordinary application registration authority.
  for app in operations configuration diagnostics hello requestportal studio cafeteria publicsite; do
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
    env_name="$app"
    [[ "$app" == "publicsite" ]] && env_name="public-site"
    env_file="/etc/aegis/$env_name.env"
    if [[ -f "$env_file" ]]; then
      cp -a "$env_file" "$BACKUP_DIR/etc-aegis/"
    fi
  done
  # An absent optional environment file must not fail a successful backup.
  return 0
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

migrate_common_secrets_env() {
  log "Migrating Common.Secrets environment configuration to provider instances"

  for app in "${SELECTED[@]}"; do
    # Cafeteria uses /etc/aegis-cafeteria-services/environment, not the control-plane env format.
    [[ "$app" == "cafeteria" ]] && continue
    env_file="/etc/aegis/$app.env"
    [[ -f "$env_file" ]] || continue

    if grep -q '^CommonSecrets__Bitwarden\(__\|PasswordManager__\)' "$env_file"; then
      echo "ERROR: $env_file contains legacy Bitwarden Common.Secrets keys." >&2
      echo "Migrate them explicitly to CommonSecrets__Providers__<name>__Type/Settings before deployment." >&2
      return 1
    fi

    if grep -q '^CommonSecrets__OpenBao__' "$env_file"; then
      # Production control-plane services standardize the existing OpenBao provider
      # instance as PrimarySecrets. Values are preserved; only the configuration path changes.
      sed -i 's/^CommonSecrets__OpenBao__/CommonSecrets__Providers__PrimarySecrets__Settings__/' "$env_file"

      if ! grep -q '^CommonSecrets__Providers__PrimarySecrets__Type=' "$env_file"; then
        printf '\nCommonSecrets__Providers__PrimarySecrets__Type=OpenBao\n' >> "$env_file"
      fi
    fi

    # ProviderOrder now references instance names rather than provider types.
    sed -i -E 's/^(CommonSecrets__ProviderOrder__[0-9]+)=OpenBao$/\1=PrimarySecrets/' "$env_file"

    # Common.Security Active Directory moved behind AuthenticationPlugin.
    # Preserve existing production values by migrating the legacy SuiteSecurity path.
    if grep -q '^SuiteSecurity__ActiveDirectory__' "$env_file"; then
      sed -i 's/^SuiteSecurity__ActiveDirectory__/AuthenticationPlugin__Settings__/' "$env_file"
    fi

    if [[ "$app" == "operations" ]]; then
      if grep -q '^SuiteSecurity__Mode=' "$env_file"; then
        sed -i 's/^SuiteSecurity__Mode=.*/SuiteSecurity__Mode=ActiveDirectory/' "$env_file"
      else
        printf '\nSuiteSecurity__Mode=ActiveDirectory\n' >> "$env_file"
      fi
    fi

    if grep -q '^SuiteSecurity__Mode=ActiveDirectory$' "$env_file"; then
      if ! grep -q '^AuthenticationPlugin__Enabled=' "$env_file"; then
        printf '\nAuthenticationPlugin__Enabled=true\n' >> "$env_file"
      fi
      if ! grep -q '^AuthenticationPlugin__AssemblyPath=' "$env_file"; then
        printf 'AuthenticationPlugin__AssemblyPath=plugins/Common.Security.Plugin.ActiveDirectory.dll\n' >> "$env_file"
      fi
    fi

    # Some env files only override ProviderOrder while provider details remain in
    # appsettings. Once the order names PrimarySecrets, ensure the matching provider
    if grep -Eq '^CommonSecrets__ProviderOrder__[0-9]+=PrimarySecrets' "$env_file"; then
      if ! grep -q '^CommonSecrets__Providers__PrimarySecrets__Type=' "$env_file"; then
        printf '\nCommonSecrets__Providers__PrimarySecrets__Type=OpenBao\n' >> "$env_file"
      fi
    fi

    chmod 600 "$env_file"
  done
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
    if [[ "$app" == "cafeteria" ]]; then
      chown -R aegis-cafeteria:aegis-cafeteria "$target"
    elif [[ "$app" == "publicsite" ]]; then
      chown -R aegis-public-site:aegis-public-site "$target"
    else
      chown -R "$OWNER" "$target"
    fi
    if [[ -d "$target/scripts" ]]; then
      find "$target/scripts" -maxdepth 1 -type f \( -name '*.sh' -o -name 'aegis-hello' \) -exec chmod 0755 {} +
    fi
  done
}

health_check() {
  local app="$1"
  local url="${HEALTH[$app]}"
  local attempt
  local -a curl_args=(--fail --silent --show-error --max-time 5)

  if [[ "$app" == "requestportal" ]]; then
    curl_args+=(-H "Host: requestportal.ffpja.org")
  fi

  for ((attempt=1; attempt<=HEALTH_RETRIES; attempt++)); do
    if curl "${curl_args[@]}" "$url" >/dev/null; then
      echo "PASS: $app health $url"
      return 0
    fi
    sleep "$HEALTH_DELAY"
  done
  echo "FAIL: $app health $url" >&2
  echo "--- systemctl status: ${SERVICE[$app]} ---" >&2
  systemctl --no-pager --full status "${SERVICE[$app]}" >&2 || true
  echo "--- recent journal: ${SERVICE[$app]} ---" >&2
  journalctl -u "${SERVICE[$app]}" -n 120 --no-pager >&2 || true
  return 1
}

rollback() {
  echo >&2
  echo "DEPLOYMENT FAILED - ROLLING BACK TO $BACKUP_DIR" >&2
  stop_selected
  for app in "${SELECTED[@]}"; do
    target="${TARGET[$app]}"
    rm -rf "$target"
    if [[ ! -f "$BACKUP_DIR/$app.__absent__" ]]; then
      mkdir -p "$target"
      cp -a "$BACKUP_DIR/$app/." "$target/"
      if [[ "$app" == "cafeteria" ]]; then
        chown -R aegis-cafeteria:aegis-cafeteria "$target"
      elif [[ "$app" == "publicsite" ]]; then
        chown -R aegis-public-site:aegis-public-site "$target"
      else
        chown -R "$OWNER" "$target"
      fi
    fi

    env_name="$app"
    [[ "$app" == "publicsite" ]] && env_name="public-site"
    env_file="/etc/aegis/$env_name.env"
    backup_env="$BACKUP_DIR/etc-aegis/$env_name.env"
    if [[ -f "$backup_env" ]]; then
      cp -a "$backup_env" "$env_file"
      chmod 600 "$env_file"
    fi
  done
  start_selected || true
  echo "Rollback attempted. Inspect systemctl/journalctl before retrying." >&2
}

log "Ensuring persistent control-plane state storage"
mkdir -p /var/lib/aegis/operations/registration
mkdir -p /var/lib/aegis/configuration/contracts/retired
mkdir -p /var/lib/aegis/diagnostics
mkdir -p /var/lib/aegis/hello
mkdir -p /var/lib/aegis/studio
# Only touch persistent state for applications selected in this deployment.
# Recursively chowning live state for unrelated services races with their atomic
# temp-file writes (for example Operations application-logs.json.tmp-*).
for app in "${SELECTED[@]}"; do
  case "$app" in
    operations) chown -R "$OWNER" /var/lib/aegis/operations ;;
    configuration) chown -R "$OWNER" /var/lib/aegis/configuration ;;
    diagnostics) chown -R "$OWNER" /var/lib/aegis/diagnostics ;;
    hello) chown -R "$OWNER" /var/lib/aegis/hello ;;
    studio) chown -R "$OWNER" /var/lib/aegis/studio ;;
  esac
done
chmod 700 /var/lib/aegis/operations/registration /var/lib/aegis/configuration /var/lib/aegis/diagnostics /var/lib/aegis/hello /var/lib/aegis/studio

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

if is_selected studio; then
  log "Ensuring Aegis.Studio Nginx reverse proxy"
  if command -v nginx >/dev/null 2>&1; then
    mkdir -p /etc/nginx/sites-available /etc/nginx/sites-enabled
    studio_nginx_site="/etc/nginx/sites-available/aegis-studio"

    # Create the bootstrap HTTP site only when Studio has no Nginx site yet.
    # Once Certbot has provisioned TLS it owns certificate/redirect directives in
    # this file; normal application deployments must not overwrite them.
    if [[ ! -f "$studio_nginx_site" ]]; then
      cat >"$studio_nginx_site" <<'EOF'
server {
    listen 80;
    listen [::]:80;
    server_name studio.bryanstudiosltd.com;

    location / {
        proxy_pass http://127.0.0.1:5402;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Port $server_port;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
        client_max_body_size 2G;
    }
}
EOF
    else
      log "Preserving existing Aegis.Studio Nginx site (including Certbot-managed TLS)"
    fi

    ln -sfn "$studio_nginx_site" /etc/nginx/sites-enabled/aegis-studio
    nginx -t
    systemctl reload nginx
  else
    echo "ERROR: nginx is not installed; cannot publish studio.bryanstudiosltd.com." >&2
    exit 1
  fi

  if ! grep -q '^Aegis__PublicUrl=' /etc/aegis/studio.env 2>/dev/null; then
    printf '\nAegis__PublicUrl=https://studio.bryanstudiosltd.com\n' >> /etc/aegis/studio.env
  else
    sed -i 's#^Aegis__PublicUrl=.*#Aegis__PublicUrl=https://studio.bryanstudiosltd.com#' /etc/aegis/studio.env
  fi

  # Kratos currently hosts the Studio click-around/pilot deployment. Keep the
  # process in Production hosting mode while explicitly enabling Studio mock auth.
  if grep -q '^MockAuthentication__Enabled=' /etc/aegis/studio.env 2>/dev/null; then
    sed -i 's/^MockAuthentication__Enabled=.*/MockAuthentication__Enabled=true/' /etc/aegis/studio.env
  else
    printf 'MockAuthentication__Enabled=true\n' >> /etc/aegis/studio.env
  fi
  chmod 600 /etc/aegis/studio.env
fi

if is_selected studio; then
  log "Ensuring Aegis.Studio systemd service"
  mkdir -p /etc/aegis
  touch /etc/aegis/studio.env
  chmod 600 /etc/aegis/studio.env
  if [[ ! -f /etc/systemd/system/aegis-studio.service ]]; then
    cat >/etc/systemd/system/aegis-studio.service <<'EOF'
[Unit]
Description=Aegis Studio
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=aegis
Group=aegis
WorkingDirectory=/opt/aegis/studio
ExecStart=/usr/bin/dotnet /opt/aegis/studio/Aegis.Studio.Web.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5402
EnvironmentFile=/etc/aegis/studio.env
Restart=always
RestartSec=5
SyslogIdentifier=aegis-studio

[Install]
WantedBy=multi-user.target
EOF
    systemctl daemon-reload
    systemctl enable aegis-studio.service
  fi

  # Kratos is currently the Studio demo/click-around host. Keep production
  # configuration active, but explicitly opt this deployment into mock auth
  # and demo seeding. The application defaults remain locked down elsewhere.
  mkdir -p /etc/systemd/system/aegis-studio.service.d
  cat >/etc/systemd/system/aegis-studio.service.d/10-kratos-demo.conf <<'EOF'
[Service]
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=MockAuthentication__Enabled=true
Environment=MockAuthentication__AllowOutsideDevelopment=true
Environment=MockAuthentication__UserName=studio.admin
Environment="MockAuthentication__DisplayName=Studio Administrator"
Environment="MockAuthentication__Groups__0=Domain Admins"
Environment="AuthenticationPlugin__Settings__AdministrativeGroups__0=Domain Admins"
Environment=DemoData__Enabled=true
EOF
  systemctl daemon-reload
fi

if is_selected publicsite; then
  log "Preparing Bryan's Public Site runtime"
  NODE_BIN="/opt/nodejs/current/bin/node"
  NPM_BIN="/opt/nodejs/current/bin/npm"
  if [[ ! -x "$NODE_BIN" || ! -x "$NPM_BIN" ]]; then
    echo "ERROR: Node.js 24 is required at /opt/nodejs/current. Install and verify it before deployment." >&2
    exit 1
  fi
  NODE_MAJOR="$("$NODE_BIN" -p 'process.versions.node.split(".")[0]')"
  if [[ "$NODE_MAJOR" -lt 24 ]]; then
    echo "ERROR: Node.js 24 or newer is required; found $("$NODE_BIN" --version)." >&2
    exit 1
  fi
  if ! getent passwd aegis-public-site >/dev/null; then
    useradd --system --user-group --home-dir /var/lib/aegis-public-site --shell /usr/sbin/nologin aegis-public-site
  fi
  install -d -o aegis-public-site -g aegis-public-site -m 0750 /var/lib/aegis-public-site
  install -d -o root -g aegis-public-site -m 0750 /etc/aegis
  if [[ ! -f /etc/aegis/public-site.env ]]; then
    cat >/etc/aegis/public-site.env <<EOF
NODE_ENV=production
PUBLIC_DEMO_MODE=false
PORT=5403
HOST=127.0.0.1
SESSION_SECRET=$("$NODE_BIN" -e "process.stdout.write(require('node:crypto').randomBytes(48).toString('hex'))")
PUBLIC_API_PROVISIONING_TOKEN=$("$NODE_BIN" -e "process.stdout.write(require('node:crypto').randomBytes(48).toString('hex'))")
INTAKE_DB_PATH=/var/lib/aegis-public-site/intake.sqlite
PUBLIC_API_KEYS_JSON=[]
CLIENT_BINDINGS_JSON={}
EOF
  else
    for required in "NODE_ENV=production" "PUBLIC_DEMO_MODE=false" "PORT=5403" "HOST=127.0.0.1" "INTAKE_DB_PATH=/var/lib/aegis-public-site/intake.sqlite"; do
      if ! grep -Fxq "$required" /etc/aegis/public-site.env; then printf '%s\n' "$required" >>/etc/aegis/public-site.env; fi
    done
  fi
  chown root:aegis-public-site /etc/aegis/public-site.env
  chmod 0640 /etc/aegis/public-site.env

  cat >/etc/systemd/system/aegis-public-site.service <<'EOF'
[Unit]
Description=Bryan's Public Site API
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=aegis-public-site
Group=aegis-public-site
WorkingDirectory=/opt/aegis/public-site
EnvironmentFile=/etc/aegis/public-site.env
Environment="PATH=/opt/nodejs/current/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/bin"
ExecStart=/opt/nodejs/current/bin/node /opt/aegis/public-site/server.mjs
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/aegis-public-site
SyslogIdentifier=aegis-public-site

[Install]
WantedBy=multi-user.target
EOF
  systemctl daemon-reload
  systemctl enable aegis-public-site.service

  if ! command -v nginx >/dev/null 2>&1; then
    echo "ERROR: nginx is required to publish api.bryanstudiosltd.com." >&2
    exit 1
  fi
  install -d /etc/nginx/sites-available /etc/nginx/sites-enabled
  publicsite_nginx_site="/etc/nginx/sites-available/aegis-public-site"
  if [[ ! -f "$publicsite_nginx_site" ]]; then
    cat >"$publicsite_nginx_site" <<'EOF'
server {
    listen 80;
    listen [::]:80;
    server_name api.bryanstudiosltd.com;
    client_max_body_size 32m;

    location / {
        proxy_pass http://127.0.0.1:5403;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF
  else
    log "Preserving existing Public Site Nginx configuration"
  fi
  ln -sfn "$publicsite_nginx_site" /etc/nginx/sites-enabled/aegis-public-site
  nginx -t
  systemctl reload nginx
fi

log "Backing up current deployment to $BACKUP_DIR"
backup_current

# The backup is complete. Arm rollback before mutating persistent environment
# or registration state so a migration failure cannot leave a partial deployment.
trap 'rc=$?; if [[ $rc -ne 0 ]]; then rollback; fi; exit $rc' EXIT

if is_selected publicsite && [[ ${#SELECTED[@]} -eq 1 ]]; then
  log "Skipping unrelated application environment migrations for Public Site-only deployment"
else
  migrate_common_secrets_env
  cleanup_legacy_registration
fi

log "Stopping selected services"
stop_selected

log "Promoting staged publish outputs"
deploy_payload

if is_selected publicsite; then
  log "Installing Public Site production dependencies"
  if [[ -f /opt/aegis/public-site/package-lock.json ]]; then
    runuser -u aegis-public-site -- env PATH="/opt/nodejs/current/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/bin" npm --prefix /opt/aegis/public-site ci --omit=dev --no-audit --no-fund
  else
    runuser -u aegis-public-site -- env PATH="/opt/nodejs/current/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/bin" npm --prefix /opt/aegis/public-site install --omit=dev --no-audit --no-fund
  fi
fi

log "Starting selected services"
start_selected

log "Waiting 5 seconds for services to initialize before validation"
sleep 5

log "Validating services"
for app in "${SELECTED[@]}"; do
  service="${SERVICE[$app]}"
  if ! systemctl is-active --quiet "$service"; then
    echo "FAIL: $app service $service is not active" >&2
    systemctl --no-pager --full status "$service" >&2 || true
    journalctl -u "$service" -n 80 --no-pager >&2 || true
    false
  fi

  health_check "$app"
done

trap - EXIT

log "Deployment succeeded"
echo "Backup: $BACKUP_DIR"
echo "Preserved: /etc/aegis/*.env and /etc/aegis/secrets"
for app in "${SELECTED[@]}"; do
  echo "  ${SERVICE[$app]} -> ${HEALTH[$app]}"
done
