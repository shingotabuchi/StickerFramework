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

Extends `WindowView`. Thin — holds serialized UI references, displays values provided by the presenter, exposes input subscriptions, and forwards lifecycle hooks to the presenter.

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using StickerFwk.Core.UI;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace App.Features.Plinko
{
    public class PlinkoWindow : WindowView
    {
        [SerializeField] Text _scoreText;
        [SerializeField] Text _ballsText;
        [SerializeField] CoolButton _dropButton;

        PlinkoPresenter _presenter;

        [Inject]
        public void Construct(PlinkoPresenter presenter)
        {
            _presenter = presenter;
            _presenter.Bind(this);
        }

        public override UniTask OnInitialize(CancellationToken ct)
        {
            return _presenter.InitializeAsync(ct);
        }

        protected override void OnShowInternal()
        {
            _presenter.OnShow();
        }

        protected override void OnHideInternal()
        {
            _presenter.OnHide();
        }

        protected override void OnDisposeInternal()
        {
            _presenter.Dispose();
        }

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
3. Configure in Inspector: Layer = `Window`, ShowTransition = `Fade`, etc.
4. Mark as Addressable with key `Views/PlinkoWindow.prefab`

### 5. LifetimeScope — DI Registration

Registers feature-specific types. Inherits from parent (Root) scope to access global services.

```csharp
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace App.Features.Plinko
{
    public class PlinkoLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
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

        // Initialization
        builder.Register<IRootInitService, RootInitService>(Lifetime.Singleton).AsImplementedInterfaces();

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
        // Optional: cleanup before the old scene unloads
        await _uiService.PopAll(UILayer.Window, ct);
    });
```

### Opening a feature's window (without scene change):

```csharp
// Push a window onto the stack
var window = await _uiService.Push<PlinkoWindow>();

// Push with options
var window = await _uiService.Push<PlinkoWindow>(options: new WindowOptions
{
    ShowTransition = TransitionType.SlideFromBottom,
    TransitionDuration = 0.5f,
    Resolver = _childScope.Container,  // inject from feature scope
});

// Pop it later
await _uiService.Pop<PlinkoWindow>();
```

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
| `IUIService` | Push/Pop/Replace windows on layer stacks |
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
