# StickerFramework Architecture

## Overview

StickerFramework is a reusable Unity framework providing DI-driven infrastructure for UI, input, camera, rendering, asset management, scene transitions, and time services. It targets **Unity 6000.0** and is distributed as a Unity package (`com.stickerfwk.core`).

## Tech Stack

| Dependency | Purpose |
|---|---|
| **VContainer** | Dependency injection container |
| **MessagePipe** | Pub/sub event messaging |
| **UniTask** | Async/await for Unity |
| **R3** | Reactive extensions (disposables) |
| **Unity Addressables** | Async asset loading |
| **Unity InputSystem** | Modern input handling |
| **URP** | Blur/rendering features |

## Layer Architecture

```
Runtime/
  Core/              <- Interfaces, base classes, event types
    Presentation/    <- Presenter contracts and base classes
  Infrastructure/    <- Concrete implementations
```

**Core** defines contracts. **Infrastructure** implements them. Features in your app depend on Core interfaces only — they never reference Infrastructure directly.

### Assembly Definitions

| Assembly | Depends On | Purpose |
|---|---|---|
| `StickerFwk.Core` | UniTask, R3, VContainer, MessagePipe | Interfaces, WindowView, events, utilities |
| `StickerFwk.Infrastructure` | Core + Addressables + InputSystem | Main service implementations |
| `StickerFwk.Infrastructure.Camera` | Core + VContainer + MessagePipe | Camera registration |
| `StickerFwk.Infrastructure.Rendering` | Core + VContainer + URP | Blur effects |
| `StickerFwk.Infrastructure.UI` | Core + VContainer + UniTask + MessagePipe | UI service, transitions, layers |
| `StickerFwk.Infrastructure.Timeline` | Unity.Timeline | Loop track support |

## Core Services

### UI System (`Core/UI/`)

The central system. Stack-based window management with async transitions.

**Key types:**
- `WindowView` — Abstract MonoBehaviour base class for all UI screens. Configurable via Inspector: layer, blocking, transition type/duration.
- `IUIService` — Push/Pop/Replace windows on layer stacks. Loads prefabs from Addressables using key `Views/{TypeName}.prefab`.
- `UILayer` — Enum: `Background(0)`, `HUD(100)`, `Window(200)`, `Popup(300)`, `Modal(400)`, `Overlay(500)`.
- `IScreenTransitionService` — Full-screen overlay transitions (fade to black, wipe, etc.) for scene changes.
- `WindowOptions` — Runtime overrides for blocking, transition type/duration, and custom DI resolver.

**Window lifecycle:**
```
Load (Addressables) → OnInitialize(ct) → OnBeforeShow → [Transition In] → OnShow → ... → OnBeforeHide → [Transition Out] → OnHide → OnDispose
```

**Window prefab key convention:**
```
Views/{WindowTypeName}.prefab          // default
Views/{WindowTypeName}_{tag}.prefab    // tagged variant
```

### Presentation Helpers (`Core/Presentation/`)

Reusable presenter contracts for keeping MonoBehaviour views thin.

**Key types:**
- `IPresenter<TView>` — Bind/unbind/dispose contract for a presenter bound to one view instance.
- `Presenter<TView>` — Base implementation that guards null binding, double binding, and use after dispose.
- `IWindowPresenter<TView>` — Presenter contract with `WindowView` lifecycle hooks.
- `WindowPresenter<TView>` — Base class for UI window presenters; override only the lifecycle methods needed.

Use presenters for view-specific logic such as MessagePipe/R3 subscriptions, UI text formatting, button-command translation, and window lifecycle side effects. Views should keep serialized Unity references and expose small display/input APIs that presenters call.

### Input System (`Core/Input/`)

- `IInputService` — Cooked input that respects locks (returns zero when locked).
- `IRawInputService` — Raw hardware input, ignores locks.
- `IInputLockService` — Ref-counted locking. `Lock()` returns `IDisposable` for scoped use.

### Camera System (`Core/Camera/`)

- `ICameraService` — Register/unregister cameras by `CameraId`. Query by ID, layer mask, or GameObject.
- `ManagedCamera` — MonoBehaviour that auto-registers on Awake, auto-unregisters on Destroy.

### Time System (`Core/Time/`)

- `ITimeService` — DeltaTime, TimeScale, LocalTimeScale, FrameCount.
- `FeatureTimeService` — Wraps root TimeService, multiplies by `LocalTimeScale` for per-feature time scaling (slow-mo, pause).

