// StickerFwkHaptics.mm
// Tiny Objective-C++ shim invoked via [DllImport("__Internal")] from IOSHapticBackend.cs.
// Routes preset hints to UIKit feedback generators and authored intensity/sharpness
// curves through Core Haptics. ~100 LOC by design — no logic the C# side could do.

#import <UIKit/UIKit.h>
#import <CoreHaptics/CoreHaptics.h>

// Matches StickerFwk.Core.Haptics.HapticPresetId.
typedef NS_ENUM(int, SFHPresetId) {
    SFHPresetIdNone = 0,
    SFHPresetIdSelection,
    SFHPresetIdLightImpact,
    SFHPresetIdMediumImpact,
    SFHPresetIdHeavyImpact,
    SFHPresetIdRigidImpact,
    SFHPresetIdSoftImpact,
    SFHPresetIdSuccess,
    SFHPresetIdWarning,
    SFHPresetIdError,
};

static CHHapticEngine *g_engine API_AVAILABLE(ios(13.0)) = nil;
static UISelectionFeedbackGenerator *g_selection = nil;
static NSMutableDictionary<NSNumber *, UIImpactFeedbackGenerator *> *g_impacts = nil;
static UINotificationFeedbackGenerator *g_notify = nil;

static void EnsureGenerators(void) {
    if (g_selection == nil) {
        g_selection = [[UISelectionFeedbackGenerator alloc] init];
        [g_selection prepare];
    }
    if (g_notify == nil) {
        g_notify = [[UINotificationFeedbackGenerator alloc] init];
        [g_notify prepare];
    }
    if (g_impacts == nil) {
        g_impacts = [NSMutableDictionary dictionary];
    }
}

static UIImpactFeedbackGenerator *ImpactFor(UIImpactFeedbackStyle style) {
    EnsureGenerators();
    NSNumber *key = @(style);
    UIImpactFeedbackGenerator *gen = g_impacts[key];
    if (gen == nil) {
        gen = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        [gen prepare];
        g_impacts[key] = gen;
    }
    return gen;
}

static void EnsureEngine(void) API_AVAILABLE(ios(13.0)) {
    if (g_engine != nil) return;
    if (![CHHapticEngine capabilitiesForHardware].supportsHaptics) return;

    NSError *err = nil;
    g_engine = [[CHHapticEngine alloc] initAndReturnError:&err];
    if (err != nil) { g_engine = nil; return; }

    __weak CHHapticEngine *weakEngine = g_engine;
    g_engine.stoppedHandler = ^(CHHapticEngineStoppedReason reason) {
        CHHapticEngine *strong = weakEngine;
        if (strong != nil) { [strong startAndReturnError:nil]; }
    };
    g_engine.resetHandler = ^{
        CHHapticEngine *strong = weakEngine;
        if (strong != nil) { [strong startAndReturnError:nil]; }
    };

    [g_engine startAndReturnError:&err];
    if (err != nil) { g_engine = nil; }
}

