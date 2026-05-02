# StickerFwk.Core.Debug

In-game debug menu for the Sticker framework. Modular, VContainer-driven, stripped from release builds.

## Compilation gating

The whole assembly is gated by the `STICKER_DEBUG` scripting define. The asmdef has
`defineConstraints: ["STICKER_DEBUG"]`, so when the define is missing the assembly is excluded
from the build entirely — no IL, no calls, nothing to strip.

`STICKER_DEBUG` is set per-platform in `ProjectSettings → Player → Scripting Define Symbols`.
Remove it for release players.

## Setup

In your root `LifetimeScope.Configure(IContainerBuilder builder)`:

```csharp
#if STICKER_DEBUG
builder.UseDebugMenu();                    // uses DebugMenuSettings.Default
// or
builder.UseDebugMenu(new DebugMenuSettings(...));
#endif
```

This:
- Spawns a `DontDestroyOnLoad` GameObject with `DebugMenuService` (an IMGUI overlay).
- Registers the built-in `LogsDebugPage`.
- Force-resolves the service so the corner toggle button (labelled "Debug") is visible immediately.

### Customizing layout

`DebugMenuSettings` is a `ScriptableObject` configuring everything visual: toggle-button corner /
size / text / margin, panel width / margin / height, UI scale, font sizes, widget height, and
label column width.

Create one via **Assets → Create → Sticker → Framework → Debug Menu Settings**, edit it in the
inspector, then drag it onto the **Debug Menu Settings** field of your root `LifetimeScope`. The
Sticker project ships with `Assets/Settings/DebugMenuSettings.asset` already wired to
`RootLifetimeScope.prefab`.

You can also edit it from **Edit → Project Settings → Sticker → Debug Menu** — the provider
loads (or creates) the asset at `Assets/Settings/DebugMenuSettings.asset` and edits it in place.

If no asset is assigned, `UseDebugMenu(...)` falls back to `DebugMenuSettings.Default` (an
in-memory instance with sensible built-in values).

## Adding a page

A page is a plain C# class — no `MonoBehaviour`, no Unity inheritance:

```csharp
#if STICKER_DEBUG
public sealed class StickerGameDebugPage : IDebugPage
{
    private readonly StickerGameService _game;

    public StickerGameDebugPage(StickerGameService game) { _game = game; }

    public string Title => "Sticker Game";
    public string Id => "sticker.game";   // stable for last-open persistence
    public int Order => 100;              // sort order on the root list

    public void Build(IDebugPageBuilder b)
    {
        b.Label("Cheats")
         .Button("Win current stage", _game.ForceWin)
         .Toggle("Infinite stickers", () => _game.Unlimited, v => _game.Unlimited = v)
         .Slider("Time scale", () => Time.timeScale, v => Time.timeScale = v, 0.1f, 4f)
         .Separator()
         .PageLink("Stage tools", new StageToolsDebugPage(_game));
    }
}
#endif
```

Register inside the feature's installer (or a dedicated `*.Debug` asmdef gated by
`STICKER_DEBUG`) so the menu picks it up:

```csharp
#if STICKER_DEBUG
builder.Register<IDebugPage, StickerGameDebugPage>(Lifetime.Singleton);
#endif
```

`Build` is called once on first display; the resulting widget list is cached for the lifetime
of the menu. State binding is done via `get`/`set` delegates so pages keep ownership of their
fields and are re-read on every render.

## Widget reference

| Widget                                                                | Purpose                                             |
|-----------------------------------------------------------------------|-----------------------------------------------------|
| `Label(string)` / `Label(Func<string>)`                               | Static or per-frame dynamic text.                   |
| `Button(text, onClick)`                                               | Tappable button.                                    |
| `Toggle(text, get, set)`                                              | Boolean toggle.                                     |
| `Slider(text, get, set, min, max)`                                    | Float slider.                                       |
| `IntSlider(text, get, set, min, max)`                                 | Int slider.                                         |
| `TextField(text, get, set)`                                           | Single-line text input.                             |
| `EnumDropdown<TEnum>(text, get, set)`                                 | Selection grid over enum values.                    |
| `PageLink(text, page)` / `PageLink(text, factory)`                    | Push another page onto the navigation stack.        |
| `Separator()`                                                         | Thin horizontal divider.                            |

## Navigation

The menu is a stack of pages, like phone settings:
- The implicit root page lists all registered `IDebugPage`s as `PageLink`s, sorted by `Order` then `Title`.
- Tapping a `PageLink` pushes; the title bar's **◀ Back** pops. Back is disabled at the root.
- **✕** closes the menu and persists the current top-of-stack page id to `PlayerPrefs`
  (`StickerFwk.DebugMenu.LastPageId`). On the next open, that page is restored if still registered.

## What this is not

- Not a player-facing settings UI.
- Not a QA build tool — release builds compile it out via `defineConstraints`.
- Not built on `WindowView`/`UIService`. It's an independent IMGUI overlay so it survives scene
  transitions and never collides with gameplay UI z-order or input locks.
