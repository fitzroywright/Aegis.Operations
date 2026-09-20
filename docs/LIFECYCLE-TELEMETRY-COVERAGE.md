# Structured Lifecycle Telemetry Coverage

This matrix tracks adoption of the `Common.Diagnostics.LifecycleEvent` abstraction. A project is only considered fully green when the shared abstraction is used, sensitive data is excluded, project-level tests exist where practical, and build/test evidence is available.

| Project | Shared abstraction | Operations path | Consumer tests | CI evidence | Status / notes |
|---|---|---|---|---|---|
| Common.Diagnostics | Yes | Defines transport contract + HTTP sink | LifecycleTelemetryTests, HttpLifecycleEventSinkTests | CI passed on lifecycle foundation | Green |
| Common.Registration | Yes | Emits registration lifecycle through ILifecycleEventSink | LifecycleRegistrationTests; LifecycleTelemetryIntegrationTests | Workflow evidence pending | Yellow |
| Common.Security | Yes | LifecycleSecurityEventSink | LifecycleSecurityEventSinkTests | Workflow evidence pending | Yellow |
| Common.Secrets | Yes | LifecycleSecretTelemetrySink | LifecycleSecretTelemetrySinkTests | Workflow evidence pending | Yellow |
| Common.Messaging | Yes | LifecycleMessageDeliveryObserver | LifecycleMessageDeliveryObserverTests | Workflow evidence pending | Yellow |
| Common.Storage | Yes | Structured storage lifecycle telemetry | LifecycleStorageTelemetryTests | Workflow evidence pending | Yellow |
| Aegis.Hello | Yes | Authenticated Operations lifecycle publisher | Common contract exercised; direct Hello sink test still desirable | Application CI currently fails before any step starts | Yellow |
| Aegis.Configuration | Yes | Control-plane registration lifecycle publisher | Existing lifecycle authority/flow tests; direct publisher test desirable | Application CI currently fails before any step starts | Yellow |
| Aegis.Diagnostics | Yes | Control-plane registration lifecycle publisher | Existing diagnostics + lifecycle coverage | CI passed; rollout merged | Green |
| Aegis.Operations | Yes | Authenticated lifecycle ingestion + journey API + visualizer | StructuredLifecycleTelemetryTests | Application CI currently fails before any step starts | Yellow |
| Aegis.Cafeteria | Yes on rollout branch | Heartbeat + committed business flow lifecycle events | Common contract coverage; app consumer test still desirable | Application CI currently fails before any step starts | Yellow |
| RequestPortal | Yes on rollout branch | Heartbeat + request submission + staff authentication | Common contract coverage; app consumer test still desirable | Application CI currently fails before any step starts | Yellow |
| Aegis.Studio | Yes on rollout branch | Heartbeat + Studio workflows | Common contract coverage; app consumer test still desirable | Application CI currently fails before any step starts | Yellow |
| Aegis.Studio.WorkDay | Via Studio rollout | WorkDay session/sync/upload/message flows emitted by Studio Operations publisher | WorkDay functional tests exist; lifecycle-specific consumer test desirable | Studio CI currently fails before any step starts | Yellow |
| Aegis.SensorNetwork | Yes on rollout branch | Heartbeat + access / guard manual access flows | Existing telemetry tests; lifecycle consumer test desirable | Application CI currently fails before any step starts | Yellow |
| Ebolito | Yes on main | Heartbeat + conversation lifecycle events | LifecycleTelemetryTests added on test branch | CI run not yet available | Yellow |
| Aegis.Payroll | Not yet on main | No registered Operations publisher yet | None | CRUD implementation exists only on feature/crud-foundation | Red |
| Aegis.NGO | No repository/project available | None | None | No accessible implementation exists | Red / blocked on project creation |

## Safety contract

Lifecycle telemetry must never contain passwords, secrets, claim tokens, bearer credentials, authorization headers, connection strings, personal message content, document content, or other sensitive payload data. Correlation IDs, operation IDs, stage names, outcome, bounded diagnostic codes, and non-sensitive technical metadata are permitted.

## Migration rule

Existing `/api/operations/flows/observe` publishers may remain during migration, but every instrumented workflow should also emit `LifecycleEvent` to `/api/operations/lifecycle/events`. The generic lifecycle model is the long-term source for the workflow visualizer.
