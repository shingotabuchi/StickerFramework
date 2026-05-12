# Feature Setup Guide

This guide shows how to scaffold a new feature using StickerFramework. Use it as a prompt:
> "Set up the minimum necessary classes for a **plinko** feature."

---

## Feature Folder Structure

Every feature lives in its own folder under `Assets/Scripts/Features/{FeatureName}/`:

```
Assets/Scripts/Features/Plinko/
  PlinkoLifetimeScope.cs      <- DI registration (VContainer)
  PlinkoModel.cs               <- State / data
  PlinkoPresenter.cs           <- Presentation logic bound to the view
  PlinkoWindow.cs              <- UI (extends WindowView)
  PlinkoEvents.cs              <- MessagePipe event structs
  Plinko.asmdef                <- Assembly definition (optional, recommended for large features)

Assets/Scenes/Plinko/
  Plinko.unity                 <- Scene file
  PlinkoSceneEntry.cs          <- Scene bootstrap MonoBehaviour

Assets/Addressables/Views/
  PlinkoWindow.prefab           <- Addressable UI prefab (key: "Views/PlinkoWindow.prefab")
```

---

## Minimum Classes

### Where State and Logic Go

Before adding a class, decide what kind of state or logic it owns:

| If you are adding... | Put it in... | Notes |
|---|---|---|
| Durable gameplay state / 状態管理 | Model/entity/value object | The source of truth for rules and progress. Pure C#, no Unity dependencies. |
| Business rule decision | Domain model/entity first | Application services can orchestrate, but rules should be testable without Unity. |
| Use-case flow | Application or feature service | Coordinates repositories, commands, and domain events. Keep durable state in models. |
| UI formatting or button command translation | Presenter | Plain C# bound to a view via `Presenter<TView>` or `WindowPresenter<TView>`. |
| Drag/layout/animation orchestration | Presentation service | May hold transient interaction state; not the source of truth for game progress. |
| Text/image/component assignment | View | MonoBehaviour with serialized references and display/input APIs only. |
| Cache, registry, lock, resource handle | Infrastructure service | Technical state, not feature business state. |

`XxxService` can be stateful when it owns workflow, interaction, or technical state. It should not own durable gameplay truth; use a model/entity for that.

### 1. Model — State and Data

Plain C# class. Holds feature state. No MonoBehaviour, no Unity dependencies. Injected into the Presenter.

```csharp
namespace App.Features.Plinko
{
    public class PlinkoModel
    {
        public int Score { get; set; }
        public int BallsRemaining { get; set; } = 5;
        public bool IsDropping { get; set; }
    }
}
```

If you need master data (designer-tunable values), define a `MasterData<T>` subclass:

```csharp
using StickerFwk.Core.MasterData;
using UnityEngine;

namespace App.Features.Plinko
{
    [System.Serializable]
    public class PlinkoMasterData : MasterData<PlinkoMasterData>
    {
        [SerializeField] float _dropForce = 5f;
        [SerializeField] int _pointsPerSlot = 100;

        public float DropForce => _dropForce;
        public int PointsPerSlot => _pointsPerSlot;
    }
}
```

### 2. Events — Cross-Feature Communication

Define as `readonly struct`. One file per feature, multiple events inside.

```csharp
namespace App.Features.Plinko
{
    public readonly struct PlinkoScoreChangedEvent
    {
        public readonly int NewScore;
        public PlinkoScoreChangedEvent(int newScore) { NewScore = newScore; }
    }

    public readonly struct PlinkoBallLandedEvent
    {
        public readonly int SlotIndex;
        public readonly int Points;
        public PlinkoBallLandedEvent(int slotIndex, int points)
        {
            SlotIndex = slotIndex;
            Points = points;
        }
    }
}
```

### 3. Presenter — Presentation Logic

Plain C# class. Receives services and model via constructor injection. Owns view-specific logic such as input command translation, display formatting, and subscriptions. Bind it to a view with `Presenter<TView>` or `WindowPresenter<TView>`.

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using StickerFwk.Core;
using StickerFwk.Core.Presentation;
using StickerFwk.Core.UI;

