# Haptics Service

A framework-level haptics service for Sticker (com.stickerfwk.core), modelled
on `ISoundService`. v1 supports one-shot playback only; continuous patterns
and gamepad rumble are out of scope.

## Layer split

| Layer | Namespace | Contents |
|-------|-----------|----------|
| Core (consumer-facing) | `StickerFwk.Core.Haptics` | `IHapticService`, `HapticPattern`, `HapticPatternCurve`, `HapticCurvePoint`, `HapticPresetId`, `HapticPresets` |
| Infrastructure | `StickerFwk.Infrastructure.Haptics` | `HapticService`, `HapticServiceRoot`, `HapticServiceInstaller`, `HapticProfile`, `DefaultHapticProfile`, `HapticCueSheet`, `HapticData`, `SerializableHapticCurvePoint` |
| Platform (internal) | `StickerFwk.Infrastructure.Haptics.Platform` | `IHapticBackend`, `NoOpHapticBackend`, `IOSHapticBackend`, `AndroidHapticBackend` |

Feature code depends only on `StickerFwk.Core.Haptics`. Only the game-project
LifetimeScope references `StickerFwk.Infrastructure.Haptics` (to attach the
installer), exactly mirroring `SoundServiceInstaller`.

## Default preset catalogue

Ships as `DefaultHapticProfile`, a C# profile registered by
`HapticServiceInstaller` and passed into `HapticService` during container
construction. Contents:

| Name | Intent | iOS native mapping |
|------|--------|--------------------|
| `Selection` | UI focus change | `UISelectionFeedbackGenerator.selectionChanged` |
| `LightImpact` | Small UI confirm | `UIImpactFeedbackGenerator.light` |
| `MediumImpact` | Standard button press | `UIImpactFeedbackGenerator.medium` |
| `HeavyImpact` | Strong impact (bat contact) | `UIImpactFeedbackGenerator.heavy` |
| `RigidImpact` | Sharp, snappy impact | `UIImpactFeedbackGenerator.rigid` (iOS 13+) |
| `SoftImpact` | Soft, dampened impact | `UIImpactFeedbackGenerator.soft` (iOS 13+) |
| `Success` | Positive notification | `UINotificationFeedbackGenerator.success` |
| `Warning` | Warning notification | `UINotificationFeedbackGenerator.warning` |
| `Error` | Error notification | `UINotificationFeedbackGenerator.error` |

Android uses `VibrationEffect.createWaveform` derived from the authored
intensity curve (API 26+) or legacy `Vibrator.vibrate(long[])` (API < 26).

## Usage

```csharp
using StickerFwk.Core.Haptics;

public sealed class BatContactPresenter
{
    private readonly IHapticService _haptics;
    public BatContactPresenter(IHapticService haptics) => _haptics = haptics;
    public void OnBatContact() => _haptics.PlayOneShot(HapticPresets.HeavyImpact);
}
```

For project-specific patterns, author a `HapticCueSheet` ScriptableObject
(`Create → HapticCueSheet`), mark it Addressable, then:

```csharp
await _haptics.LoadCueSheetAsync("homerunderby/gameplay/haptics", ct);
```

Unload it on scope dispose. The full tutorial and smoke-test sequence is at
[`specs/003-haptics-service/quickstart.md`](../../../../../specs/003-haptics-service/quickstart.md).

## Editor / unsupported platforms

`NoOpHapticBackend` is selected whenever `Application.isEditor` is true or the
runtime platform is not iOS/Android. The backend is selected once during
service construction; per-call code never branches on platform.
