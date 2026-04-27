# StickerFramework

Reusable Unity 6 (URP) framework distributed as a local UPM package (`com.stickerfwk.core`).
Provides **Core** interfaces and **Infrastructure** implementations built around
VContainer (DI), UniTask (async), R3 (reactive), and MessagePipe (pub/sub).

---

## 1. Folder Structure

```
Packages/com.stickerfwk.core/
├── package.json
├── Runtime/
│   ├── Core/                              # Interfaces, base classes, value objects, events, utilities
│   │   ├── StickerFwk.Core.asmdef
│   │   ├── ISceneTransitionService.cs
│   │   ├── SceneReadyNotifier.cs
│   │   ├── Animation/
│   │   ├── AssetManagement/
│   │   ├── Camera/
│   │   ├── Diagnostics/
│   │   ├── Initialization/
│   │   ├── Input/
│   │   ├── InspectorTools/
│   │   ├── MasterData/
│   │   ├── Physics/
│   │   ├── Presentation/
│   │   ├── Rendering/
│   │   ├── Screen/
│   │   ├── Time/
│   │   ├── UI/
│   │   └── Utilities/
│   │
│   └── Infrastructure/                    # Concrete implementations
│       ├── StickerFwk.Infrastructure.asmdef
│       ├── AssetManagement/
│       ├── Camera/                        # separate asmdef
│       ├── Initialization/
│       ├── Input/
│       ├── MasterData/
│       ├── Rendering/                     # separate asmdef
│       ├── SceneManagement/
│       ├── Time/
│       ├── Timeline/                      # separate asmdef
│       └── UI/                            # separate asmdef
```

### Core — `Runtime/Core/`

| Subfolder | Key Types | Description |
|---|---|---|
| *(root)* | `ISceneTransitionService`, `SceneReadyNotifier` | Scene transition contract and ready signal |
| `Animation/` | `AnimatorExtensions`, `EaseType`, `Easing`, `RotationAnimator`, `SpriteWipeAnimator`, `UniformScaleSetter` | Animation helpers and easing utilities |
| `AssetManagement/` | `IAssetRequester` | Async asset loading contract |
| `Camera/` | `ICameraService`, `CameraId`, `CameraFitter`, `CameraExtensions` | Camera registration and query contracts |
| `Diagnostics/` | `Assert`, `Log` | Debug assertion and logging wrappers |
| `Initialization/` | `IRootInitService` | App startup contract |
| `Input/` | `IInputService`, `IRawInputService`, `IInputLockService`, `InputLockService`, `InputLockChangedEvent` | Input abstraction with ref-counted locking |
| `InspectorTools/` | `ButtonAttribute` | Custom inspector attributes |
| `MasterData/` | `IMasterData`, `IMasterDataRepository`, `IMasterDataScriptableObject`, `MasterAsset<T>`, `MasterData<T>` | Static data loading and query |
| `Physics/` | `IWorldRaycastService` | Physics raycast contract |
| `Presentation/` | `IPresenter<TView>`, `Presenter<TView>`, `IWindowPresenter<TView>`, `WindowPresenter<TView>` | Plain C# presenter contracts for moving view-specific logic out of MonoBehaviours |
| `Rendering/` | `IBlurService`, `BlurTransitionEvent` | Blur effect contract and event |
| `Screen/` | `ScreenService`, `ScreenChangedEvent` | Screen resolution monitoring |
| `Time/` | `ITimeService` | Time abstraction contract |
| `UI/` | `IUIService`, `IScreenTransitionService`, `ITransition`, `WindowView`, `WindowOptions`, `CoolButton`, `SafeAreaView`, `ScreenTransitionView`, `UILayer`, `TransitionType`, `WindowOpenedEvent`, `WindowClosedEvent` | Stack-based UI window system |
| `Utilities/` | `Deque<T>`, `KeyedOperationGate<TKey>`, `SmoothMath` | General-purpose data structures and math |

### Infrastructure — `Runtime/Infrastructure/`

