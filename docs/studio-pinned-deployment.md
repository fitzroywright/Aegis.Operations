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
