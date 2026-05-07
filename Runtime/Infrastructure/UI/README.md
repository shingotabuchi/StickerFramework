# UI System

> Status: current. Documents the UI window stack, layer model, transitions, and the scene-canvas binder. Kept in sync with code in `Runtime/Core/UI/` and `Runtime/Infrastructure/UI/`.

## Overview

A general-purpose UI framework for managing windows (menus, HUD, popups, dialogs, etc.) with stack-based navigation, async Addressable loading, layered rendering, and transition animations. Integrates with the existing VContainer + MessagePipe + UniTask architecture.

---

## R1: Window Lifecycle

- Each window is a **prefab** loaded via Addressables at runtime.
- Windows extend the `WindowView` base class (MonoBehaviour).
- Windows follow a lifecycle: **Load → Initialize → Show (transition in) → Active → Hide (transition out) → Dispose**.
- `WindowView` provides virtual lifecycle methods: `OnInitialize()`, `OnBeforeShow()`, `OnShow()`, `OnBeforeHide()`, `OnHide()`, `OnDispose()`.
- Windows may delegate those lifecycle hooks to a `WindowPresenter<TView>` from `StickerFwk.Core.Presentation` to keep view logic out of the MonoBehaviour.
- Windows are instantiated under the appropriate layer Canvas when opened and destroyed when closed.
- The system manages a **window stack** per layer for navigation history.
- Each window instance is tracked internally via a `WindowHandle` that stores the view, asset handle, input blocker, and layer reference.

## R2: Stack-Based Navigation

- **Push**: Open a new window on top of the stack.
- **Pop**: Close the top window of a layer (`Pop(UILayer)`), the topmost window of a given type (`Pop<T>()`), or a specific window instance regardless of position (`Pop(WindowView)`).
- **Replace**: Close the current top window of the new window's layer and push the new one. The target layer is taken from the prefab, so the pop and the push are guaranteed to happen on the same layer.
- **PopAll**: Clear all windows from a layer.
- **IsOpen\<T\>** / **GetWindow\<T\>**: Query whether a specific window type is open.
- **GetStackCount**: Check the stack size for a given layer.
- Each layer maintains its own independent stack.

