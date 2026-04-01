# UI System — Requirements

## Overview

A general-purpose UI framework for managing windows (menus, HUD, popups, dialogs, etc.) with stack-based navigation, async Addressable loading, layered rendering, and transition animations. Integrates with the existing VContainer + MessagePipe + UniTask architecture.

---

## R1: Window Lifecycle

- Each window is a **prefab** loaded via Addressables at runtime.
- Windows extend the `WindowView` base class (MonoBehaviour).
- Windows follow a lifecycle: **Load → Initialize → Show (transition in) → Active → Hide (transition out) → Dispose**.
- `WindowView` provides virtual lifecycle methods: `OnInitialize()`, `OnShow()`, `OnHide()`, `OnDispose()`.
- Windows are instantiated under the appropriate layer Canvas when opened and destroyed when closed.
- The system manages a **window stack** per layer for navigation history.
- Each window instance is tracked internally via a `WindowHandle` that stores the view, asset handle, input blocker, and layer reference.

## R2: Stack-Based Navigation

- **Push**: Open a new window on top of the stack.
- **Pop**: Close the top window and return to the previous one.
- **Replace**: Close the current top window and push a new one.
- **PopAll**: Clear all windows from a layer.
- **IsOpen\<T\>** / **GetWindow\<T\>**: Query whether a specific window type is open.
- **GetStackCount**: Check the stack size for a given layer.
- Each layer maintains its own independent stack.

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
- All async operations use **UniTask**.
- No singletons — `UIService` is registered as a singleton in VContainer (`RootLifetimeScope`).
- `UIService` implements `IStartable` (initializes on app start) and `IDisposable` (cleans up all windows).
- Feature folder: `Assets/Scripts/Runtime/Features/UI/`.
- Cross-feature communication via MessagePipe only.
