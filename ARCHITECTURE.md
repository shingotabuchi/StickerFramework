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

### 2. Event-Driven Communication (MessagePipe)

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

### 3. Async-First (UniTask)

All I/O operations return `UniTask`. Cancellation tokens propagate through all call chains. Use `UniTask.CompletedTask` for sync implementations of async interfaces.

### 4. IDisposable Resource Management

- Asset handles are ref-counted — dispose when done.
- Input locks return `IDisposable` — use `using var _ = lockService.Lock();`.
- Blur requests return `IDisposable`.
- `WindowView.AddDisposable()` tracks subscriptions, cleaned on hide.

### 5. Thin Views

MonoBehaviour views are display-only. No business logic. They:
- Receive injected dependencies via `[Inject]`
- Forward user input to Presenters
- Bind UI elements to state
- Override `WindowView` lifecycle hooks

All logic lives in Presenters/Controllers (plain C# classes).
