# Camera System

> Status: current. Documents the project-wide camera registration, profiles, and stack resolver. Kept in sync with code in `Runtime/Core/Camera/` and `Runtime/Infrastructure/Camera/`.

The framework camera system manages all cameras in the project from script. Scenes do **not** author cameras; profiles do.

The activation model is intentionally minimal: **push a profile and every camera it declares renders. Pop the profile and they go away.** There is no per-camera lease, no mode flag, no activation policy — the only mechanism for turning cameras on or off is profile push/pop.

## Concepts

### `CameraId`
A small enum identifying every camera role used in the project (`Background`, `World`, `UI`, `WorldOverlay`, `UIOverlay`, `Wipe`). Adding a new role = add an enum entry + reference it from a profile.

### `CameraDefinition`
A serialized blob describing one camera: `CameraId`, depth, culling mask, clear flags, etc. Depth determines both stack order **and** Base/Overlay role: among all currently-active cameras the one with the lowest depth becomes the Base; every other camera becomes an Overlay in the Base's stack, sorted by depth ascending. Definitions live in `CameraSystemSettings._cameraDefinitions` — there is exactly one definition per `CameraId` for the whole project, and profiles only reference cameras by id.

### `CameraProfile` (ScriptableObject)
A list of `CameraId`s plus a `CameraProfileId`. Profiles are the unit of push/pop. They do not carry per-camera settings — those are owned by `CameraSystemSettings`.

### `CameraSystemSettings` (ScriptableObject)
The catalogue of all profiles (`_profiles`) **and** all per-camera settings (`_cameraDefinitions`) in the project. Referenced by `RootLifetimeScope`. `OnValidate` warns (does not throw) when two `CameraDefinition` entries share a `CameraId`.

### `CameraProfileId`
- `Root` — minimal always-pushed profile. References `Wipe`. Pushed by `RootLifetimeScope` on app start so transitions can happen even when no scene is loaded.
- `BackgroundOnly` — pushed when no gameplay scene is active so `Background` becomes the Base. Popped before pushing a profile that owns its own Base camera (e.g. `Gameplay`).
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

After every Push/Pop, `CameraProfileService.Recompute()` builds a `CameraSlot` (id, depth) for every currently registered camera and hands the list to `CameraStackResolver.Resolve(...)`:

1. **Pick winning base.** The slot with the **lowest depth** wins the Base role.
2. **Build enabled set + stack.** All slots are enabled. Non-base slots are sorted by depth ascending and become the winning Base's `cameraStack`.
3. **Apply.** Set `UniversalAdditionalCameraData.renderType` per camera (Base for the winner, Overlay for the rest), then set `gameObject.activeSelf` and `Camera.enabled`.

The Base/Overlay role is **derived purely from depth** — there is no per-camera marker. To make a camera "the Base while no scene is loaded", give it a low depth and isolate it in its own profile (e.g. `BackgroundOnly`) so it isn't active when a scene's profile (with an even-lower-depth Base, like `World`) is pushed.

## Lifecycle Walkthrough

Boot (`Root` + `BackgroundOnly` pushed):
- Cameras: `Wipe`, `Background`. Both render. `Background` (depth=-5) wins the base slot; `Wipe` (depth=2) overlays it.

Game scene loaded (`BackgroundOnly` popped, `Gameplay` pushed on top of `Root`):
- Cameras: `Wipe`, `World`, `UI`, `WorldOverlay`, `UIOverlay`. All render.
- `World` (depth=-10) wins the base slot.
- Stack on `World`: `[UI, WorldOverlay, UIOverlay, Wipe]` (sorted by depth ascending).

Game scene unloaded (`Gameplay` popped, `BackgroundOnly` re-pushed):
- `Gameplay`-only cameras destroyed. `Background` becomes the winning base again.

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
- Single slot becomes the Base
- Lowest-depth slot wins the Base role; all others become overlays sorted by depth
- Empty input → no Base
- Multi-profile composition (Root → Root+Gameplay handoff)

`CameraProfileService` integration tests (`CameraProfileServiceTests.cs`) cover refcount semantics:
- Push registers cameras + activates the profile
- Push the same profile twice → cameras created once
- Pop one of two profiles → keeps the survivor's cameras
- Two profiles sharing a `CameraId` dedup it (one camera, destroyed when both release)
- `Dispose` pops all active profiles