| Subfolder | Key Types | Description |
|---|---|---|
| `AssetManagement/` | `AddressableCache`, `AddressableHandle<T>`, `AddressableManager`, `IAddressableHandle` | Addressables-backed asset loading |
| `Camera/` | `CameraService`, `CameraModel`, `ManagedCamera`, `CameraRegisteredEvent` | Camera registry implementation |
| `Initialization/` | `RootInitService` | Startup sequence (frame rate, master data) |
| `Input/` | `InputService`, `LockingInputService`, `WorldRaycastService` | Input System wrappers |
| `MasterData/` | `MasterDataRepository` | Addressables-backed master data loading |
| `Rendering/` | `BlurService`, `BlurVolume`, `ManagedBlurVolume`, `DualKawaseBlurFeature`, `DualKawaseBlurPass`, `CachedBlurBlitPass` | URP dual-Kawase blur pipeline |
| `SceneManagement/` | `SceneTransitionService` | Scene loading with transition overlay |
| `Time/` | `TimeService`, `FeatureTimeService` | Frame time and per-feature time scaling |
| `Timeline/` | `LoopTrack`, `LoopTrackComponent`, `LoopClipAsset`, `LoopClipBehaviour`, `LoopClipMixerBehaviour` | Custom Timeline looping track |
| `UI/` | `UIService`, `UILayerManager`, `WindowHandle`, `TransitionFactory`, `InputBlocker`, `InputBlockerView`, `ScreenTransitionService`, `FadeTransition`, `ScaleTransition`, `SlideTransition`, `AnimatorTransition`, `TimelineTransition`, `NoneTransition`, `PlayableDirectorExtensions` | Full UI service implementation with transitions |

---

## 2. Namespace Conventions

### Core layer

Most subfolders use the **flat root namespace** `StickerFwk.Core`. Subfolders that form
a cohesive module with many related types use a **hierarchical sub-namespace**
`StickerFwk.Core.<Module>`.

| Location | Namespace | Pattern |
|---|---|---|
| `Core/` *(root files)* | `StickerFwk.Core` | Flat |
| `Core/Camera/` | `StickerFwk.Core` | Flat |
| `Core/Diagnostics/` | `StickerFwk.Core` | Flat |
| `Core/Input/` | `StickerFwk.Core` | Flat |
| `Core/Physics/` | `StickerFwk.Core` | Flat |
| `Core/Screen/` | `StickerFwk.Core` | Flat |
| `Core/Time/` | `StickerFwk.Core` | Flat |
| `Core/Utilities/` | `StickerFwk.Core` | Flat |
| `Core/Animation/` | Mixed — `StickerFwk.Core` *and* `StickerFwk.Core.Animation` | Mixed |
| `Core/AssetManagement/` | `StickerFwk.Core.AssetManagement` | Hierarchical |
| `Core/Initialization/` | `StickerFwk.Core.Initialization` | Hierarchical |
| `Core/InspectorTools/` | `StickerFwk.Core.InspectorTools` | Hierarchical |
| `Core/MasterData/` | `StickerFwk.Core.MasterData` | Hierarchical |
| `Core/Presentation/` | `StickerFwk.Core.Presentation` | Hierarchical |
| `Core/Rendering/` | `StickerFwk.Core.Rendering` | Hierarchical |
| `Core/UI/` | `StickerFwk.Core.UI` | Hierarchical |

> **Rule of thumb:** If consumers need to import the module explicitly (asset loading,
> master data, UI, rendering), it gets its own sub-namespace. General contracts that
> most features use daily (input, camera, time, screen) stay in the flat root to
> minimise `using` statements.

### Infrastructure layer

All subfolders use **hierarchical sub-namespaces** matching the folder:
`StickerFwk.Infrastructure.<Module>`.

| Location | Namespace |
|---|---|
| `Infrastructure/AssetManagement/` | `StickerFwk.Infrastructure.AssetManagement` |
| `Infrastructure/Camera/` | `StickerFwk.Infrastructure.Camera` |
| `Infrastructure/Initialization/` | `StickerFwk.Infrastructure.Initialization` |
| `Infrastructure/Input/` | `StickerFwk.Infrastructure.Input` |
| `Infrastructure/MasterData/` | `StickerFwk.Infrastructure.MasterData` |
| `Infrastructure/Rendering/` | `StickerFwk.Infrastructure.Rendering` |
| `Infrastructure/SceneManagement/` | `StickerFwk.Infrastructure.SceneManagement` |
| `Infrastructure/Time/` | `StickerFwk.Infrastructure.Time` |
| `Infrastructure/Timeline/` | `StickerFwk.Infrastructure.Timeline` |
| `Infrastructure/UI/` | `StickerFwk.Infrastructure.UI` |

### Examples

```csharp
// Core — flat namespace (Camera subfolder)
namespace StickerFwk.Core
{
    public interface ICameraService { /* ... */ }
}

// Core — hierarchical namespace (UI subfolder)
namespace StickerFwk.Core.UI
{
    public abstract class WindowView : MonoBehaviour { /* ... */ }
}

// Infrastructure — always hierarchical
namespace StickerFwk.Infrastructure.Camera
{
    public class CameraService : ICameraService { /* ... */ }
}
```

---

## 3. Assembly Definitions

Six `.asmdef` files define compilation boundaries and dependency edges.

### Summary

