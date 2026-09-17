# RequestPortal Production Commissioning Runbook

**Target host:** KRATOS  
**Application:** RequestPortal  
**Kestrel:** `127.0.0.1:5401`  
**Public names:** `https://requestportal.ffpja.org`, `https://relief.ffpja.org`  
**Configuration:** `https://config.ffpja.org`  
**Diagnostics:** `https://diagnostics.ffpja.org`

> Production rule: application values may be supplied by environment/configuration, but secrets must resolve through Common.Secrets. OpenBao is a provider behind Common.Secrets; RequestPortal must not depend on OpenBao APIs directly.

## 1. Required server paths

```bash
sudo mkdir -p /opt/aegis/requestportal
sudo mkdir -p /var/lib/aegis/requestportal/{storage,keys,messaging}
sudo mkdir -p /etc/aegis/secrets/requestportal
sudo chown -R aegis:aegis /opt/aegis/requestportal /var/lib/aegis/requestportal
sudo chmod 0750 /opt/aegis/requestportal /var/lib/aegis/requestportal
```

The systemd unit is `aegis-requestportal.service` and uses:

```text
WorkingDirectory=/opt/aegis/requestportal
ExecStart=/usr/bin/dotnet /opt/aegis/requestportal/RequestPortal.Web.dll
EnvironmentFile=/etc/aegis/requestportal.env
```

## 2. Publish and deploy the application

On the development workstation:

```powershell
cd D:\Projects\RequestPortal
git pull --ff-only origin main

dotnet publish .\RequestPortal.Web\RequestPortal.Web.csproj `
    -c Release `
    -o .\publish-test
```

Verify the output includes `RequestPortal.Web.dll`, `appsettings.json`, and `appsettings.Production.json`.

When using Aegis.Operations deployment:

```powershell
cd D:\Projects\Aegis.Operations
.\scripts\Deploy-Kratos.ps1 -UserName FWright -Applications requestportal -SkipAuthoritativeBuild
```

If commissioning is incomplete, the deployment health check may fail and roll back. That is acceptable during commissioning only if the new RequestPortal build successfully publishes its complete commissioning state to Aegis.Configuration before aborting.

## 3. Production environment file

Edit:

```bash
sudo nano /etc/aegis/requestportal.env
```

Recommended production baseline:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5401

Aegis__Configuration__Url=https://config.ffpja.org
Aegis__Diagnostics__Url=https://diagnostics.ffpja.org
AEGIS_CONFIGURATION_REGISTRATION_KEY=<registration-bootstrap-key>

CommonSecrets__Mode=Production
CommonSecrets__WorkloadName=RequestPortal
CommonSecrets__EnvironmentName=Production
CommonSecrets__OpenBao__Enabled=true
CommonSecrets__OpenBao__Address=https://openbao.ffpja.org:8200
CommonSecrets__OpenBao__RoleId=<requestportal-role-id>

Storage__Local__RootPath=/var/lib/aegis/requestportal/storage
Security__DataProtectionKeysPath=/var/lib/aegis/requestportal/keys
Messaging__DurableQueuePath=/var/lib/aegis/requestportal/messaging

AllowedHosts=requestportal.ffpja.org;relief.ffpja.org
Site__Id=FFPJA-PROD

Authentication__ActiveDirectory__Domain=fftpj.local
Authentication__ActiveDirectory__ReviewerGroups__0=Domain Admins
Authentication__ActiveDirectory__AdministratorGroups__0=Domain Admins

Uploads__MalwareScanning__Enabled=true
Uploads__MalwareScanning__Host=127.0.0.1
Uploads__MalwareScanning__Port=3310

Notifications__Enabled=true
Notifications__SmtpHost=10.10.0.8
Notifications__SmtpPort=25
Notifications__EnableSsl=false
Notifications__FromAddress=svcNetMon@foodforthepoorja.org
Notifications__FromName="FFP Request Portal"
Notifications__UserName=svcNetMon@foodforthepoorja.org
Notifications__PasswordSecretName=Notifications:SmtpPassword
```

Do not place OpenBao SecretId, SMTP password, Microsoft Graph client secret, Slack token, WhatsApp token, or OIDC client secret directly in this file.

## 4. OpenBao AppRole

Create a RequestPortal policy, for example `/etc/openbao/requestportal-policy.hcl` on the OpenBao administration host. The policy file is an administrative input; RequestPortal does not read this HCL file.

The policy should grant only the RequestPortal secret subtree and any explicitly required shared registration secret. Example shape:

```hcl
path "secret/data/requestportal/*" {
  capabilities = ["read"]
}

