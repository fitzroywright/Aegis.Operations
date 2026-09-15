# Main Branch Authority

## Rule

`main` is the authoritative branch for every Aegis application and Common library.

Completed or accepted functionality must exist on `main`. A feature branch is not a second source of truth and must not be treated as newer merely because Git reports commits that are not ancestors of `main`.

## Before accepting branch work

1. Compare the branch against current `main`.
2. Determine whether the behavior/files already exist on `main`.
3. If the branch is strictly behind `main`, it is historical only.
4. If the branch diverged but its accepted behavior already exists on `main`, do not merge it.
5. If the branch contains accepted behavior that would overwrite or remove newer main code, reimplement that behavior against current `main` and validate it there.
6. Never force an old branch into `main` when doing so would delete newer schema, configuration, security, diagnostics, or product work.

## Build authority

Use `scripts/Build-All-Aegis.ps1` from Aegis.Operations to validate the local workspace.

The script:

- refuses repositories with uncommitted changes;
- switches every repository to `main`;
- fetches and prunes remote refs;
- uses `git pull --ff-only origin main` so local merge commits cannot silently change authority;
- stops on any non-zero Git or .NET command exit code;
- restores, builds, and runs available tests;
- prints the checked-out main commit for each repository;
- prints the final success banner only after every project succeeds.

## Truth rule

A source change being present on `main` is not the same as proving it works.

Use these labels when reporting status:

- **Implemented on main**: accepted source is present on authoritative `main`.
- **Build/test proven**: restore/build/tests completed successfully for that exact or descendant main commit.
- **Runtime proven**: the deployed application was exercised against its real dependencies and failure/recovery paths.

Do not report a build, test, registration, health check, deployment, or integration as successful when the corresponding operation failed or did not execute.

## Historical branches

Historical branches may be retained temporarily for audit/reference, but they must not have open pull requests targeting `main` once superseded. Delete them after the retained behavior has been verified on `main` and no recovery/audit reason remains.