| Assembly | Root Namespace | Location | Key References |
|---|---|---|---|
| `StickerFwk.Core` | `StickerFwk.Core` | `Runtime/Core/` | UniTask, R3.Unity, VContainer, MessagePipe, URP |
| `StickerFwk.Infrastructure` | `StickerFwk.Infrastructure` | `Runtime/Infrastructure/` | StickerFwk.Core, Unity.Addressables, Unity.ResourceManager, Unity.InputSystem, UniTask, R3.Unity, VContainer, MessagePipe |
| `StickerFwk.Infrastructure.Camera` | `StickerFwk.Infrastructure.Camera` | `Runtime/Infrastructure/Camera/` | StickerFwk.Core, VContainer, MessagePipe, UniTask |
| `StickerFwk.Infrastructure.Rendering` | `StickerFwk.Infrastructure.Rendering` | `Runtime/Infrastructure/Rendering/` | StickerFwk.Core, VContainer, UniTask, MessagePipe, URP |
| `StickerFwk.Infrastructure.UI` | `StickerFwk.Infrastructure.UI` | `Runtime/Infrastructure/UI/` | StickerFwk.Core, VContainer, UniTask, MessagePipe |
| `StickerFwk.Infrastructure.Timeline` | `StickerFwk.Infrastructure.Timeline` | `Runtime/Infrastructure/Timeline/` | Unity.Timeline |

### Dependency Graph

```
StickerFwk.Core                          (no framework assembly dependencies)
  ^
  |--- StickerFwk.Infrastructure         (main implementations — Addressables, InputSystem)
  |--- StickerFwk.Infrastructure.Camera  (VContainer, MessagePipe, UniTask)
  |--- StickerFwk.Infrastructure.Rendering (VContainer, UniTask, MessagePipe, URP)
  |--- StickerFwk.Infrastructure.UI      (VContainer, UniTask, MessagePipe)

StickerFwk.Infrastructure.Timeline       (standalone — Unity.Timeline only)
```

All Infrastructure assemblies reference `StickerFwk.Core`.
`StickerFwk.Infrastructure.Timeline` is standalone — it has no dependency on Core or
any DI framework, only `Unity.Timeline`.

### When to create a separate Infrastructure assembly

Create a **separate** `.asmdef` when:

- The module pulls in a **heavyweight or optional Unity package** (URP, Timeline,
  Input System) that other Infrastructure code should not depend on.
- The module has a **distinct set of third-party references** that would pollute
  the main assembly's dependency list.
- You want consuming projects to be able to **exclude** the module without
  recompiling the rest of Infrastructure.

Put code in the **main** `StickerFwk.Infrastructure` assembly when:

- It only needs packages already referenced by the main assembly (Addressables,
  Input System, UniTask, R3, VContainer, MessagePipe).
- It is a small implementation (one or two files) that does not warrant its own
  compilation unit.

---

## 4. Consuming Project Conventions

When a game project imports this framework, follow these conventions.

### Root namespace

Pick your own root (e.g., `MyGame`). Never place game code in `StickerFwk.*`.

### Recommended folder layout

```
Assets/Scripts/
├── Runtime/
│   ├── Core/                              # Game-specific interfaces and value objects
│   ├── Infrastructure/                    # Game-specific implementations
│   ├── Features/
│   │   └── <FeatureName>/
│   │       ├── Domain/                    # Entities, value objects, domain events
│   │       ├── Application/               # Use cases, app services, ports
│   │       └── Presentation/              # MonoBehaviour views, presenters, UI services
│   ├── Installers/                        # VContainer LifetimeScopes
│   └── MasterData/                        # Game-specific MasterData<T> subclasses
```

### Assembly naming

| Assembly | Scope |
|---|---|
| `<Game>.Runtime.Core.Game` | Game-specific interfaces and value objects |
| `<Game>.Infrastructure.Game` | Game-specific implementations |
| `<Game>.Runtime.Features.<X>.Domain` | Feature domain layer (`noEngineReferences: true`) |
| `<Game>.Runtime.Features.<X>.Application` | Feature application/use-case layer |
| `<Game>.Runtime.Features.<X>.Presentation` | Feature MonoBehaviour views, presenters, repositories, adapters |
| `<Game>.Runtime.Installers` | DI registration (LifetimeScopes) |

### Layer dependency rules

```
Domain          → (zero dependencies — pure C#, noEngineReferences: true)
Application     → Domain + StickerFwk.Core
Presentation    → Domain + Application + StickerFwk.Core
Installers      → all of the above + StickerFwk.Infrastructure.*
```

Key constraints:

- **Only Installers reference Infrastructure.** Feature code depends on Core
  interfaces, never on concrete implementations.
- **Features never reference other features.** Cross-feature communication goes
  through MessagePipe (`IPublisher<T>` / `ISubscriber<T>`).
- **Domain has zero assembly references.** Use `noEngineReferences: true` to
  enforce this at compile time.