namespace App.Features.Plinko
{
    public class PlinkoPresenter : WindowPresenter<PlinkoWindow>
    {
        readonly PlinkoModel _model;
        readonly IUIService _uiService;
        readonly IInputLockService _inputLockService;
        readonly IPublisher<PlinkoScoreChangedEvent> _scorePublisher;
        IDisposable _dropSubscription;

        public PlinkoPresenter(
            PlinkoModel model,
            IUIService uiService,
            IInputLockService inputLockService,
            IPublisher<PlinkoScoreChangedEvent> scorePublisher)
        {
            _model = model;
            _uiService = uiService;
            _inputLockService = inputLockService;
            _scorePublisher = scorePublisher;
        }

        public bool CanDrop => _model.BallsRemaining > 0 && !_model.IsDropping;

        public override UniTask InitializeAsync(CancellationToken ct)
        {
            UpdateView();
            return UniTask.CompletedTask;
        }

        public override void OnShow()
        {
            _dropSubscription = View.AddDropListener(OnBallDropRequested);
        }

        public override void OnHide()
        {
            ReleaseDropSubscription();
        }

        void OnBallDropRequested()
        {
            if (!CanDrop)
            {
                return;
            }

            _model.BallsRemaining--;
            _model.IsDropping = true;
            UpdateView();
        }

        public void OnBallLanded(int slotIndex, int points)
        {
            _model.IsDropping = false;
            _model.Score += points;
            _scorePublisher.Publish(new PlinkoScoreChangedEvent(_model.Score));
            UpdateView();
        }

        public async UniTask OnGameOver(CancellationToken ct)
        {
            // Example: push a result window
            await _uiService.Push<PlinkoResultWindow>(ct: ct);
        }

        void UpdateView()
        {
            if (!IsBound)
            {
                return;
            }

            View.SetScore(_model.Score);
            View.SetBallsRemaining(_model.BallsRemaining);
            View.SetDropInteractable(CanDrop);
        }

        protected override void OnDispose()
        {
            ReleaseDropSubscription();
        }

        void ReleaseDropSubscription()
        {
            _dropSubscription?.Dispose();
            _dropSubscription = null;
        }
    }
}
```

### 4. Window (View) — UI Display

Extends `WindowView<TSelf, TPresenter>`. Thin — holds serialized UI references, displays values provided by the presenter, and exposes input subscriptions. The generic base **automatically forwards every lifecycle hook** (`OnInitialize`, `OnBeforeShow`, `OnShow`, `OnBeforeHide`, `OnHide`, `OnDispose`) to the presenter, so you can't accidentally forget to dispose the presenter and leak it. The presenter is auto-injected by VContainer — no manual `BindPresenter` call required, just register the presenter type in your `LifetimeScope`.

```csharp
using System;
using StickerFwk.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace App.Features.Plinko
{
    public class PlinkoWindow : WindowView<PlinkoWindow, PlinkoPresenter>
    {
        [SerializeField] Text _scoreText;
        [SerializeField] Text _ballsText;
        [SerializeField] CoolButton _dropButton;

        public IDisposable AddDropListener(Action listener)
        {
            return _dropButton.AddClickListener(listener);
        }

        public void SetScore(int score)
        {
            _scoreText.text = $"Score: {score}";
        }

        public void SetBallsRemaining(int ballsRemaining)
        {
            _ballsText.text = $"Balls: {ballsRemaining}";
        }

        public void SetDropInteractable(bool interactable)
        {
            _dropButton.Interactable = interactable;
        }
    }
}
```

**Prefab setup:**
1. Create a UI prefab with `PlinkoWindow` component attached
2. It auto-requires `CanvasGroup` (from `WindowView`)
3. Configure in Inspector: Layer = `UI` (or `UIOverlay` / `Wipe`), ShowTransition = `Fade` (or any `ITransition` subclass), etc.
4. Mark as Addressable with key `Views/PlinkoWindow.prefab`

### 5. LifetimeScope — DI Registration

Registers feature-specific types. Inherits from parent (Root) scope to access global services.

```csharp
using MessagePipe;
using StickerFwk.Core;
using VContainer;
using VContainer.Unity;

