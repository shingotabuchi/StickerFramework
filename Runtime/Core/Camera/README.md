# Camera System

The framework camera system manages all cameras in the project from script. Scenes do **not** author cameras; profiles do.

The activation model is intentionally minimal: **push a profile and every camera it declares renders. Pop the profile and they go away.** There is no per-camera lease, no mode flag, no activation policy — the only mechanism for turning cameras on or off is profile push/pop.

## Concepts

### `CameraId`
A small enum identifying every camera role used in the project (`Background`, `World`, `UI`, `WorldOverlay`, `UIOverlay`, `Wipe`). Adding a new role = add an enum entry + reference it from a profile.

### `CameraDefinition`
A serialized blob describing one camera: `CameraId`, render type (`Base` / `Overlay`), depth, culling mask, clear flags, etc. Depth determines stack order; render type determines whether a camera draws its own frame (Base) or is composited into a Base's stack (Overlay). Definitions live in `CameraSystemSettings._cameraDefinitions` — there is exactly one definition per `CameraId` for the whole project, and profiles only reference cameras by id.

### `CameraProfile` (ScriptableObject)
A list of `CameraId`s plus a `CameraProfileId`. Profiles are the unit of push/pop. They do not carry per-camera settings — those are owned by `CameraSystemSettings`.

### `CameraSystemSettings` (ScriptableObject)
The catalogue of all profiles (`_profiles`) **and** all per-camera settings (`_cameraDefinitions`) in the project. Referenced by `RootLifetimeScope`. `OnValidate` warns (does not throw) when two `CameraDefinition` entries share a `CameraId`.

### `CameraProfileId`
- `Root` — minimal always-pushed profile. References `Background` and `Wipe`. Pushed by `RootLifetimeScope` on app start so transitions can happen even when no scene is loaded.
- `Gameplay` — gameplay scene profile. References `World`, `UI`, `WorldOverlay`, `UIOverlay`. Pushed by `StickerGameLifetimeScope` while the game scene is active.

## Service

### `ICameraProfileService`
```csharp
IDisposable Push(CameraProfileId id);
bool IsActive(CameraProfileId id);
bool TryGetDefinition(CameraId id, out CameraDefinition def);
IReadOnlyCollection<CameraProfileId> ActiveProfiles { get; }
```

Refcount-based at two levels:

1. **Per-profile refcount.** `Push(id)` increments and returns an `IDisposable` profile handle. Disposing the handle decrements. The first push of a profile materialises its cameras; the last release tears them down.
2. **Per-camera refcount.** When two active profiles both reference the same `CameraId`, the camera is created once and survives until **both** profiles release. Both profiles share the single `CameraDefinition` registered in `CameraSystemSettings`.

## Resolution

After every Push/Pop, `CameraProfileService.Recompute()` builds a `CameraSlot` (id, render type, depth) for every currently registered camera and hands the list to `CameraStackResolver.Resolve(...)`:

1. **Pick winning base.** Among Base-type slots, the one with the **lowest depth** wins.
2. **Build enabled set + stack.** All slots are enabled. Overlay slots are sorted by depth ascending and replace the winning Base's `cameraStack`.
3. **Apply.** Set `gameObject.activeSelf` and `Camera.enabled` per camera (winning base on, losing bases off, all overlays on).

Only **one Base camera renders at a time** — losing Bases are forced off. This is what makes the `Background` ↔ `World` handoff work: while only `Root` is active, `Background` is the winning base; once `Gameplay` is also pushed, `World` (depth=-1) beats `Background` (depth=100) and `Background` is disabled. When `Gameplay` is popped, `Background` becomes the winner again.

## Lifecycle Walkthrough

Boot (only `Root` pushed):
- Cameras: `Background`, `Wipe`. Both render. `Background` is the winning base; `Wipe` overlays it.

Game scene loaded (`Gameplay` pushed on top of `Root`):
- Cameras: `Background`, `Wipe`, `World`, `UI`, `WorldOverlay`, `UIOverlay`. All render.
- `World` wins the base slot; `Background` is forced off.
- Stack on `World`: `[UI, WorldOverlay, UIOverlay, Wipe]` (sorted by depth ascending).

Game scene unloaded (Gameplay's profile handle disposed):
- `Gameplay`-only cameras (`World`, `UI`, `WorldOverlay`, `UIOverlay`) destroyed.
- `Background` becomes the winning base again. `Wipe` still overlays it.

## Pushing a profile from a scope

```csharp
public class MyLifetimeScope : LifetimeScope
{
    [SerializeField] CameraProfileId _cameraProfileId;
    System.IDisposable _cameraProfileHandle;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterBuildCallback(container =>
        {
            _cameraProfileHandle = container
                .Resolve<ICameraProfileService>()
                .Push(_cameraProfileId);
        });
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _cameraProfileHandle?.Dispose();
        _cameraProfileHandle = null;
    }
}
```

## Tests

`CameraStackResolver` (pure C#) is the testable core. See `Assets/Tests/EditMode/Camera/CameraStackResolverTests.cs` for:
- Single profile, single base
- Multiple bases (lower depth wins, others disabled)
- Overlay stack sorted by depth
- No base case
- Multi-profile composition (Root → Root+Gameplay handoff)

`CameraProfileService` integration tests (`CameraProfileServiceTests.cs`) cover refcount semantics:
- Push registers cameras + activates the profile
- Push the same profile twice → cameras created once
- Pop one of two profiles → keeps the survivor's cameras
- Two profiles sharing a `CameraId` dedup it (one camera, destroyed when both release)
- `Dispose` pops all active profiles
