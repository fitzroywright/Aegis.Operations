# Request Portal Bridge on Kratos

Deploy only `RequestPortal/FfpManager.Bridge`, using the local restore/build/test,
Linux publish, tar/scp/SSH, restart, health-check and rollback conventions used by
`scripts/Deploy-Kratos.ps1` for Cafeteria. This entrypoint does not deploy the website
or change its configuration. Run from PowerShell 7 on Windows, macOS or Linux.

## First installation (administrator)

1. Verify the .NET runtime matching RequestPortal's target framework, Python 3,
   curl, systemd, and trusted CA certificates on Kratos. Verify SQL connectivity
   and the mounted supporting-document share under the dedicated identity.
2. Create system user/group `aegis-ffpmanager-bridge`. Install the supplied unit as
   `/etc/systemd/system/aegis-ffpmanager-bridge.service`. Install
   `scripts/Deploy-RequestPortalBridge-Kratos-Server.sh` as root-owned mode 0755
   `/usr/local/sbin/aegis-deploy-requestportal-bridge`. Only permit the deployment
   account to sudo this root-owned executable; never permit execution of an
   uploaded script as root. Updating this deployer requires an administrator.
3. Provision `/etc/aegis/ffpmanager-bridge.env` as root-owned mode 0600. Configure
   `Bridge__ServiceIdentity`, `Bridge__Gateway=Sql`,
   `Bridge__RequestPortalBaseUrl` (HTTPS), `Bridge__SupportingDocumentRoot`,
   `Bridge__InitialApplicationStatus`, `Bridge__StatusMap__<legacy-code>`,
   `Bridge__DocumentTypeMap__<portal-type>` and `Bridge__EnableOutboxDispatch=true`.
   Set `AllowedHosts` to the bridge hostname. Use real lookup values, never guessed
   example values. Configure the approved Common.Secrets production provider and
   workload bootstrap credentials using the existing deployment conventions.
   Resolve `FfpManagerBridge:SqlConnectionString` and
   `FfpManagerIntegration:SharedSecret` through that provider; do not put them in
   source-controlled settings or command arguments. This script does not provision
   secrets or silently copy another application's identity.
4. Configure an HTTPS listener and certificate, for example using
   `ASPNETCORE_URLS=https://0.0.0.0:<approved-port>` and the appropriate Kestrel
   certificate configuration. Restrict firewall access to approved callers.
   Both Portal-to-bridge and bridge-to-Portal traffic must validate certificates.
   Install any internal CA in both machines' trust stores. Do not disable TLS
   validation. If TLS terminates at a reverse proxy, the local upstream must be
   restricted to loopback; approve that topology explicitly.
5. Ensure diagnostics use the unit's persistent StateDirectory, and grant the
   service account access to its certificate, secrets bootstrap files and mounted
   test document directory. This deployment does not acquire Kerberos tickets;
   integrated SQL/SMB authentication needs separately provisioned ticket renewal.
6. Install and review the SQL scripts described in RequestPortal's
   `docs/FFP-MANAGER-INTEGRATION-COMMISSIONING.md`, against the intended test DB.
   FFP Manager must enqueue events transactionally. Deployment does not run SQL
   migrations or change production data.
7. Run `systemctl daemon-reload`. Enable the unit for boot once initial
   commissioning is accepted. The deployment starts it without changing enablement.

## Deploy

From the desired Aegis.Operations branch, with sibling RequestPortal and Common
repositories checked out at the intended compatible revisions:

```powershell
pwsh ./scripts/Build-Deploy-RequestPortalBridge-Kratos.ps1 -Root "$HOME/Projects" -BridgeBaseUrl "https://<bridge-hostname>:<approved-port>"
```

On Windows use `-Root D:\Projects`. The HTTPS origin is mandatory and must resolve
from Kratos to this bridge, directly or through its approved proxy. The deployment
checks `/health` and `/ready` without following redirects or bypassing certificates;
it verifies JSON status and SQL gateway mode. The bridge production startup guards
must also pass. These checks do not prove document-share writes, callbacks, event
wiring or end-to-end acceptance.

`-SkipTests` is explicit. `-SkipPublishBuild` additionally requires prebuilt Release
outputs restored/built for the exact target runtime; the normal solution build
alone does not supply those runtime-specific outputs. Neither option is the default.
The script never fetches, switches, or resets source repositories.

The service must use isolated test DB/storage and test notification destinations
for commissioning, even though it runs with production security settings.

## Failure and recovery

The deployer preserves environment, secrets, persistent diagnostics, databases and
document storage. It backs up application binaries before replacement. On failed
startup, health or readiness, it restores the previous application and restarts it
only if it was previously active. It leaves first-install failures stopped. Backups
are retained under `/opt/aegis/backups`; manage retention separately.

Inspect failures locally with `journalctl -u aegis-ffpmanager-bridge.service`.
The scripts do not print environment files, connection strings or journal contents.
After successful deployment, separately configure Portal's HTTPS bridge URL and
perform the submission, attachment, status callback, information exchange,
duplicate-prevention and outage-recovery acceptance tests. Existing diagnostics
and configuration-contract files do not themselves prove control-plane registration;
verify that separately before claiming full commissioning.