namespace App.Features.Plinko
{
    public class PlinkoLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.UseScopeCancellation(this);

            // Model (singleton within this feature scope)
            builder.Register<PlinkoModel>(Lifetime.Scoped);

            // Presenter
            builder.Register<PlinkoPresenter>(Lifetime.Transient);

            // MessagePipe events
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<PlinkoScoreChangedEvent>(options);
            builder.RegisterMessageBroker<PlinkoBallLandedEvent>(options);
        }
    }
}
```

`UseScopeCancellation(this)` registers `IScopeCancellation`, a scope-bound token
for presenters/services. Use `IScopeCancellation.Token` for fire-and-forget
operations tied to scene lifetime, and `CreateLinked()` for restartable loops
that should cancel on either manual stop or scope teardown.

**Scene setup:** Attach this LifetimeScope to a GameObject in your scene. Set `Parent` to the Root LifetimeScope (either via Inspector reference or `autoRun` with `parentReference`).

### 6. Scene Entry Point — Bootstrap

A MonoBehaviour placed in the scene that signals readiness after initialization.

```csharp
using StickerFwk.Core;
using VContainer;

namespace App.Features.Plinko
{
    public class PlinkoSceneEntry : UnityEngine.MonoBehaviour
    {
        [Inject] SceneReadyNotifier _sceneReadyNotifier;

        void Start()
        {
            // Signal that the scene is ready (allows screen transition to reveal)
            _sceneReadyNotifier.NotifyReady();
        }
    }
}
```

---

## Scene-bound start views (Title pattern)

> **When to use:** the small number of views that are placed directly in a scene's
> hierarchy and exist before a UI stack is meaningful — chiefly **start scenes
> like Title**, where the view is the first thing visible after the scene loads
> and there is no transition-driven push semantics.
>
> **For everything else, prefer `WindowView` + `IUIService.Push<T>()`.** That gets
> you show/hide transitions, layer routing, blocking semantics, and auto-pop on
> scope dispose — none of which a scene-bound view provides.

The framework deliberately does **not** ship a parallel `SceneBoundView` hierarchy
for this case. Scene-bound views are rare and their lifecycle is naturally
expressed in MonoBehaviour callbacks, so the pattern is:

- The view is a plain `MonoBehaviour` (not a `WindowView`).
- The presenter extends the existing `Presenter<TView>` base from
  `StickerFwk.Core.Presentation` (which already provides `Bind` / `Unbind` /
  `Dispose` and the `View` accessor).
- The view receives the presenter through a VContainer `[Inject]` method, calls
  `Bind`, then drives `InitializeAsync` from `Start` and `Dispose` from
  `OnDestroy`.
- The scene's `LifetimeScope` registers both the view (via
  `RegisterComponentInHierarchy`) and the presenter.

That's the entire pattern. Roughly twenty lines of glue per view, no new
framework types.

### Example: Title

```csharp
// TitleView.cs — scene-resident MonoBehaviour
using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

public class TitleView : MonoBehaviour
{
    [SerializeField] CoolButton _startButton;
    [SerializeField] TMP_Text _reviveCountText;

    TitlePresenter _presenter;

    public Action OnStartButtonPressed;

    [Inject]
    public void Construct(TitlePresenter presenter)
    {
        _presenter = presenter;
        _presenter.Bind(this);
    }

    void Awake()
    {
        _startButton.AddClickListener(() => OnStartButtonPressed?.Invoke());
    }