### Responsibility model

Use this model to decide where state management, business logic, presentation logic, and service state belong:

| Concern | Owner | Allowed state | Examples |
|---|---|---|---|
| Domain state / 状態管理 | Domain models, entities, value objects | Durable gameplay state and invariants | placed flags, score, level completion, valid transitions |
| Business logic | Domain first; Application for use-case orchestration | Domain-owned state plus short-lived command locals | rule decisions, command handling, repository-backed initialization |
| Presentation logic | Presenters and presentation services | Transient UI/interaction state | button guards, selected item, drag candidate, formatted UI text |
| View display | MonoBehaviour views | Serialized references and cached Unity components | text/image assignment, input listener exposure |
| Infrastructure | Infrastructure services | Technical caches/resources | addressable handles, registered cameras, input locks, blur refs |

`XxxService` does not automatically mean stateless. Services can hold state when the state is part of their responsibility, but durable gameplay truth belongs in models/entities. If a value must survive across commands as business truth, make it model state. If a value only describes an in-progress UI gesture, transition, or technical resource, a presenter/service may own it.

---

## 5. Presenter Pattern

Use `StickerFwk.Core.Presentation` when a view needs logic beyond pure rendering. Views remain MonoBehaviours with serialized Unity references; presenters are plain C# classes that own subscriptions, button-command translation, UI text formatting, and other view-specific orchestration.

**Framework types:**
- `IPresenter<TView>` / `Presenter<TView>` — bind/unbind/dispose lifecycle for any view type.
- `IWindowPresenter<TView>` / `WindowPresenter<TView>` — adds `WindowView` lifecycle hooks: initialize, before show, show, before hide, hide.

**Feature placement:**
```
Assets/Scripts/Runtime/Features/<FeatureName>/Presentation/
  Views/
    XxxView.cs              # MonoBehaviour: serialized refs, display APIs, input listener APIs
  Presenters/
    XxxPresenter.cs         # Plain C#: subscriptions, formatting, command/event publishing
```

**Rules:**
- Register presenters with VContainer in the feature LifetimeScope.
- Inject the presenter into the view and call `presenter.Bind(this)` from the view's `[Inject]` method.
- Forward `WindowView` lifecycle callbacks to `WindowPresenter<TView>`.
- Keep `IDisposable` subscriptions in the presenter. Views should expose small APIs such as `SetCount(...)`, `SetInteractable(...)`, or `AddClickListener(...)`.

---

## 6. Code Style & Naming

### Language

- **C# 9.0**, block-scoped namespaces (not file-scoped)
- `readonly struct` for events and value objects (never `record`)
- `readonly` fields for injected dependencies
- One type per file
- Always use braces (no single-line `if` without braces)

### Naming conventions

| Category | Pattern | Example |
|---|---|---|
| MonoBehaviour view | `XxxView` | `SafeAreaView`, `InputBlockerView` |
| UI window | `XxxWindow` (extends `WindowView`) | `PlinkoWindow` |
| Presenter | `XxxPresenter` (plain C#) | `PlinkoPresenter` |
| Aggregate root / state | `XxxModel` | `CameraModel`, `PlinkoModel` |
| Entity | `XxxEntity` | `BallEntity` |
| Service (stateless) | `XxxService` | `CameraService`, `TimeService` |
| View-bound service | `XxxViewService` | `HudViewService` |
| Event (`readonly struct`) | `XxxEvent` | `WindowOpenedEvent`, `ScreenChangedEvent` |
| Interface | `IXxx` | `ICameraService`, `IBlurService` |
| DI installer | `XxxLifetimeScope` | `RootLifetimeScope`, `PlinkoLifetimeScope` |
| Enum | `XxxType` / `XxxId` / `XxxLayer` | `TransitionType`, `CameraId`, `UILayer` |
| Attribute | `XxxAttribute` | `ButtonAttribute` |
| Private field | `_camelCase` | `_model`, `_uiService` |
| Addressable UI key | `Views/{TypeName}.prefab` | `Views/PlinkoWindow.prefab` |

---

## 7. Required Dependencies

| Package | Purpose | Required |
|---|---|---|
| **VContainer** | Dependency injection container | Yes |
| **UniTask** | `async`/`await` for Unity | Yes |
| **R3** | Reactive extensions (disposables, observables) | Yes |
| **MessagePipe** | Pub/sub event messaging | Yes |
| **Unity Addressables** | Async asset loading | Yes |
| **Unity Input System** | Modern input handling | Yes |
| **URP (Universal Render Pipeline)** | Blur/rendering features | Yes |
| **Unity Timeline** | Loop track support | Optional |

Install these via Unity Package Manager or your project's `manifest.json` before
importing `com.stickerfwk.core`.