path "secret/metadata/requestportal/*" {
  capabilities = ["read"]
}
```

Load the policy and create the AppRole using the OpenBao CLI appropriate to the server configuration. Record the RoleId. Generate a fresh SecretId for bootstrap and do not commit either value.

## 5. Secret Zero bootstrap

RequestPortal's production bootstrap should use a protected SecretId source, not a permanent plaintext appsetting. Store the one-time/current bootstrap value in:

```text
/etc/aegis/secrets/requestportal/openbao-secret-id
```

Example:

```bash
sudo install -o root -g aegis -m 0640 /dev/null /etc/aegis/secrets/requestportal/openbao-secret-id
sudo nano /etc/aegis/secrets/requestportal/openbao-secret-id
sudo chmod 0640 /etc/aegis/secrets/requestportal/openbao-secret-id
```

If the deployed Common.Secrets version uses a runtime secret file under `/run/secrets`, stage the protected file into the configured runtime location via systemd `ExecStartPre` or equivalent. Do not invent legacy SecretId settings that the current Common.Secrets version no longer consumes. Always confirm the active production options in `appsettings.Production.json` before adding bootstrap keys.

## 6. RequestPortal secrets in OpenBao

Populate only the secrets actually required by enabled features. Typical RequestPortal paths are:

```text
secret/requestportal/ConnectionStrings/RequestPortal
secret/requestportal/Authentication/StaffOidc/ClientSecret
secret/requestportal/Notifications/SmtpPassword
secret/requestportal/messaging/msgraph/client-secret
secret/requestportal/messaging/msgraph/teams/delegated-access-token
secret/requestportal/messaging/slack/bot-token
secret/requestportal/messaging/whatsapp/api-token
```

Example:

```bash
bao kv put secret/requestportal/Notifications/SmtpPassword value='<smtp-password>'
bao kv put secret/requestportal/messaging/msgraph/client-secret value='<graph-client-secret>'
bao kv put secret/requestportal/messaging/slack/bot-token value='<slack-bot-token>'
```

Never paste production secret values into source control or this runbook.

## 7. PostgreSQL on Axios

The PostgreSQL connection string must be stored through Common.Secrets as `ConnectionStrings:RequestPortal`.

Verify connectivity from Kratos before blaming RequestPortal:

```bash
getent hosts axios.fftpj.local
nc -vz axios.fftpj.local 5432
```

Database migrations are applied by the production startup path only after commissioning validation passes.

## 8. ClamAV

Install and enable ClamAV on Kratos:

```bash
sudo apt update
sudo apt install -y clamav clamav-daemon
sudo systemctl enable --now clamav-daemon
```

For RequestPortal TCP scanning, `/etc/clamav/clamd.conf` must expose localhost TCP 3310:

```text
TCPSocket 3310
TCPAddr 127.0.0.1
```

After changing ClamAV configuration:

```bash
sudo systemctl restart clamav-daemon
sudo ss -ltnp | grep 3310
```

Expected: `127.0.0.1:3310` is listening. A Unix socket alone is not sufficient when RequestPortal is configured for TCP ClamAV access.

## 9. Staff OIDC

Production requires staff OIDC commissioning.

Use the existing Microsoft Entra app registration where appropriate. Register both RequestPortal hostnames as Web redirect URIs:

```text
https://requestportal.ffpja.org/signin-ffp-staff
https://relief.ffpja.org/signin-ffp-staff
```

Environment values:

```text
Authentication__StaffOidc__Enabled=true
Authentication__StaffOidc__Authority=https://login.microsoftonline.com/<tenant-id>/v2.0
Authentication__StaffOidc__ClientId=<application-client-id>
```

Store the client secret through Common.Secrets/OpenBao as `Authentication:StaffOidc:ClientSecret`.

Do not reuse the Microsoft Graph messaging client secret as Staff OIDC credentials unless the Entra registration was explicitly designed and permissioned for both purposes.

## 10. Representative external identity provider

Production currently requires at least one commissioned external representative identity provider. Supported production validation checks Google, Microsoft consumer OAuth, or Facebook settings. Configure only the provider intended for the deployment and keep its client secret in Common.Secrets.

Aegis.Configuration should show `RP-PROD-OAUTH-001` until this requirement is satisfied.

## 11. Microsoft Graph Email and Teams

Current Common.Messaging channels use these non-secret settings:

```text
Messaging__MicrosoftGraph__TenantId=<tenant-id>
Messaging__MicrosoftGraph__ClientId=<client-id>
Messaging__MicrosoftGraph__ClientSecretName=messaging/msgraph/client-secret

Messaging__MicrosoftGraph__Email__Enabled=true
Messaging__MicrosoftGraph__Email__SenderUpn=svcNetMon@foodforthepoorja.org

