# Studio dependency and deployment isolation

`Build-Deploy-Studio-Kratos.ps1` remains the Studio deployment entry point.
It first synchronizes clean authoritative main checkouts and rejects operator
changes. It then prepares temporary detached Common worktrees from Studio's
`security-baseline.json`. Every pin must be an ancestor of authoritative main;
unmerged coordinated dependencies block deployment. The shared baseline verifier
must pass before restore/build/test/publish.

The pinned `CommonRoot` flows through restore, build, test and the existing
`Deploy-Kratos.ps1` fresh publish. A main branch advancing beyond an approved pin
cannot silently change the published shared library. Temporary dependency
worktrees are cleaned afterward without switching the operator's shared sources.
The default behavior of other deployment entry points is retained.

Studio-only server deployments preserve environment and registration state.
They do not run the cross-application secrets migration, remove unrelated
environment entries or delete control-plane bootstrap files. Public Site-only
deployment retains the same protection. Explicit coordinated control-plane
deployments retain their existing migration behavior. Application health checks
and automatic package rollback remain in the established deployer.

Local verification:

```sh
python3 tests/StudioDeploymentScopeTests.py
python3 tests/StudioPinnedDependenciesTests.py
```

The pin test discovers the baseline verifier in the normal sibling Common
layout, or accepts `STUDIO_SECURITY_BASELINE_VERIFIER`. It uses disposable Git
repositories to verify exact pinned source selection, unchanged original main
checkouts, cleanup and rejection of an unmerged/unavailable pin. Neither test
invokes a deployment. PowerShell parser validation and `bash -n` also pass.

Source approval, merges and application cutover remain separate gates. These
changes do not authorize deployment, live database writes or service restarts.

## Reviewed Demo cutover

The same entry point accepts `-ExpectedStudioCommit` and
`-StudioDemoCutoverDirectory`. Demo mode requires the exact merged main SHA and
Studio-only selection. The protected directory contains owner-only
`appsettings.Demo.json`, `studio-demo.env` and `plan.json`; never commit these
installation-specific inputs. The plan selects at least two immutable allowlisted
administrators and identifies the reviewed database, previous package, base
environment and service context. The bundle binds maintenance to its published
source SHA. Protected archives and remote staging use owner-only permissions.

The server verifies the prior context, takes package/configuration backups, stops
Studio, preserves current runtime directories, promotes the complete bundle and
installs a separate Demo environment/drop-in without rewriting existing provider
credentials or operator drop-ins. The Python hook runs package-version checking,
all administrator plans, then applies through real maintenance commands as the
service user before startup. The runtime enforces external integration isolation.
An exact database mismatch fails before account/schema writes. Existing Nginx
TLS configuration is tested and preserved without a reload when unchanged.

The generated solution resolves both project references and solution-level Common
projects to reviewed pins. Fresh publish follows the complete local solution
build/test. A server deployment lock excludes overlapping Studio deployments.
Storage, uploads, application data and the protected Demo profile survive package
promotion and rollback; current SQLite WAL siblings travel with their data
directory. Rollback restores the prior package/profile and restarts Studio while
retaining current runtime files and committed database/audit changes. It never
restores a stale database over new writes.

Run all three deployment suites (12 tests), PowerShell parser validation and
`bash -n scripts/Deploy-Kratos-Server.sh`. `StudioDemoCutoverTests.py` covers the
stopped-service requirement, target guards, plan-before-apply ordering and
failure recovery. Its systemd calls are simulated locally; a connected Mac
rehearsal additionally executes real maintenance against a restored PostgreSQL
database. Actual systemd acceptance belongs to the authorized Linux cutover.