extern "C" {

void StickerFwk_Haptics_PlayImpact(int style, float intensity) {
    if (intensity <= 0.0f) return;
    EnsureGenerators();
    float clamped = intensity < 0.0f ? 0.0f : (intensity > 1.0f ? 1.0f : intensity);

    switch ((SFHPresetId)style) {
        case SFHPresetIdSelection:
            [g_selection selectionChanged];
            [g_selection prepare];
            return;

        case SFHPresetIdSuccess:
            [g_notify notificationOccurred:UINotificationFeedbackTypeSuccess];
            [g_notify prepare];
            return;
        case SFHPresetIdWarning:
            [g_notify notificationOccurred:UINotificationFeedbackTypeWarning];
            [g_notify prepare];
            return;
        case SFHPresetIdError:
            [g_notify notificationOccurred:UINotificationFeedbackTypeError];
            [g_notify prepare];
            return;

        case SFHPresetIdLightImpact: {
            UIImpactFeedbackGenerator *gen = ImpactFor(UIImpactFeedbackStyleLight);
            [gen impactOccurredWithIntensity:clamped]; [gen prepare];
            return;
        }
        case SFHPresetIdMediumImpact: {
            UIImpactFeedbackGenerator *gen = ImpactFor(UIImpactFeedbackStyleMedium);
            [gen impactOccurredWithIntensity:clamped]; [gen prepare];
            return;
        }
        case SFHPresetIdHeavyImpact: {
            UIImpactFeedbackGenerator *gen = ImpactFor(UIImpactFeedbackStyleHeavy);
            [gen impactOccurredWithIntensity:clamped]; [gen prepare];
            return;
        }
        case SFHPresetIdRigidImpact: {
            if (@available(iOS 13.0, *)) {
                UIImpactFeedbackGenerator *gen = ImpactFor(UIImpactFeedbackStyleRigid);
                [gen impactOccurredWithIntensity:clamped]; [gen prepare];
            } else {
                UIImpactFeedbackGenerator *gen = ImpactFor(UIImpactFeedbackStyleHeavy);
                [gen impactOccurredWithIntensity:clamped]; [gen prepare];
            }
            return;
        }
        case SFHPresetIdSoftImpact: {
            if (@available(iOS 13.0, *)) {
                UIImpactFeedbackGenerator *gen = ImpactFor(UIImpactFeedbackStyleSoft);
                [gen impactOccurredWithIntensity:clamped]; [gen prepare];
            } else {
                UIImpactFeedbackGenerator *gen = ImpactFor(UIImpactFeedbackStyleLight);
                [gen impactOccurredWithIntensity:clamped]; [gen prepare];
            }
            return;
        }

        case SFHPresetIdNone:
        default:
            return;
    }
}

void StickerFwk_Haptics_PlayPattern(float *intensity, float *sharpness, int count,
                                    float duration, float intensityScale) {
    if (count <= 0 || duration <= 0.0f || intensity == NULL || sharpness == NULL) return;
    if (intensityScale <= 0.0f) return;

    if (@available(iOS 13.0, *)) {
        EnsureEngine();
        if (g_engine == nil) return;

        NSMutableArray<CHHapticEventParameter *> *params0 = [NSMutableArray arrayWithObjects:
            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity
                                                          value:intensity[0] * intensityScale],
            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness
                                                          value:sharpness[0]],
            nil];

        NSMutableArray<CHHapticEvent *> *events = [NSMutableArray array];
        CHHapticEvent *base = [[CHHapticEvent alloc]
            initWithEventType:CHHapticEventTypeHapticContinuous
                   parameters:params0
                 relativeTime:0
                     duration:duration];
        [events addObject:base];

        NSMutableArray<CHHapticParameterCurve *> *curves = [NSMutableArray array];

        NSMutableArray<CHHapticParameterCurveControlPoint *> *intensityCps = [NSMutableArray array];
        NSMutableArray<CHHapticParameterCurveControlPoint *> *sharpnessCps = [NSMutableArray array];
        float step = (count <= 1) ? 0.0f : (duration / (float)(count - 1));
        for (int i = 0; i < count; i++) {
            NSTimeInterval t = (NSTimeInterval)(step * i);
            [intensityCps addObject:[[CHHapticParameterCurveControlPoint alloc]
                                       initWithRelativeTime:t value:intensity[i] * intensityScale]];
            [sharpnessCps addObject:[[CHHapticParameterCurveControlPoint alloc]
                                       initWithRelativeTime:t value:sharpness[i]]];
        }
        [curves addObject:[[CHHapticParameterCurve alloc]
            initWithParameterID:CHHapticDynamicParameterIDHapticIntensityControl
                  controlPoints:intensityCps relativeTime:0]];
        [curves addObject:[[CHHapticParameterCurve alloc]
            initWithParameterID:CHHapticDynamicParameterIDHapticSharpnessControl
                  controlPoints:sharpnessCps relativeTime:0]];

        NSError *err = nil;
        CHHapticPattern *pattern = [[CHHapticPattern alloc]
            initWithEvents:events parameterCurves:curves error:&err];
        if (err != nil || pattern == nil) return;

        id<CHHapticPatternPlayer> player = [g_engine createPlayerWithPattern:pattern error:&err];
        if (err != nil || player == nil) return;

        [player startAtTime:0 error:&err];
    }
}

void StickerFwk_Haptics_StopEngine(void) {
    if (@available(iOS 13.0, *)) {
        if (g_engine != nil) {
            [g_engine stopWithCompletionHandler:nil];
            g_engine = nil;
        }
    }
    g_selection = nil;
    g_notify = nil;
    g_impacts = nil;
}

} // extern "C"