    async void Start()
    {
        try
        {
            await _presenter.InitializeAsync(this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException) { }
    }

    void OnDestroy()
    {
        _presenter?.Dispose();
        _presenter = null;
    }

    public void SetReviveCount(int count) =>
        _reviveCountText.text = $"第{count + 1}回";
}
```

```csharp
// TitlePresenter.cs — extends Presenter<TView>; scene-bound presenters add
// their own InitializeAsync because there is no UI stack pushing the view.
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.Presentation;

public class TitlePresenter : Presenter<TitleView>
{
    readonly PlayerDataService _playerData;

    public TitlePresenter(PlayerDataService playerData) { _playerData = playerData; }

    protected override void OnBind(TitleView view)
    {
        view.OnStartButtonPressed += HandleStartButtonPressed;
    }

    protected override void OnUnbind(TitleView view)
    {
        view.OnStartButtonPressed -= HandleStartButtonPressed;
    }

    public UniTask InitializeAsync(CancellationToken ct)
    {
        View.SetReviveCount(_playerData.GetReviveCount());
        return UniTask.CompletedTask;
    }

    void HandleStartButtonPressed() { /* ... */ }
}
```

```csharp
// TitleScope.cs — feature LifetimeScope
using VContainer;
using VContainer.Unity;

public class TitleScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // RegisterComponentInHierarchy fires the view's [Inject] hook during
        // scope build, which resolves and binds the presenter.
        builder.RegisterComponentInHierarchy<TitleView>();
        builder.Register<TitlePresenter>(Lifetime.Scoped);
    }
}
```

> **Caveat:** because there is no UI stack involved, navigating *away* from a
> scene-bound view is the application's responsibility (typically by triggering
> an `ISceneTransitionService.TransitionToSceneAsync(...)` from the presenter
> in response to a button press). When the scene unloads, `OnDestroy` fires
> dispose.

---

## Root LifetimeScope (One Per App)

Your app needs a single root scope that registers all framework services. Create this once:

```csharp
using MessagePipe;
using StickerFwk.Core;
using StickerFwk.Core.AssetManagement;
using StickerFwk.Core.Initialization;
using StickerFwk.Core.MasterData;
using StickerFwk.Core.Rendering;
using StickerFwk.Core.UI;
using StickerFwk.Infrastructure.Camera;
using StickerFwk.Infrastructure.Initialization;
using StickerFwk.Infrastructure.Input;
using StickerFwk.Infrastructure.MasterData;
using StickerFwk.Infrastructure.Rendering;
using StickerFwk.Infrastructure.SceneManagement;
using StickerFwk.Infrastructure.Time;
using StickerFwk.Infrastructure.UI;
using VContainer;
using VContainer.Unity;

