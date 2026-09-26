# Aegis Debian 13 Production Deployment Runbook

**Target:** KRATOS (`10.10.0.207`) — FFP Jamaica  
**Services:** Aegis.Operations, Aegis.Configuration, Aegis.Diagnostics  
**Production URLs:** `https://operations.ffpja.org`, `https://config.ffpja.org`, `https://diagnostics.ffpja.org`

> **Security:** Never commit the real `SuiteSecurity__SigningKey`, passwords, private keys, or other production secrets. Values shown as `<...>` are placeholders.

## 1. Final architecture

| Service | Kestrel | Production URL | Startup DLL |
|---|---|---|---|
| Operations | `127.0.0.1:5100` | `https://operations.ffpja.org` | `Aegis.Operations.dll` |
| Configuration | `127.0.0.1:5200` | `https://config.ffpja.org` | `Aegis.Configuration.Web.dll` |
| Diagnostics | `127.0.0.1:5300` | `https://diagnostics.ffpja.org` | `Aegis.Diagnostics.dll` |

Traffic is Client → Nginx :443 → localhost Kestrel. pfSense remains router/firewall and ACME certificate source.

## 2. Prerequisites

- Debian 13 VM, hostname `kratos`, static IP `10.10.0.207` on OFFICE/VLAN10.
- Internal DNS records for all three `*.ffpja.org` names pointing to `10.10.0.207`.
- Kratos resolves `JMAD01.fftpj.local` (`10.10.0.221`) and `JMAD02.fftpj.local` (`10.10.0.222`).
- TCP 443 from clients to Kratos; TCP 636 from Kratos to both DCs.
- Published .NET output for all three applications.
- pfSense ACME `adminCertificates` certificate set.

## 3. Base server

```bash
sudo apt update
sudo apt full-upgrade -y
sudo apt install -y nginx openssh-server rsync ca-certificates openssl netcat-openbsd
```

Install the .NET 10 ASP.NET Core runtime using Microsoft's current Debian 13 packages, then verify `dotnet --list-runtimes`.

```bash
sudo adduser --system --group --home /nonexistent --no-create-home aegis
sudo mkdir -p /opt/aegis/{operations,configuration,diagnostics}
sudo mkdir -p /var/log/aegis /var/lib/aegis /etc/aegis
sudo chown -R aegis:aegis /opt/aegis /var/log/aegis /var/lib/aegis
```

## 4. Deploy published applications

Copy publish output to `/tmp/aegis-*`, then:

```bash
sudo rsync -a --delete /tmp/aegis-operations/ /opt/aegis/operations/
sudo rsync -a --delete /tmp/aegis-configuration/ /opt/aegis/configuration/
sudo rsync -a --delete /tmp/aegis-diagnostics/ /opt/aegis/diagnostics/
sudo chown -R aegis:aegis /opt/aegis
```

Verify the three startup DLLs listed in Section 1 exist.

## 5. Production environment

Create `/etc/aegis/operations.env`, `/etc/aegis/configuration.env`, and `/etc/aegis/diagnostics.env`, root-owned and mode 600. All three must use the **same persistent signing key**.

```text
SuiteSecurity__SigningKey=<SAME-PERSISTENT-SECRET-ON-ALL-THREE>
SuiteSecurity__Mode=ActiveDirectory
AuthenticationPlugin__Settings__Domain=fftpj.local
AuthenticationPlugin__Settings__SearchBase=DC=fftpj,DC=local
AuthenticationPlugin__Settings__Servers__0=JMAD01.fftpj.local
AuthenticationPlugin__Settings__Servers__1=JMAD02.fftpj.local
AuthenticationPlugin__Settings__Port=636
```

Operations additionally requires:

```text
Operations__ConfigurationUrl=https://config.ffpja.org
Operations__DiagnosticsUrl=https://diagnostics.ffpja.org
```

Common.Secrets environment overrides must use the provider-instance contract. For a
production OpenBao provider named `PrimarySecrets`, use keys such as:

```text
CommonSecrets__ProviderOrder__0=PrimarySecrets
CommonSecrets__Providers__PrimarySecrets__Type=OpenBao
CommonSecrets__Providers__PrimarySecrets__Settings__Enabled=true
CommonSecrets__Providers__PrimarySecrets__Settings__RequireHttps=true
CommonSecrets__Providers__PrimarySecrets__Settings__Address=https://openbao.ffpja.org
```

Do not use legacy keys such as `CommonSecrets__OpenBao__Address`. The bootstrap
credential itself remains external; for the current AppRole deployment
`OPENBAO_SECRET_ID` remains an environment secret and is not moved under
`CommonSecrets__Providers__...__Settings`.

Remove temporary `SuiteSecurity__Mock__*` settings after AD validation.

## 6. systemd

Operations uses `/usr/bin/dotnet /opt/aegis/operations/Aegis.Operations.dll`, `ASPNETCORE_URLS=http://127.0.0.1:5100`, and `EnvironmentFile=/etc/aegis/operations.env`.

Configuration uses `/usr/bin/dotnet /opt/aegis/configuration/Aegis.Configuration.Web.dll`, port 5200, and `/etc/aegis/configuration.env`.

Diagnostics uses `/usr/bin/dotnet /opt/aegis/diagnostics/Aegis.Diagnostics.dll`, port 5300, and `/etc/aegis/diagnostics.env`.

