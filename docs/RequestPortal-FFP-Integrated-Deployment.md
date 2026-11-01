# Request Portal + FFP Manager Bridge: JMDB01 / FFP commissioning

## Scope and prohibitions
Deploy the Request Portal and FfpManager.Bridge from the RequestPortal repository using the integrated entrypoint `scripts/Build-Deploy-RequestPortal-Kratos.ps1` in Aegis.Operations. The only authorized FFP Manager SQL target is **JMDB01/FFP**. **Never connect to, inspect or modify FFPJM.** Existing Request Portal application, PostgreSQL data, identity settings, user uploads, and state must be preserved.

## What is already established (2026-11-01)
- Request Portal runs as `aegis` with `/opt/aegis/requestportal/RequestPortal.Web.dll` and `/etc/aegis/requestportal.env`.
- The bridge systemd unit exists but is inactive: `aegis-ffpmanager-bridge.service`.
- The bridge deployment directory `/opt/aegis/ffpmanager-bridge` was missing.
- The unit points to the root-owned `/usr/local/sbin/aegis-deploy-requestportal-bridge` installer, and prestarts with `/usr/local/libexec/aegis-check-requestportal-bridge`.
- The bridge environment template has placeholder initial status and status/document mapping keys, OpenBao RoleId and BasePath unset, TLS/SecretId missing or unreadable, and supporting document root not provisioned.
- The Mac build succeeds, but PostgreSQL integration tests fail when `REQUESTPORTAL_POSTGRES` is missing. Do not turn those failures into a green build by skipping tests.
- `https://kratos.fftpj.local:5440` is the selected bridge origin, not an observed validated listener or certificate.

## Stage 1: Build/test prerequisites on Mac
1. `cd ~/Projects/Aegis.Operations && git pull --ff-only origin main`.
2. Confirm the authoritative `main` of RequestPortal includes the verified bridge work, and clean up/stage local changes safely. The launcher refuses dirty checkouts.
3. Provision a dedicated disposable PostgreSQL test DB; never use the live Request Portal database. Export `REQUESTPORTAL_POSTGRES` only into the launching terminal environment; never commit credentials.
4. Run `dotnet test ~/Projects/RequestPortal/RequestPortal.slnx -c Release` and confirm zero failures. The integrated deployer will independently rerun tests.

## Stage 2: Bridge environment on Kratos
Existing files:
- `/etc/aegis/ffpmanager-bridge.env` (root-owned, 0600)
- `/etc/systemd/system/aegis-ffpmanager-bridge.service`
- `/etc/aegis/ffpmanager-bridge/tls/`
- `/usr/local/libexec/aegis-check-requestportal-bridge`

Set `AllowedHosts=kratos.fftpj.local` and `ASPNETCORE_URLS=https://0.0.0.0:5440` only if the listener is approved on that interface, the port is free, and certificate covers that DNS name. Prefer firewall restriction to the intended hosts; never expose the bridge to the public Internet. Configure trusted certificate chain and private key with the service group permitted to read the key.

Obtain valid `Bridge__InitialApplicationStatus`, `Bridge__StatusMap__*`, and `Bridge__DocumentTypeMap__*` from actual JMDB01/FFP schema/lookups and the FFP Manager application's semantics. Replace placeholder *keys and values*; do not guess codes. Use the guarded read-only SQL preflight and compare migrations 000/001 before proposing any change.

Configure the bridge's approved OpenBao workload RoleId/BasePath and persistent SecretId file. The named `Bridge__SqlConnectionStringSecretName` must resolve to a connection that targets JMDB01 and database FFP. Test its effective target using the bridge's actual service identity; do not print the secret. Explicitly reject any reference to FFPJM. The current service account has no observed Kerberos ticket; if using Windows Integrated SQL authentication, provision valid noninteractive ticket renewal or an approved keytab mechanism before starting. Do not assume root's credentials apply.

Set `Bridge__SupportingDocumentRoot` to an existing Linux-mounted test TrueNAS path. Verify the mount is present and the bridge service identity has read/write/execute access. Never point it at a directory that just happens to exist when TrueNAS is absent; use systemd mount dependencies.

Run the existing read-only validation from Kratos:
```bash
/usr/bin/python3 /usr/local/libexec/aegis-check-requestportal-bridge --env-file /etc/aegis/ffpmanager-bridge.env
```
A zero exit code is required, but does not alone establish secret resolution, SQL connectivity, TLS trust, or write access as the service identity.

## Stage 3: Install root-owned deployer
After syncing the latest Operations repository on the Mac, run the existing bootstrap:
```bash
cd ~/Projects/Aegis.Operations
pwsh ./scripts/Build-Deploy-RequestPortalBridge-Kratos.ps1 -Root "$HOME/Projects" -InitializeOnly
```
The bootstrap should preserve existing environment files and unit customizations, and update the installed root-owned deployer. Compare customized unit settings. Do not grant sudo access to user-uploaded deployment scripts.

## Stage 4: Integrated deployment
Once all prerequisites are verified:
```bash
cd ~/Projects/Aegis.Operations
pwsh ./scripts/Build-Deploy-RequestPortal-Kratos.ps1 -Root "$HOME/Projects" -BridgeBaseUrl "https://kratos.fftpj.local:5440"
```
This builds/tests RequestPortal, then builds/tests/publishes and deploys the bridge with guarded HTTPS /health and /ready checks, then deploys Request Portal. It intentionally does NOT auto-configure secrets, migrate SQL, activate the Portal's bridge integration, or run business acceptance.

## Stage 5: Portal activation and acceptance
Only after receiver readiness:
1. Verify the deployed Portal config is compatible with the bridge and set `FfpManagerIntegration__BaseUrl` to the trusted bridge HTTPS origin using the application's real configuration convention, with `FfpManagerIntegration__Enabled` still false until acceptance preparation.
2. Verify both sides resolve the approved matching shared-secret reference through OpenBao; do not insert plaintext into JSON or Git.
3. Validate SQL target by a read-only JMDB01/FFP query under the actual service identity and check correct migration/schema presence. Do not access FFPJM.
4. Confirm the transactional FFP Manager event hooks, status callbacks, outbox, notifications and attachment path are implemented and ready.
5. Enable Portal's integration only when a controlled acceptance submission is authorized. Use an explicitly synthetic submission; verify Application Letters, Supporting Documents, idempotency, status reconciliation and rollback behavior directly in FFP and ensure production user data is unaffected.
6. Validate Portal service/UI, bridge health, logs, TLS, outbox and notification behavior. If failed, disable Portal integration first and roll back application binaries through the supported deployer; never use destructive database rollback.

## Current stopping conditions
This runbook is NOT an approval to run SQL migrations against FFP or enable writes automatically. Unknown FFP lookup codes, unavailable OpenBao credentials, untrusted TLS material, missing test PostgreSQL, inaccessible TrueNAS or missing noninteractive Kerberos are genuine blockers and should be reported precisely. No fallback to FFPJM is permitted.