> `Pop(WindowView)` removes a buried window without playing its hide transition (it isn't visible) but still fires `OnBeforeHide` / `OnHide` and publishes `WindowClosedEvent`. Top-of-layer pops use the normal transition path.

### Concurrency

- `Push` / `Pop` / `Replace` / `PopAll` are safe to call from overlapping async flows. Operations on the **same layer** are serialized through a per-layer FIFO queue so stack ordering, blocker bookkeeping, and `interactable` toggling cannot interleave. Different layers run concurrently.
- Asset loads happen **outside** the layer lock, so two pushes on the same layer can still load their prefabs in parallel; only the stack-mutating + transition phase is serialized.
- `Pop<T>()` and `Pop(WindowView)` need to find the owning layer first; they take a brief global lock for the lookup, then hand off to the per-layer lock.
- Each call honors its own `CancellationToken`. Cancelling one caller (even while it is still queued behind an earlier op) does not affect other queued or running ops.
- `Dispose` cancels all queued and in-flight ops; their tokens fire `OperationCanceledException` and they unwind cleanly before the service tears down its stacks.

## R3: Fixed Layer System

Predefined layers with fixed sort orders (`UILayer` enum, defined in `Runtime/Core/UI/UILayer.cs`). Each layer is bound to a dedicated `CameraId` and gets its own Canvas, created on demand by `UILayerManager`:

| Layer | Sort Order | Bound Camera (`CameraId`) | Purpose |
|-------|-----------|---------------------------|---------|
| **UI** | 100 | `CameraId.UI` | Standard in-game UI (HUD, menus, popups, modals) |
| **UIOverlay** | 200 | `CameraId.UIOverlay` | UI that must render above the main UI camera |
| **Wipe** | 300 | `CameraId.Wipe` | Full-screen scene-transition wipes |

- Windows specify which layer they belong to via the `Layer` field on `WindowView` (default `UILayer.UI`).
- Layer canvases are created **lazily** the first time a window targets that layer (`UILayerManager.TryEnsureLayer`) and parented under a single `[UI Root]` GameObject (DontDestroyOnLoad). The push fails fast if the layer's camera (`CameraId.UI` / `UIOverlay` / `Wipe`) has not been registered yet — apply the appropriate `CameraProfile` first.
- Each Canvas is configured with `RenderMode.ScreenSpaceCamera` bound to the registered camera, `sortingOrder` equal to the layer's integer value, a `CanvasScaler` (1920×1080 reference, 0.5 match), and a `GraphicRaycaster`. Canvases are disabled when their stack becomes empty and re-enabled when the next window pushes onto the layer.
- `UILayerManager` re-binds `Canvas.worldCamera` automatically when a layer's camera is unregistered and a fresh one is registered (e.g. across scene transitions that swap camera profiles).

Need a Canvas authored in the scene (boot splash, version label, debug overlay)? Use `CanvasCameraBinder` instead — see **R11**. The `UILayer` enum is reserved for windows pushed through `IUIService`.

## R4: Modal / Input Blocking

- Windows with `IsBlocking = true` place a semi-transparent overlay (`InputBlocker`) behind the window that intercepts raycasts.
- The blocker is a full-screen `Image` (color `rgba(0, 0, 0, 0.5)`) with `raycastTarget = true`.
- Windows can opt out of blocking by setting `IsBlocking = false` on the `WindowView` inspector or via `WindowOptions` at runtime.
- When a blocking window opens, interaction on the previous top window is disabled. When it closes, interaction is re-enabled.

## R5: Transition Animations

- Windows define **show** and **hide** transitions via `[SerializeReference] ITransition` fields on `WindowView` (`ShowTransition`, `HideTransition`) plus a shared `TransitionDuration`. The Unity inspector renders a subclass picker so each prefab only shows the fields for its chosen transition.
- Built-in `ITransition` implementations (all `[Serializable]`, parameterless ctor, in `StickerFwk.Core.UI`):
  - `NoneTransition` — Instant visibility change
  - `FadeTransition` — Alpha fade in/out via `CanvasGroup`
  - `SlideTransition` — Position animation with easing (`Direction` enum field)
  - `ScaleTransition` — Scale combined with alpha fade (`MinScale` field)
  - `AnimatorTransition` — Plays an `Animator` state (`ShowState` / `HideState` strings). Resolves the `Animator` from the window's root `GetComponent<Animator>()`; if the animator lives on a child, add an `AnimatorTransitionTargets` component on the root that points at it.
  - `TimelineTransition` — Plays show/hide `PlayableDirector`s. Requires a sibling `TimelineTransitionTargets` component on the window root that holds the show/hide director references.
- Custom transitions: implement `ITransition` (parameterless ctor, `[Serializable]`) in any assembly that references `StickerFwk.Core` — Unity's `[SerializeReference]` picker will list it automatically. Note: `[SerializeReference]` data is not remapped to the prefab instance for nested `UnityEngine.Object` references; if your transition needs to reference scene/prefab objects, store those references on a companion `MonoBehaviour` (see `TimelineTransitionTargets`).
- Async helper extensions: `AnimatorExtensions.PlayAsync()` and `PlayableDirectorExtensions.PlayAsync()`.
- Transitions are async (`UniTask`-based) and support cancellation.
- During a transition, input to the transitioning window is disabled.
- Runtime overrides for transition and duration are supported via `WindowOptions`.

## R6: Addressable Loading

- UI prefabs are loaded asynchronously via `IAssetRequester` (interface in `Core/`), implemented by `AddressableCache` (in `Infrastructure/AssetManagement/`).
- `AddressableCache` ref-counts loaded assets and prevents duplicate concurrent requests via `KeyedOperationGate`.
- Each window type maps to an Addressable key (e.g., `"UI/CounterWindow"`).
- Loading shows no intermediate state by default (the window appears after load + transition in).
- The system handles load failures gracefully (logs error, does not break the stack).
- Asset handles are released when windows are disposed.

## R7: Dependency Injection (Per-Push Resolver)

- `UIService` does **not** create a child `LifetimeScope` per window. There is no per-window scope to dispose.
- After instantiating a window prefab, `UIService.PushInternal` calls `resolver.InjectGameObject(instance)` to populate `[Inject]` members on the window's MonoBehaviour and its children.
- The resolver used is, in order of preference:
  1. `WindowOptions.Resolver` if the caller passed one (typically a feature/scene child scope's `IObjectResolver`),
  2. otherwise the `IObjectResolver` that was injected into `UIService` itself (the scope where `UIService` was registered — usually the root scope).
- Feature-specific dependencies are made available to a window by **building a child `LifetimeScope` yourself** and passing its `IObjectResolver` via `WindowOptions.Resolver` on `Push` / `Replace`. The child scope's lifetime, including any services it owns, is the caller's responsibility.
- For automatic teardown of windows pushed from a scope, register a `ScopedUIService` (see **R12**). It wraps `IUIService` and pops tracked windows when the scope disposes — but it still does not create per-window scopes.

## R8: MessagePipe Events

The UI system publishes events (readonly structs) for system-level notifications:

- `WindowOpenedEvent(string Key, UILayer Layer)` — fired after a window finishes its show transition.
- `WindowClosedEvent(string Key, UILayer Layer)` — fired after a window finishes its hide transition and is destroyed.

These events are registered as MessagePipe brokers in `RootLifetimeScope`. Other features can subscribe to react to UI state changes without coupling to `UIService` directly.

## R9: Window Configuration

Each window defines its configuration via inspector fields on the `WindowView` base class:

- **Layer** (`UILayer`) — Which layer it belongs to.
- **IsBlocking** (`bool`) — Whether it blocks input behind it (default: `true`).
- **ShowTransition / HideTransition** (`[SerializeReference] ITransition`) — Transition strategy instances. Pick a subclass in the inspector; per-strategy fields (Animator state names, Timeline directors, slide direction, etc.) appear inline only for the selected type.
- **TransitionDuration** (`float`) — Duration of transition animations in seconds.

Runtime overrides can be passed via a `WindowOptions` object when calling `UIService.Push()` or `UIService.Replace()`.

## R10: Integration Constraints

- Follows existing project conventions (see `CLAUDE.md`).
- MonoBehaviour Views are **thin** — no logic beyond displaying state and forwarding input.
- View-specific logic belongs in plain C# presenters (`Presenter<TView>` / `WindowPresenter<TView>`): subscriptions, UI formatting, command publishing, and lifecycle side effects.
- All async operations use **UniTask**.
- No singletons — `UIService` is registered as a singleton in VContainer (`RootLifetimeScope`).
- `UIService` implements `IStartable` (initializes on app start) and `IDisposable` (cleans up all windows).
- Feature folder: `Assets/Scripts/Runtime/Features/UI/`.
- Cross-feature communication via MessagePipe only.

---

## R11: Scene-Authored Canvases (`CanvasCameraBinder`)

`UILayerManager` owns canvases for windows pushed through `IUIService`. For canvases that are **authored in a scene** (boot splash, version label, debug overlay, anything that should be visible before the runtime UI stack is initialised), use `CanvasCameraBinder` instead.

### Behaviour

- In `Awake`, the binder forces `RenderMode.ScreenSpaceOverlay` if no camera is bound, so the canvas is visible from the first frame even before any `CameraProfile` is pushed.
- On VContainer injection it captures `ICameraService` and subscribes to `CameraRegisteredEvent`. If the target `CameraId` is already registered, it binds immediately.
- On `CameraRegisteredEvent(IsRegistered: true)` for the configured `CameraId`, the canvas switches to `ScreenSpaceCamera` with `worldCamera` set and the configured `planeDistance` applied. Idempotent — no-ops when the camera reference is unchanged.
- On `CameraRegisteredEvent(IsRegistered: false)` it reverts to `ScreenSpaceOverlay` and clears `worldCamera`, so the canvas keeps rendering across profile transitions.
- Disposes its subscription in `OnDestroy`.

### Required wiring

| Concern | Where |
|---|---|
| Push a `CameraProfile` that includes the target `CameraId` | `CameraProfileScopeBinding` on the scope's GameObject |
| Auto-install `IInstaller` MonoBehaviours on a scope | Scope inherits from `StickerLifetimeScope` |
| Auto-inject scene-authored binders | `builder.RegisterComponentInHierarchy<CanvasCameraBinder>()` in `ConfigureScope` |
| Provide `ICameraService` + `ISubscriber<CameraRegisteredEvent>` | Registered in `RootLifetimeScope` (parent scope) |

### Layering

The binder does **not** set `Canvas.sortingOrder`. Stack order between camera-rendered canvases is determined by the cameras' `depth` in `CameraSystemSettings._cameraDefinitions`. A binder targeting `CameraId.UI` (depth 20) renders below `CameraId.Wipe` (depth 50), so wipe transitions correctly draw above an authored boot canvas.

### What it deliberately does not do

- Does not parent your canvas under `[UI Root]`. It stays where you authored it.
- Does not register the canvas with `UIService` / `UILayerManager`. Windows pushed via `IUIService.Push` still go to dynamic layer canvases.
- Does not toggle `Canvas.enabled`. Use a separate component if you need show/hide on profile transitions instead of overlay fallback.
- Does not configure `CanvasScaler` or `GraphicRaycaster` — those are authored in-scene.

> Do not pre-assign `Canvas.worldCamera` in the inspector. The binder owns that field and will overwrite it on bind.

---

## R12: Scope-Aware Window Lifetime (`ScopedUIService`)

Windows pushed via `IUIService` from inside a child `LifetimeScope` should be popped automatically when that scope disposes, so callers don't need a symmetric `Pop` in every teardown path. `ScopedUIService` provides this without changing the `IUIService` API.

### How it works

- `ScopedUIService` is a thin wrapper that takes the concrete root `UIService` and forwards every call to it.
- It tracks every `WindowView` returned by its own `Push` / `Replace` calls.
- On `Dispose` (scope teardown), it iterates tracked views in reverse push order and calls `IUIService.Pop(WindowView)` for each one whose GameObject hasn't already been destroyed (Unity-null check).
- All other calls (`Pop`, `Pop<T>`, `PopAll`, `Preload`, `IsOpen`, `GetWindow`, `GetStackCount`) pass through untouched. Manual gameplay-driven pops still work exactly as before; the wrapper is purely a teardown safety net.

### Wiring

Register the wrapper `As<IUIService>()` in the child scope. VContainer's child-scope resolution shadows the root singleton for any service constructed inside that scope, so consumers keep injecting plain `IUIService` and have no idea the wrapper is in front. To prevent the wrapper resolving back into itself, construct it with the parent scope's `IUIService` via a factory:

```csharp
public class FeatureLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register(_ => new ScopedUIService(Parent.Container.Resolve<IUIService>()),
            Lifetime.Singleton).As<IUIService>();
        // ...other feature services that inject IUIService...
    }
}
```

`Lifetime.Singleton` here means "one instance per this LifetimeScope" — the wrapper is created when the scope builds and disposed when the scope disposes. Resolving `IUIService` from `Parent.Container` skips the registration we are currently defining, so no recursion.

### What stays unchanged

- Services and presenters in the child scope inject `IUIService` as before. They cannot tell whether they got the singleton or the wrapper.
- Direct `Pop` / `Replace` / `PopAll` calls behave identically — the wrapper just delegates and the underlying stack mutates as usual.
- Tracked windows that were popped manually before scope teardown are detected via a Unity-null check on the cached `WindowView` reference and skipped on dispose.
- Root-scope services (registered in `RootLifetimeScope`) still resolve `IUIService` to the singleton; only child scopes that explicitly register a `ScopedUIService` get the wrapper.

### When to add it to a new scope

Register a `ScopedUIService` in any feature/scene `LifetimeScope` whose services push windows that should not survive the scope. If the scope's services never push windows (or always pop them through their own teardown), the registration is unnecessary.