Each unit runs as `User=aegis`, `Group=aegis`, uses the corresponding `/opt/aegis/...` working directory, sets `ASPNETCORE_ENVIRONMENT=Production`, and uses `Restart=on-failure`.

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now aegis-operations aegis-configuration aegis-diagnostics nginx
```

## 7. pfSense ACME propagation

Create `acmeuser`, install pfSense's `/root/.ssh/id_rsa.pub` in its `authorized_keys`, and make `/etc/ssl/ffpja.org` writable for the certificate deployment account. Never copy the pfSense private key.

Permit only the required Nginx restart via `/etc/sudoers.d/acme-nginx`:

```text
acmeuser ALL=(root) NOPASSWD: /usr/bin/systemctl restart nginx
```

Add to pfSense `acme_cert_propgate.sh`:

```sh
deploy_kratos()
{
    scp -i "$RSA_KEY" \
        "$CERT_DIR"/adminCertificates.* \
        acmeuser@10.10.0.207:/etc/ssl/ffpja.org/. || return 1

    ssh -i "$RSA_KEY" \
        acmeuser@10.10.0.207 \
        'sudo /usr/bin/systemctl restart nginx' || return 1

    return 0
}

run_step "Aegis - Kratos" "10.10.0.207" deploy_kratos
```

## 8. Nginx HTTPS

Each site has an HTTP redirect block and an HTTPS proxy block. Example for Configuration:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name config.ffpja.org;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name config.ffpja.org;

    ssl_certificate     /etc/ssl/ffpja.org/adminCertificates.fullchain;
    ssl_certificate_key /etc/ssl/ffpja.org/adminCertificates.key;
    ssl_protocols TLSv1.2 TLSv1.3;

    location / {
        proxy_pass http://127.0.0.1:5200;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

Use the same structure for Operations → port 5100 and Diagnostics → port 5300. Enable the sites, then:

```bash
sudo nginx -t
sudo systemctl reload nginx
sudo ss -lntp | grep nginx
```

## 9. HTTPS validation

```bash
curl -v --resolve config.ffpja.org:443:127.0.0.1 https://config.ffpja.org/
curl -v --resolve diagnostics.ffpja.org:443:127.0.0.1 https://diagnostics.ffpja.org/
curl -v --resolve operations.ffpja.org:443:127.0.0.1 https://operations.ffpja.org/
```

Expected: `*.ffpja.org` verifies successfully and unauthenticated requests return 302 to `/login`.

## 10. AD CA trust and LDAPS

The DC certificates are issued by `fftpj-PKI-CA`. Export only the CA public certificate from Windows and install it on Kratos:

```bash
sudo cp /tmp/fftpj-PKI-CA.cer /usr/local/share/ca-certificates/fftpj-PKI-CA.crt
sudo update-ca-certificates
```

If the `.cer` is DER, convert it first with `openssl x509 -inform DER`.

Validate:

```bash
openssl s_client -connect JMAD01.fftpj.local:636 -servername JMAD01.fftpj.local </dev/null 2>/dev/null | grep "Verify return code"
openssl s_client -connect JMAD02.fftpj.local:636 -servername JMAD02.fftpj.local </dev/null 2>/dev/null | grep "Verify return code"
```

Required result: `Verify return code: 0 (ok)`.

## 11. Active Directory authentication

Restart all applications after the AD settings are installed:

```bash
sudo systemctl restart aegis-configuration aegis-diagnostics aegis-operations
```

Test all three HTTPS login pages using an AD account. For failures, inspect the corresponding `journalctl -u aegis-*` log. If TCP 636 is open but the application reports `LDAP server is unavailable`, verify the DC certificate chain first.

## 12. Operations integration

Do not leave Operations on development defaults `http://localhost:5101` / `http://localhost:5102`. Use the production URL overrides in Section 5.

```bash
curl -i https://config.ffpja.org/health
curl -i https://diagnostics.ffpja.org/health
```

Operations should report both services Available. Validate secure handoff from Operations to Configuration and Diagnostics; the three applications must share the same signing key.

## 13. Acceptance checklist

- All DNS names resolve to `10.10.0.207` internally.
- Aegis services and Nginx are active and enabled.
- Kestrel ports 5100/5200/5300 bind to localhost only.
- HTTP redirects to HTTPS and the wildcard certificate validates.
- pfSense can propagate `adminCertificates.*` and restart Nginx.
- Both DCs are reachable on 636 and their certificates validate.
- All three apps accept AD credentials.
- Operations reports Configuration and Diagnostics Available.
- Secure handoff is tested.
- No Mock password or production secret is committed to Git.

## 14. Routine operations

```bash
systemctl status aegis-operations aegis-configuration aegis-diagnostics nginx
journalctl -u aegis-operations -f
journalctl -u aegis-configuration -f
journalctl -u aegis-diagnostics -f
sudo nginx -t && sudo systemctl reload nginx
```

## 15. Upgrade procedure

Stop only the affected service, rsync the new publish output into `/opt/aegis/<service>`, restore `aegis:aegis` ownership, start the service, inspect its journal, and test through HTTPS. Production settings remain in `/etc/aegis/*.env` and therefore survive publish replacement.

## 16. Troubleshooting quick reference

| Symptom | First check |
|---|---|
| Login form clears / returns to login | Confirm HTTPS; production session cookies are Secure. |
| LDAP server unavailable | TCP 636, LDAPS certificate trust, `fftpj-PKI-CA`. |
| Operations says services unavailable | Production URL overrides and `/health`. |
| 502 Bad Gateway | Corresponding systemd service and localhost port. |
| Nginx reload fails | `sudo nginx -t`, certificate/key paths and permissions. |
| Certificate warning | Wildcard SAN, chain, dates, DNS and ACME propagation. |

## 17. Future hardening

- Move the suite signing key and other secrets from protected environment files into OpenBao using the agreed bootstrap/Secret Zero design.
- Finalize AD administrative-group mapping and least privilege.
- Add health monitoring for all three URLs.
- Perform a controlled reboot acceptance test.
- Automate deployment/CI after the manual runbook remains stable.
