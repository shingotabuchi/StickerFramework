# Camera System

> Status: current. Documents scene-resident camera registration and base/overlay stack control. Kept in sync with code in `Runtime/Core/Camera/` and `Runtime/Infrastructure/Camera/`.

The framework camera system no longer creates cameras procedurally. Cameras are authored in scenes as normal Unity `Camera` GameObjects and opt into the framework with `ManagedCamera`.

## Core vs Infrastructure split

The camera code is split across two assemblies by **dependency**, not by "is it a MonoBehaviour":

- **`Runtime/Core/Camera/` (`StickerFwk.Core`)** — camera contracts and value types (`ICameraService`, `CameraId`, events), plus **self-contained authoring components that depend only on `UnityEngine` (+ `RenderPipelines.Core`)** and do not implement/require `CameraService`: `CameraFitter`, `CameraBoxFovFitter`, `CameraBackgroundQuad`, `CameraFovLerper`. These just pose/scale a `Transform` or set a single `Camera` property.
- **`Runtime/Infrastructure/Camera/` (`StickerFwk.Infrastructure.Camera`)** — the **URP-dependent service stack**: `CameraService`, `CameraModel`, and `ManagedCamera`. Anything touching `UniversalAdditionalCameraData`, the URP `cameraStack`, or implementing `ICameraService` lives here.

Rule of thumb: if a camera component needs URP (`Universal.Runtime`) or the camera service, it belongs in Infrastructure; otherwise it is a Core authoring helper.

The activation model is intentionally small:

- Scene cameras register by `CameraId` when enabled and unregister when disabled.
- The active Base camera is selected by `SetDefaultBase` plus scoped `PushBase` leases.
- Overlay cameras are enabled by default and can be temporarily hidden with ref-counted `DisableOverlay` leases.
- URP Base/Overlay role is read from each camera's `UniversalAdditionalCameraData.renderType` at registration time.

## Concepts

### `CameraId`

`CameraId` is a serializable value object:

```csharp
[Serializable]
public readonly struct CameraId : IEquatable<CameraId>
```

The framework reserves only three static slots:

| ID | Purpose |
|---|---|
| `CameraId.UI` | Standard UI layer camera |
| `CameraId.UIOverlay` | UI that must render above the main UI camera |
| `CameraId.Wipe` | Full-screen scene-transition wipes |

Projects define additional IDs in project code:

```csharp
public static class CameraIds
{
    public static readonly CameraId Game = new("Game");
    public static readonly CameraId BirdsEye = new("BirdsEye");
}
```

### Scene-resident cameras

Add `ManagedCamera` to each scene-authored camera and assign its `CameraId` in the Inspector. `ManagedCamera` calls:

```csharp
_cameraService.Register(id, camera);   // OnEnable / injection
_cameraService.Unregister(id);         // OnDisable
```

The same `CameraId` may have a different URP render type in different scenes. For example, `CameraId.UI` can be a Base camera in a UI-only boot scene and an Overlay camera in gameplay. The framework does not encode Base/Overlay in the ID; it introspects `UniversalAdditionalCameraData.renderType` on the registered camera.

## Service

### Migration note

The old profile API (`ICameraProfileService`, `CameraProfile`,
`CameraProfileId`, `CameraSystemSettings`) was replaced by scene camera
registration plus a Base/Overlay stack. Register each scene camera with a
stable `CameraId`, select default or temporary Base cameras with
`SetDefaultBase` / `PushBase`, and suppress Overlay cameras with
`DisableOverlay`.

### `ICameraService`

```csharp
void Register(CameraId id, Camera camera);
void Unregister(CameraId id);
Camera GetCamera(CameraId id);
bool TryGetCamera(CameraId id, out Camera camera);
Camera GetRequiredCamera(CameraId id);
Camera GetCameraForRenderer(Renderer renderer);
IReadOnlyList<CameraId> GetRegisteredIds();

CameraId ActiveBase { get; }
event Action<ActiveBaseChangedEvent> ActiveBaseChanged;
void SetDefaultBase(CameraId id);
IDisposable PushBase(CameraId id);

IDisposable DisableOverlay(CameraId id);
```

### Base selection

`SetDefaultBase(id)` sets the bottom of the Base stack. Use it when a scene or scope establishes its normal camera:

```csharp
// On Game scene scope build:
_cameraService.SetDefaultBase(CameraIds.Game);
```

If the ID has not registered yet, the default is queued and applied when that Base camera registers.

`PushBase(id)` temporarily overrides the active Base. It returns a lease; disposing that lease removes only that push. Tokens are handle-based, so disposal order does not matter.

```csharp
IDisposable _birdsEyeLease;

// During gameplay to switch to birds-eye:
_birdsEyeLease = _cameraService.PushBase(CameraIds.BirdsEye);

// To switch back:
_birdsEyeLease.Dispose();
_birdsEyeLease = null;
```

This mirrors `IInputLockService`-style temporary ownership: callers keep the lease they created and release it in their own teardown path.

### Overlay disable leases

Overlay cameras are enabled by default and added to the active Base camera's URP `cameraStack`, sorted by camera depth. `DisableOverlay(id)` returns a ref-counted lease that hides an Overlay while any lease is alive:

```csharp
using var hideWipe = _cameraService.DisableOverlay(CameraId.Wipe);
```

`DisableOverlay` is for overlays only. Base cameras are controlled with `SetDefaultBase` / `PushBase`.

### Active Base notifications

`ActiveBase` exposes the current Base `CameraId`. Whenever it changes, `CameraService` raises the C# event and publishes MessagePipe `ActiveBaseChangedEvent`:

```csharp
public readonly struct ActiveBaseChangedEvent
{
    public readonly CameraId Previous;
    public readonly CameraId Current;
}
```

Use this for systems that need to react to Game ↔ BirdsEye transitions without polling.

## Lifecycle Walkthrough

Game scene loads:

1. Scene cameras with `ManagedCamera` register `CameraIds.Game`, `CameraIds.BirdsEye`, `CameraId.UI`, `CameraId.UIOverlay`, and `CameraId.Wipe` as they enable.
2. The scene scope calls `_cameraService.SetDefaultBase(CameraIds.Game)`.
3. The Game Base is enabled. Registered Overlay cameras are added to Game's URP stack.

Gameplay switches to birds-eye:

```csharp
_birdsEyeLease = _cameraService.PushBase(CameraIds.BirdsEye);
```

- `CameraIds.BirdsEye` becomes the active Base.
- Game remains registered but disabled as a Base.
- Overlay cameras are re-stacked onto BirdsEye.

Gameplay returns to normal:

```csharp
_birdsEyeLease.Dispose();
```

- The BirdsEye push is removed.
- The default Game Base becomes active again.
- Overlay cameras are re-stacked onto Game.

## Scene setup checklist

- Author cameras as scene GameObjects.
- Add `ManagedCamera` to each framework-managed camera.
- Assign a valid `CameraId`.
- Set URP `UniversalAdditionalCameraData.renderType` to `Base` or `Overlay` in the scene.
- Call `SetDefaultBase` from the scene/scope that owns the default Base.
- Use `PushBase` for temporary overrides such as BirdsEye.
- Use `DisableOverlay` for scoped overlay suppression.
