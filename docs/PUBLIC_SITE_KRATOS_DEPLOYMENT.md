# Bryan's Public Site: Mac-to-Kratos deployment

The Public Site deploys from the Mac. The shared Operations deployer stages its source files, uploads a deployment bundle, and asks Kratos to install the service and validate local health. The .NET applications are not built or restarted during a Public Site-only deployment.

## Required checkouts

Keep sibling repositories under the same Projects directory:

- `Aegis.Operations`
- `Aegis.Studio`

Both the deployment tooling and Public Site implementation are merged to `main`. Update both local checkouts:

```powershell
git -C "$HOME/Projects/Aegis.Operations" switch main
git -C "$HOME/Projects/Aegis.Operations" pull --ff-only
git -C "$HOME/Projects/Aegis.Studio" switch main
git -C "$HOME/Projects/Aegis.Studio" pull --ff-only
```

Use the appropriate ProjectsRoot if your repositories are in another directory.

## Deploy from the Mac

From the Operations checkout:

```powershell
pwsh ./scripts/Build-Deploy-PublicSite-Kratos.ps1
```

The script requires Node.js 24+ and npm on the Mac. It installs the Public Site npm dependencies, runs the full Node test suite locally, then deploys only `publicsite` to Kratos. A nonzero test result stops deployment. Native OpenSSH key authentication must work for `fwright@kratos`; pass `-IdentityFile /path/to/key` if the key is not selected automatically.

Optional parameters include `-ProjectsRoot`, `-HostName`, `-UserName`, and `-KeepLocalBundle` / `-KeepRemoteBundle` for diagnosing a failed transfer.

## What the Kratos deployer manages

- Runtime: `/opt/nodejs/current` (Node 24); Debian's system Node is not changed.
- Application: `/opt/aegis/public-site`.
- Persistent DB/media store: `/var/lib/aegis-public-site`.
- Environment and generated server secrets: `/etc/aegis/public-site.env`.
- Service: `aegis-public-site.service`, with a persistent Node path and `HOST=127.0.0.1`, `PORT=5403`.
- Nginx site: `api.bryanstudiosltd.com` redirects HTTP to HTTPS and proxies to `127.0.0.1:5403`. It uses the dedicated Certbot certificate at `/etc/letsencrypt/live/api.bryanstudiosltd.com/`. Existing API Nginx configuration is preserved so Certbot-managed TLS directives remain intact.
- Health validation: `http://127.0.0.1:5403/health`; a failed validation triggers the deployer's backup and rollback path.

The deployer creates `/etc/aegis/public-site.env` on first install with random session and provisioning secrets, production mode, demo login disabled, and a separate persistent data path. Keep this file root-owned and protected.

## Obtain the API certificate on Kratos

Before the first deployment, create an HTTP Nginx site for the API so Certbot can validate the hostname. On Kratos:

```bash
sudo tee /etc/nginx/sites-available/bryans-api >/dev/null <<'EOF'
server {
    listen 80;
    listen [::]:80;
    server_name api.bryanstudiosltd.com;
    client_max_body_size 128m;

    location / {
        proxy_pass http://127.0.0.1:5403;
        proxy_read_timeout 150s;
        proxy_send_timeout 150s;
        client_body_timeout 150s;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOF
sudo ln -sfn /etc/nginx/sites-available/bryans-api /etc/nginx/sites-enabled/bryans-api
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d api.bryanstudiosltd.com
```

If Certbot offers to redirect HTTP to HTTPS, choose the redirect option. Verify the issued certificate before deploying:

```bash
sudo openssl x509 -in /etc/letsencrypt/live/api.bryanstudiosltd.com/fullchain.pem -noout -ext subjectAltName
sudo test -r /etc/letsencrypt/live/api.bryanstudiosltd.com/privkey.pem
```

The SAN output must include `DNS:api.bryanstudiosltd.com`. The deployer checks this certificate before creating or enabling the service, then preserves the Certbot-managed Nginx configuration.

From the Mac, verify both DNS paths and HTTPS:

```bash
dig +short api.bryanstudiosltd.com A
curl -i https://api.bryanstudiosltd.com/health
curl -i https://api.bryanstudiosltd.com/api/public-site/v1/openapi.json
```

Do not forward port 5403 from pfSense; Nginx is the public entry point.

This deploys the current booking and WorkDay API gateway implementation for controlled testing. Studio's catalog and availability feed drive website bookings; WorkDay mobile calls only `https://api.bryanstudiosltd.com/api/v1` and the gateway forwards its allowlisted requests to Studio over private TLS. The Studio Client ID proof workflow is implemented. Real client Gallery use still requires configuring and validating the production identity provider sign-in before clients redeem proofs.

## WorkDay API gateway

The Public Site now provides the WorkDay mobile API gateway. Its service environment defaults the Studio upstream to `https://studio.bryanstudiosltd.com` over the local Nginx listener at `127.0.0.1`. Keep the API-to-Studio TLS certificate validation enabled. The gateway forwards only WorkDay mobile routes; Studio remains authoritative. Deployment preserves existing Nginx TLS settings.

For uploads, set the API Nginx site to allow up to 128 MB request bodies and wait up to 150 seconds for upload/proxy activity. If the site was already configured, edit its existing `location /` block to include `client_max_body_size 128m`, `client_body_timeout 150s`, `proxy_read_timeout 150s` and `proxy_send_timeout 150s`, then run `sudo nginx -t && sudo systemctl reload nginx`. Existing environment files are preserved; the deployer appends missing WorkDay upstream defaults without replacing values already present.

The WorkDay mobile app connects only to `https://api.bryanstudiosltd.com/api/v1`. See `Aegis.Studio/docs/public-site-v1/WORKDAY-API-GATEWAY.md` for the complete path and authentication contract.
