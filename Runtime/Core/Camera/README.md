# Camera System

The framework camera system manages all cameras in the project from script. Scenes do **not** author cameras; profiles do.

## Concepts

### `CameraId`
A small enum identifying every camera role used in the project (`Background`, `World`, `UI`, `WorldOverlay`, `UIOverlay`, `Wipe`). Adding a new role = add an enum entry + reference it from a profile.

### `CameraDefinition`
A serialized blob describing one camera: `CameraId`, render type (`Base` / `Overlay`), depth, culling mask, clear flags, activation policy, etc. Depth determines stack order; render type determines if a camera draws its own frame (Base) or is composited into a Base's stack (Overlay).

### `CameraActivationPolicy`
- `AlwaysOn` — camera is enabled whenever the active mode allows it. Used for cameras that must render every frame (e.g. `World`, `Background`, `WorldOverlay`).
- `OnUsage` — camera is only enabled while at least one consumer holds a lease via `ICameraUsageService.Acquire(id)`. Used for lazily-rendered overlays (e.g. `UI`, `UIOverlay`, `Wipe`) where a window or transition opens a lease for its lifetime.

### `CameraProfile` (ScriptableObject)
A bundle of `CameraDefinition`s + a `CameraProfileId`. Profiles are pushed/popped to control which cameras exist at all.

### `CameraSystemSettings` (ScriptableObject)
The catalogue of all profiles in the project. Referenced by `RootLifetimeScope`.

### `CameraMode`
A runtime mode (`Gameplay`, `GameplayModal`, `Transition`) that filters which `CameraId`s are eligible to render. `CameraModeService.ModeIncludes(mode, id)` is the source of truth for that filter. `Background` is always included in every mode (fallback floor).

### `CameraProfileId`
- `Root` — minimal always-pushed profile. Owns `Background` (Base, AlwaysOn, depth=100) and `Wipe` (Overlay, OnUsage, depth=2). Pushed by `RootLifetimeScope` on app start.
- `Gameplay` — gameplay scene profile. Owns `World` (Base, AlwaysOn, depth=-1), `UI`, `WorldOverlay`, `UIOverlay`. Pushed by `StickerGameLifetimeScope`.

## Services

### `ICameraProfileService`
```csharp
IDisposable Push(CameraProfileId id);
bool IsActive(CameraProfileId id);
bool TryGetDefinition(CameraId id, out CameraDefinition def);
IReadOnlyCollection<CameraProfileId> ActiveProfiles { get; }
```
Refcount-based. **Multiple profiles can be active simultaneously.** Refcounts work at two levels:

1. **Per-profile refcount.** `Push(id)` increments. `Lease.Dispose()` decrements. The first push of a profile materialises its cameras; the last release tears them down.
2. **Per-camera refcount.** When two profiles both declare the same `CameraId`, the camera is created once and survives until **both** profiles release. The first profile's `CameraDefinition` wins (subsequent declarations are silently treated as a refcount bump).

This makes profiles composable: e.g. `Wipe` only ever lives in `Root`, but if it ever appeared in a second profile too, both would share the single camera and it would only be destroyed when both released.

### `ICameraModeService`
```csharp
CameraMode CurrentMode { get; }
event Action<CameraMode> ModeChanged;
void SetMode(CameraMode mode);
bool ModeIncludes(CameraMode mode, CameraId id);
```
Owns the current mode. Mode → camera-set mapping is hardcoded in `CameraModeService.ModeIncludes`.

### `ICameraUsageService`
```csharp
IDisposable Acquire(CameraId id);
bool IsActive(CameraId id);
```
Holds per-camera lease counts. `Acquire` increments and triggers a `Recompute()`; disposing the lease decrements and triggers a `Recompute()`.

`Recompute()` rebuilds the rendering state on every change. It delegates the decision to `CameraStackResolver.Resolve(...)`:

1. For each registered camera, build a `CameraSlot` (id, render type, depth, activation policy, lease count).
2. **Pick winning base.** Walk Base-type slots that are wanted (mode includes them AND `AlwaysOn` OR `LeaseCount > 0`). Lowest depth wins.
3. **Build enabled set + stack.** Walk Overlay-type slots that are wanted; collect them and sort by depth ascending.
4. **Apply.** Set `gameObject.activeSelf` and `Camera.enabled` per camera (winning base on, losing bases off, wanted overlays on, unwanted off). Replace the winning base's `cameraStack` with the sorted overlay list.

Only **one base camera renders at a time** — losing bases are forced off, even if they're `AlwaysOn`. This is what makes the `Background` ↔ `World` handoff work: while only `Root` is active, `Background` is the winning base; once `Gameplay` is also pushed, `World` (depth=-1) beats `Background` (depth=100) and `Background` is disabled.

## Lifecycle Walkthrough

Boot scene (only `Root` profile pushed):
- Cameras registered: `Background`, `Wipe`.
- Mode = `Transition`. Both ids included.
- `Background` is `AlwaysOn` → wanted. `Wipe` is `OnUsage` → wanted only when leased.
- Winning base = `Background`. Stack = `[Wipe]` if leased else `[]`.
- Result: black screen by default; wipe overlays when a transition acquires it.

Game scene loaded (`Gameplay` profile pushed on top):
- Cameras registered: `Background` + `World` + `UI` + `WorldOverlay` + `UIOverlay` + `Wipe`.
- Bases wanted: `Background` (depth=100), `World` (depth=-1). `World` wins; `Background` forced off.
- Overlays wanted: `WorldOverlay` (AlwaysOn), plus `UI` / `UIOverlay` / `Wipe` if leased.
- Stack on `World` = sorted overlays.

Game scene unloaded (Gameplay's lease disposed):
- Per-profile refcount of `Gameplay` → 0.
- Each camera in the profile decrements its per-camera refcount. `World`, `UI`, `WorldOverlay`, `UIOverlay` go to 0 → destroyed + unregistered.
- `Recompute()` runs: only `Background` + `Wipe` remain. We're back to the boot state.

## Pushing a profile from a scope

```csharp
public class MyLifetimeScope : LifetimeScope
{
    [SerializeField] CameraProfileId _cameraProfileId;
    System.IDisposable _cameraProfileLease;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterBuildCallback(container =>
        {
            _cameraProfileLease = container
                .Resolve<ICameraProfileService>()
                .Push(_cameraProfileId);
        });
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _cameraProfileLease?.Dispose();
        _cameraProfileLease = null;
    }
}
```

## Acquiring a camera lease (UI / transitions)

```csharp
// Push window: get a lease for the layer's camera.
_cameraLease = _cameraUsageService.Acquire(CameraId.UI);

// Pop window: dispose to decrement.
_cameraLease?.Dispose();
```

`UIService` handles this automatically per-window. Custom features that want a camera enabled (e.g. a one-shot transition) can call `Acquire` directly and dispose when done.

## Tests

`CameraStackResolver` (pure C#) is the testable core. See `Assets/Tests/EditMode/Camera/CameraStackResolverTests.cs` for coverage of:
- Single profile, single base
- Multiple bases (lower depth wins, others disabled)
- `OnUsage` requires lease, `AlwaysOn` does not
- Mode filtering
- Stack sorted by depth

`CameraProfileService` integration tests cover refcount semantics:
- Pushing the same profile twice creates cameras once
- Disposing one of two leases keeps the profile active
- Two profiles sharing a camera dedup it (created once, destroyed when last profile releases)
- `TryGetDefinition` round-trips