public class RootLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // --- MessagePipe ---
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<WindowOpenedEvent>(options);
        builder.RegisterMessageBroker<WindowClosedEvent>(options);
        builder.RegisterMessageBroker<InputLockChangedEvent>(options);
        builder.RegisterMessageBroker<CameraRegisteredEvent>(options);
        builder.RegisterMessageBroker<ScreenChangedEvent>(options);
        builder.RegisterMessageBroker<BlurTransitionEvent>(options);

        // --- Core Services ---
        builder.Register<SceneReadyNotifier>(Lifetime.Singleton);

        // --- Infrastructure Services ---
        // Assets
        builder.Register<IAssetRequester, AddressableCache>(Lifetime.Singleton);

        // UI
        builder.Register<IUIService, UIService>(Lifetime.Singleton).AsImplementedInterfaces();
        builder.Register<IScreenTransitionService, ScreenTransitionService>(Lifetime.Singleton);

        // Input
        builder.Register<IRawInputService, InputService>(Lifetime.Singleton);
        builder.Register<IInputService, LockingInputService>(Lifetime.Singleton);
        builder.Register<IInputLockService, InputLockService>(Lifetime.Singleton);

        // Camera
        builder.Register<ICameraService, CameraService>(Lifetime.Singleton).AsImplementedInterfaces();

        // Time
        builder.Register<TimeService>(Lifetime.Singleton);
        builder.Register<ITimeService, TimeService>(Lifetime.Singleton);

        // Scene
        builder.Register<ISceneTransitionService, SceneTransitionService>(Lifetime.Singleton);

        // Master Data
        builder.Register<IMasterDataRepository, MasterDataRepository>(Lifetime.Singleton);

        // Initialization — pipeline is registered automatically the first time you add a task.
        // Each Use*/AddInit* call is independent; mix and match what your project needs.
        builder.UseTargetFrameRate(60);   // Bootstrap phase: sets Application.targetFrameRate
        builder.UseMasterDataInit();      // Load phase: loads IMasterDataRepository
        // builder.AddInitTask<MyCueSheetLoadTask>();   // Project-defined Load-phase task
        // builder.AddInitTask<MyRepoInitTask>();       // Project-defined Warmup-phase task
        // builder.AddInitObserver<MyLoadingGuard>();   // Wraps the whole pipeline (e.g., input lock)

        // Rendering
        builder.Register<IBlurService, BlurService>(Lifetime.Singleton);

        // Screen
        builder.RegisterEntryPoint<ScreenService>();
    }
}
```

---

## How to Navigate Between Features

### Transitioning to a feature's scene:

```csharp
// From any Presenter that has ISceneTransitionService injected:
await _sceneTransitionService.TransitionToSceneAsync(
    "Plinko",                    // scene name
    transitionViewTag: "fade",   // optional: transition style
    beforeLoad: async ct =>
    {
        // Optional: cleanup before the old scene unloads.
        // immediate: true skips hide transitions (we're leaving the scene anyway)
        // so we don't pay 0.3s × stack depth before the load can start.
        await _uiService.PopAll(UILayer.UI, immediate: true, ct);
    });
```

### Opening a feature's window (without scene change):

```csharp
// Push a window onto the stack
var window = await _uiService.Push<PlinkoWindow>();

// Push with options
var window = await _uiService.Push<PlinkoWindow>(options: new WindowOptions
{
    ShowTransition = new SlideTransition { SlideDirection = SlideTransition.Direction.Bottom },
    TransitionDuration = 0.5f,
    Inject = _childScope.Container.InjectGameObject,  // inject from feature scope
});

// Pop it later
await _uiService.Pop<PlinkoWindow>();
```

---

### Pushing windows with args

When a window needs data before its `InitializeAsync` runs, implement `IWindowWithArgs<TArgs>`:

```csharp
public class MatchStartViewArgs
{
    public string OpponentName { get; set; }
    public int RoundNumber { get; set; }
}

public class MatchStartWindow : WindowView<MatchStartWindow, MatchStartPresenter>, IWindowWithArgs<MatchStartViewArgs>
{
    public MatchStartViewArgs Args { get; private set; }

    public void SetArgs(MatchStartViewArgs args) => Args = args;
}
```

Then push with the typed overload. `SetArgs` is called after VContainer injection and **before** `OnInitialize`, so presenters can read `Args` inside `InitializeAsync`:

```csharp
var args = new MatchStartViewArgs { OpponentName = "Player2", RoundNumber = 3 };
var window = await _uiService.Push<MatchStartWindow, MatchStartViewArgs>(args);

