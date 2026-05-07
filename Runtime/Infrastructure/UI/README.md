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
- **Replace**: Close the current top window and push a new one.
- **PopAll**: Clear all windows from a layer.
- **IsOpen\<T\>** / **GetWindow\<T\>**: Query whether a specific window type is open.
- **GetStackCount**: Check the stack size for a given layer.
- Each layer maintains its own independent stack.

> `Pop(WindowView)` removes a buried window without playing its hide transition (it isn't visible) but still fires `OnBeforeHide` / `OnHide` and publishes `WindowClosedEvent`. Top-of-layer pops use the normal transition path.

## R3: Fixed Layer System

Predefined layers with fixed sort orders (`UILayer` enum). Each layer has its own Canvas with a dedicated sorting order, created by `UILayerManager` at initialization:

| Layer | Sort Order | Purpose |
|-------|-----------|---------|
| **Background** | 0 | Full-screen backgrounds, skyboxes |
| **HUD** | 100 | Always-visible gameplay UI (health, score) |
| **Window** | 200 | Standard menu screens (settings, inventory) |
| **Popup** | 300 | Popups, tooltips, notifications |
| **Modal** | 400 | Confirmation dialogs, blocking overlays |
| **Overlay** | 500 | System-level (loading screens, fade) |

- Windows specify which layer they belong to via the `Layer` field on `WindowView`.
- Layer Canvases are created once at system initialization under a `[UI Root]` GameObject (DontDestroyOnLoad) and persist for the app lifetime.
- Each Canvas is configured with `ScreenSpaceOverlay` render mode, a `CanvasScaler` (1920×1080 reference, 0.5 match), and a `GraphicRaycaster`.

## R4: Modal / Input Blocking

- Windows with `IsBlocking = true` place a semi-transparent overlay (`InputBlocker`) behind the window that intercepts raycasts.
- The blocker is a full-screen `Image` (color `rgba(0, 0, 0, 0.5)`) with `raycastTarget = true`.
- Windows can opt out of blocking by setting `IsBlocking = false` on the `WindowView` inspector or via `WindowOptions` at runtime.
- When a blocking window opens, interaction on the previous top window is disabled. When it closes, interaction is re-enabled.

## R5: Transition Animations

- Windows define **show** and **hide** transitions via inspector fields on `WindowView` (`ShowTransition`, `HideTransition`, `TransitionDuration`).
- Built-in transition types (`TransitionType` enum):
  - **None** — Instant visibility change
  - **Fade** — Alpha fade in/out via `CanvasGroup`
  - **SlideFromLeft / SlideFromRight / SlideFromTop / SlideFromBottom** — Position animation with easing
  - **Scale** — Scale (0.85→1.0) combined with alpha fade
  - **Animator** — Plays an Animator state (configured via `ShowAnimatorState` / `HideAnimatorState` fields)
  - **Timeline** — Plays a `PlayableDirector` timeline (configured via `ShowTimeline` / `HideTimeline` fields)
- All transitions implement the `ITransition` interface and are created via `TransitionFactory`.
- Async helper extensions: `AnimatorExtensions.PlayAsync()` and `PlayableDirectorExtensions.PlayAsync()`.
- Transitions are async (`UniTask`-based) and support cancellation.
- During a transition, input to the transitioning window is disabled.
- Runtime overrides for transition type and duration are supported via `WindowOptions`.

## R6: Addressable Loading

- UI prefabs are loaded asynchronously via `IAssetRequester` (interface in `Core/`), implemented by `AddressableCache` (in `Infrastructure/AssetManagement/`).
- `AddressableCache` ref-counts loaded assets and prevents duplicate concurrent requests via `KeyedOperationGate`.
- Each window type maps to an Addressable key (e.g., `"UI/CounterWindow"`).
- Loading shows no intermediate state by default (the window appears after load + transition in).
- The system handles load failures gracefully (logs error, does not break the stack).
- Asset handles are released when windows are disposed.

## R7: Dependency Injection (Child LifetimeScope)

- Each window instance gets its own **child LifetimeScope** scoped to the window's lifetime.
- The child scope is created when the window is instantiated and disposed when the window is destroyed.
- Window-specific services are registered in the child scope.
- The window's View (MonoBehaviour) is injected via `VContainer.InjectGameObject()`.

## R8: MessagePipe Events

The UI system publishes events (readonly structs) for system-level notifications:

- `WindowOpenedEvent(string Key, UILayer Layer)` — fired after a window finishes its show transition.
- `WindowClosedEvent(string Key, UILayer Layer)` — fired after a window finishes its hide transition and is destroyed.

These events are registered as MessagePipe brokers in `RootLifetimeScope`. Other features can subscribe to react to UI state changes without coupling to `UIService` directly.

## R9: Window Configuration

Each window defines its configuration via inspector fields on the `WindowView` base class:

- **Layer** (`UILayer`) — Which layer it belongs to.
- **IsBlocking** (`bool`) — Whether it blocks input behind it (default: `true`).
- **ShowTransition / HideTransition** (`TransitionType`) — Which transition to use.
- **TransitionDuration** (`float`) — Duration of transition animations in seconds.
- **ShowAnimatorState / HideAnimatorState** (`string`) — Animator state names (for `Animator` transition type).
- **ShowTimeline / HideTimeline** (`PlayableDirector`) — Timeline references (for `Timeline` transition type).

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