### Asset Management (`Core/AssetManagement/`)

- `IAssetRequester` — Async asset loading with ref-counted handles.
- `AddressableCache` — Prevents duplicate concurrent loads via `KeyedOperationGate`.
- All asset handles implement `IDisposable`.

### Master Data (`Core/MasterData/`)

- `IMasterDataRepository` — Load all master data at startup, query by type and ID.
- `MasterData<T>` — Serializable base class with `Id` field.
- `MasterAsset<T>` — ScriptableObject container, labeled `"MasterData"` in Addressables.

### Scene Management (`Core/`)

- `ISceneTransitionService` — Locks input, covers screen with transition overlay, loads scene, waits for ready signal, reveals.
- `SceneReadyNotifier` — Completion source. Scene code calls `NotifyReady()` when initialization is done.

### Rendering (`Core/Rendering/`)

- `IBlurService` — Ref-counted blur requests with easing transitions. Dual Kawase blur via URP renderer feature.

### Initialization (`Core/Initialization/`)

- `IRootInitService` — Exposes `UniTask Initialization` for awaiting app startup (frame rate setup, master data loading).

## Design Patterns

### 1. Dependency Injection (VContainer)

All services are registered in a **RootLifetimeScope** and injected via constructors. VContainer lifecycle interfaces:
- `IStartable` — sync initialization (e.g., UIService creates layer canvases)
- `IAsyncStartable` — async initialization (e.g., RootInitService loads master data)
- `ITickable` — per-frame updates (e.g., ScreenService checks resolution changes)
- `IDisposable` — cleanup

Features can create **child LifetimeScopes** for scoped dependencies. Pass the child resolver via `WindowOptions.Resolver` so pushed windows get feature-specific injection.

### 2. State and Logic Ownership

Use this responsibility model in consuming projects:

| Concern | Owner | Allowed state | Examples |
|---|---|---|---|
| Domain state / 状態管理 | Domain models, entities, value objects | Durable gameplay state and invariants | placed flags, score, level completion, valid transitions |
| Business logic | Domain first; Application for orchestration | Domain-owned state plus short-lived command locals | rule decisions, command handling, repository-backed initialization |
| Presentation logic | Presenters and presentation services | Transient UI/interaction state | selected item, button guard, drag candidate, formatted UI text |
| View display | MonoBehaviour views | Serialized references and cached Unity components | text/image assignment, input listener exposure |
| Infrastructure | Infrastructure services | Technical caches/resources | addressable handles, registered cameras, input locks, blur refs |

`XxxService` does not automatically mean stateless. Services can hold state when the state is part of their technical or orchestration responsibility, but durable gameplay truth belongs in models/entities. If a value must survive across commands as business truth, make it model state. If a value only describes an in-progress UI gesture, transition, or technical resource, a presenter/service may own it.

### 3. Event-Driven Communication (MessagePipe)

Events are `readonly struct`s. Services inject `IPublisher<T>` to fire, consumers inject `ISubscriber<T>` to listen.

Built-in events:
```csharp
WindowOpenedEvent(string Key, UILayer Layer)
WindowClosedEvent(string Key, UILayer Layer)
InputLockChangedEvent(bool IsLocked)
CameraRegisteredEvent(CameraId Id)
ScreenChangedEvent                          // marker (no data)
BlurTransitionEvent(bool Enabled, EaseType Ease, float Duration)
```

### 4. Async-First (UniTask)

All I/O operations return `UniTask`. Cancellation tokens propagate through all call chains. Use `UniTask.CompletedTask` for sync implementations of async interfaces.

### 5. IDisposable Resource Management

- Asset handles are ref-counted — dispose when done.
- Input locks return `IDisposable` — use `using var _ = lockService.Lock();`.
- Blur requests return `IDisposable`.
- `WindowView.AddDisposable()` tracks subscriptions, cleaned on hide.

### 6. Thin Views

MonoBehaviour views are display-only. No business logic. They:
- Receive an injected presenter via `[Inject]` and bind it with `presenter.Bind(this)`
- Expose display methods, for example `SetCount(...)` or `SetInteractable(...)`
- Expose input subscription methods, for example `AddClickListener(...)`
- Forward `WindowView` lifecycle hooks to `WindowPresenter<TView>`

All view-specific logic lives in presenters/controllers (plain C# classes). Presenters own disposable subscriptions and are registered in VContainer.