// Replace the top window and pass new args:
var window = await _uiService.Replace<MatchStartWindow, MatchStartViewArgs>(args);
```

---

### Push handle, push below, push prepared

Use `PushWithHandle` when ownership should stay with the caller and close the exact instance later:

```csharp
var handle = await _uiService.PushWithHandle<ResultWindow>(options: windowOptions, ct: ct);
await handle.PopAsync(ct);          // plays hide transition
await handle.PopImmediateAsync(ct); // skips hide transition
```

Use `PushBelow` for compound flows where the new window should be the logical top of the stack but render below an existing covering window:

```csharp
var animation = await _uiService.PushWithHandle<GachaAnimationWindow>(options: nonBlockingOptions, ct: ct);
var result = await _uiService.PushBelow<GachaResultWindow>(animation.View, options: nonBlockingOptions, ct: ct);
```

`PushBelow` only reorders siblings when both windows share a parent. Blocking windows create an input blocker sibling; for below-push flows, prefer `WindowOptions { IsBlocking = false }` unless you intentionally manage blocker order.

Use `PushPrepared` to initialize a hidden window, populate it asynchronously, then show it only after preparation succeeds:

```csharp
var swap = await _uiService.PushPrepared<CardSwapWindow>(async (view, token) =>
{
    await view.PopulateAsync(model, token);
}, options: nonBlockingOptions, ct: ct);
```

If preparation is canceled or throws before the show phase, the instantiated GameObject is destroyed and the exception is rethrown.

---

## Checklist: Adding a New Feature

1. **Create feature folder:** `Assets/Scripts/Features/{Name}/`
2. **Model:** `{Name}Model.cs` — plain C# class with state properties
3. **Events:** `{Name}Events.cs` — `readonly struct` event types
4. **Presenter:** `{Name}Presenter.cs` — constructor-injected logic class
5. **Window:** `{Name}Window.cs` — extends `WindowView`, thin display layer
6. **LifetimeScope:** `{Name}LifetimeScope.cs` — registers Model, Presenter, events
7. **Scene (if needed):** `{Name}.unity` + `{Name}SceneEntry.cs` that calls `NotifyReady()`
8. **Prefab:** Create UI prefab with Window component, mark Addressable as `Views/{Name}Window.prefab`
9. **Master data (if needed):** `{Name}MasterData.cs` + ScriptableObject asset labeled `"MasterData"`
10. **Wire up navigation:** Add scene transition or window push from wherever the feature is entered

---

## Naming Conventions

| Type | Pattern | Example |
|---|---|---|
| Feature folder | `Features/{Name}/` | `Features/Plinko/` |
| Model | `{Name}Model` | `PlinkoModel` |
| Presenter | `{Name}Presenter` | `PlinkoPresenter` |
| Window (UI) | `{Name}Window` | `PlinkoWindow` |
| LifetimeScope | `{Name}LifetimeScope` | `PlinkoLifetimeScope` |
| Scene entry | `{Name}SceneEntry` | `PlinkoSceneEntry` |
| Events | `{Name}{Action}Event` | `PlinkoScoreChangedEvent` |
| Master data | `{Name}MasterData` | `PlinkoMasterData` |
| Service interface | `I{Name}Service` | `IPlinkoService` |
| Assembly def | `App.Features.{Name}` | `App.Features.Plinko` |
| Addressable key | `Views/{Name}Window.prefab` | `Views/PlinkoWindow.prefab` |
| Private fields | `_{camelCase}` | `_model`, `_scorePublisher` |
| Namespaces | `App.Features.{Name}` | `App.Features.Plinko` |

---

## Quick Reference: Framework Services Available via DI

| Interface | What It Does |
|---|---|
| `IUIService` | Push/Pop/Replace windows on layer stacks (override per-scope by registering `ScopedUIService` `As<IUIService>()` for auto-pop on scope dispose) |
| `IScreenTransitionService` | Full-screen overlay transitions |
| `ISceneTransitionService` | Load scenes with screen cover + input lock |
| `IInputService` | Pointer position, press state (respects locks) |
| `IRawInputService` | Raw input (ignores locks) |
| `IInputLockService` | Lock/unlock all input (`Lock()` returns `IDisposable`) |
| `ICameraService` | Register/query cameras by ID |
| `ITimeService` | DeltaTime, TimeScale, LocalTimeScale |
| `IAssetRequester` | Load/preload Addressable assets |
| `IMasterDataRepository` | Query loaded master data by type and ID |
| `IBlurService` | Request background blur with easing |
| `IRootInitService` | Await app initialization completion |
| `SceneReadyNotifier` | Signal scene readiness after init |
| `IPublisher<T>` | Publish MessagePipe events |
| `ISubscriber<T>` | Subscribe to MessagePipe events |
