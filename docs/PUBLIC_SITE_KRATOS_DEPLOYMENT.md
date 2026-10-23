# Bryan's Public Site: Mac-to-Kratos deployment

The Public Site deploys from the Mac. The shared Operations deployer stages its source files, uploads a deployment bundle, and asks Kratos to install the service and validate local health. The .NET applications are not built or restarted during a Public Site-only deployment.

## Required checkouts

Keep sibling repositories under the same Projects directory:

- `Aegis.Operations`
- `Aegis.Studio`

Check out the reviewed deployment-script branch in Operations and the Public Site implementation branch in Studio:

```powershell
git -C "$HOME/Projects/Aegis.Operations" fetch origin
git -C "$HOME/Projects/Aegis.Operations" switch feature/deploy-public-site-kratos-20261023
git -C "$HOME/Projects/Aegis.Studio" fetch origin
git -C "$HOME/Projects/Aegis.Studio" switch feature/reference-api-implementation-20261023
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
- Nginx HTTP site: `api.bryanstudiosltd.com` -> `127.0.0.1:5403`. Existing Nginx configuration is preserved if the Public Site site file already exists.
- Health validation: `http://127.0.0.1:5403/health`; a failed validation triggers the deployer's backup and rollback path.

The deployer creates `/etc/aegis/public-site.env` on first install with random session and provisioning secrets, production mode, demo login disabled, and a separate persistent data path. Keep this file root-owned and protected.

## HTTPS and post-deploy checks

After the first successful deployment, on Kratos run:

```bash
sudo certbot --nginx -d api.bryanstudiosltd.com
sudo certbot renew --dry-run
```

From the Mac, verify both DNS paths and HTTPS:

```bash
dig +short api.bryanstudiosltd.com A
curl -i https://api.bryanstudiosltd.com/health
curl -i https://api.bryanstudiosltd.com/api/public-site/v1/openapi.json
```

Do not forward port 5403 from pfSense; Nginx is the public entry point.

This deploys the current implementation for controlled infrastructure testing. Production provider sign-in, Studio Admin credential and Client ID proof pages, and booking availability integration still need completion before real clients use galleries or submit live bookings.
