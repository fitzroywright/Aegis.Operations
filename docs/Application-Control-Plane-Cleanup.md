# Clean up an application in the control plane

Use Operations → Registration → Actions → **Clean up application**.

1. Sign in with Configuration.Manage permission.
2. Select an application (exact ApplicationId) or a family (that ID and its dot-separated descendants).
3. Review the affected registration instances and active configuration contracts.
4. Stop all applications in the selected scope on every host, including development runs.
5. Select **Clean up application**. Confirm the applications are stopped, type the selected ApplicationId exactly, and enter a reason.
6. If registrations or contracts changed since preview, refresh the preview and review the new selection before retrying.

Examples:

| Selection | Scope |
| --- | --- |
| Aegis.Studio — application only | Aegis.Studio only |
| Aegis.Studio — family | Aegis.Studio and Aegis.Studio.Web, Aegis.Studio.Worker, etc. |
| Aegis.Cafeteria — family | Aegis.Cafeteria and its Services, POS and POS.UI descendants |
| RequestPortal — application only | RequestPortal only |

Matching is case-insensitive and respects dot boundaries: Aegis.StudioBackup is not part of Aegis.Studio. Legacy names outside a selected namespace must be selected separately. Family choices are derived from registered application IDs and published contracts, rather than a hardcoded product list.

Cleanup retires selected registration identities, invalidates credentials and runtime leases, retires active contracts, clears current Operations application rows, closes active flow stages, and resolves active alerts. Audit history, old logs, retired contracts, and business databases are retained. Core control-plane services (Operations, Configuration and Diagnostics) cannot be reset through this workflow.

To register again, keep the applications stopped while backing up their local registration identity files. Restart one application at a time and approve its new Pending registration. Each host must have a distinct logical instance identity. Cafeteria-specific identity paths and commands are documented in Cafeteria-Control-Plane-Reset.md.

Deploy the paired Operations and Configuration changes together. The previous Cafeteria-specific cleanup API is replaced by generic cleanup-targets, cleanup-preview and cleanup endpoints. Existing family cleanup markers remain readable.