Messaging__MicrosoftGraph__Teams__Enabled=true
Messaging__MicrosoftGraph__Teams__SenderUpn=svcNetMon@foodforthepoorja.org
Messaging__MicrosoftGraph__Teams__DelegatedAccessTokenSecretName=messaging/msgraph/teams/delegated-access-token
```

Graph Email is app-only. Graph Teams DM uses delegated service-user token material and validates the configured sender identity. Keep the two channels separate.

## 12. Slack and WhatsApp

Slack bot token default secret:

```text
messaging/slack/bot-token
```

WhatsApp API token default secret:

```text
messaging/whatsapp/api-token
```

Enable these channels only after their endpoint/channel-specific configuration is confirmed. Do not copy old Aegis key names blindly when the Common.Messaging contract has changed.

## 13. Nginx for both RequestPortal names

Nginx must proxy both names to the same Kestrel process:

```nginx
server {
    listen 80;
    server_name requestportal.ffpja.org relief.ffpja.org;

    client_max_body_size 30M;

    location / {
        proxy_pass http://127.0.0.1:5401;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

For HTTPS termination on Kratos, the certificate must cover both names. If TLS terminates elsewhere, that layer must also recognize both names.

Validate and reload:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

A `502 Bad Gateway` means nginx accepted the host but Kestrel is not listening on 5401; inspect RequestPortal startup instead of changing the nginx port.

## 14. Commissioning through Aegis.Configuration

RequestPortal production startup intentionally validates all commissioning requirements together. The current validator includes:

- `RP-CONFIG-DB-001` PostgreSQL connection
- `RP-PROD-STORAGE-001` durable storage
- `RP-PROD-DP-001` Data Protection key persistence
- `RP-PROD-SECRETS-001` OpenBao address
- `RP-PROD-SECRETS-002` OpenBao RoleId
- `RP-PROD-HOSTS-001` restricted AllowedHosts
- `RP-PROD-UPLOAD-001` malware scanning
- `RP-PROD-STAFF-GROUPS-001` staff groups
- `RP-PROD-OIDC-001` staff OIDC
- `RP-PROD-OAUTH-001` external representative identity provider
- `RP-PROD-SMTP-001` SMTP notifications
- `RP-PROD-MESSAGING-001` durable messaging path
- `RP-PROD-SITE-001` site identity
- `RP-PROD-FFPMANAGER-001` conditional FFP Manager integration

The current startup path collects the complete list, attempts bootstrap publication to Aegis.Configuration, then aborts if unresolved requirements remain. Configuration should therefore be the primary commissioning checklist rather than forcing the operator to discover one gate at a time from `journalctl`.

If registration itself fails, inspect `RP-CONFIG-REG-001` and verify the Configuration URL, registration credential, network path, and Common.Secrets availability.

## 15. Starting and diagnosing RequestPortal

```bash
sudo systemctl stop aegis-requestportal
sudo systemctl reset-failed aegis-requestportal
sudo systemctl start aegis-requestportal
sleep 3
sudo systemctl status aegis-requestportal --no-pager -l
```

Latest logs:

```bash
sudo journalctl -u aegis-requestportal -n 150 --no-pager -l
```

Listening socket:

```bash
sudo ss -ltnp | grep 5401
```

Direct health check:

```bash
curl -i http://127.0.0.1:5401/health
```

Do not troubleshoot nginx until 5401 is listening.

## 16. Validation after startup

```bash
curl -I -H 'Host: requestportal.ffpja.org' http://127.0.0.1
curl -I -H 'Host: relief.ffpja.org' http://127.0.0.1
```

Then validate HTTPS externally for both names.

In Aegis.Configuration verify RequestPortal shows:

- registered application identity
- version/environment/host
- complete commissioning requirement list
- PASS/Missing state per requirement
- prominent commissioning issue summary when incomplete

In Aegis.Diagnostics verify RequestPortal events are correlated from Configuration registration rather than a second application registry.

## 17. Acceptance checklist

- RequestPortal systemd unit is active and stable.
- `127.0.0.1:5401` is listening.
- `/health` responds locally.
- Both public hostnames route correctly.
- Production AllowedHosts contains both names.
- OpenBao AppRole is least privilege.
- Secret Zero is not committed or left in ordinary appsettings.
- PostgreSQL connection resolves from Common.Secrets.
- ClamAV listens on localhost TCP 3310 and upload scanning is enabled.
- Staff OIDC login succeeds.
- At least one representative external identity provider is commissioned.
- SMTP test succeeds.
- Graph Email test succeeds when enabled.
- Graph Teams DM test succeeds when enabled.
- Configuration shows all requirements truthfully.
- Diagnostics receives operational events without duplicate application registration.
- Reboot test restores RequestPortal automatically.

## 18. Failure rule

Never make a production gate report success merely to allow deployment to continue. During commissioning, fix the requirement or intentionally change the product requirement in source. Configuration must show the same truth the application uses to decide whether production startup is safe.
