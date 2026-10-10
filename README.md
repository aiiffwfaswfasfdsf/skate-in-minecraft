# Skate in GTA

**Skate-style skateboarding for GTA V Story Mode.** Push, ollie, kickflip, heelflip, shove-it, spin, and bail, all on the streets of Los Santos.

Inspired by the "GTA 5 in Skate" clips: those put the GTA map into a skate game. This mod does the opposite and puts skating into GTA. **You don't need Skate.** You only need GTA V on PC.

> **Status: v0.1, early and untested in-game.** The code compiles against the real ScriptHookVDotNet API, but it hasn't been played yet. If something feels off, most of it can be tuned in `SkateInGTA.ini` without touching code. Bug reports are welcome under [Issues](../../issues).

---

## ⚠️ Read this first

- **PC only.** PlayStation and Xbox can't load mods.
- **Story Mode only. Never go into GTA Online with mods installed.** Script Hook V closes the game if you try. Remove or rename the mod files before playing Online so you don't risk a ban.
- **Remove old mod menus or trainers first.** If an old "mod menu" or trainer is still in your GTA folder, delete it before installing. This applies especially to anything made for GTA Online, old `dsound.dll` loaders, and outdated `ScriptHookVDotNet*` files. Old files cause crashes and conflicts.

---

## Installation (GTA V Enhanced, the 2025 PC version)

1. **Find your game folder.** It's the folder that contains the game's `.exe`. Common locations:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V Enhanced`
   - Rockstar Launcher: `C:\Program Files\Rockstar Games\Grand Theft Auto V Enhanced`
   - Steam shortcut: right-click the game → *Manage* → *Browse local files*.
2. **Install Script Hook V** by Alexander Blade from the official site: <http://www.dev-c.com/gtav/scripthookv/>
   - Copy everything from the zip's `bin` folder into the game folder.
   - Version 3889.0 / 1158.13 and newer supports both Enhanced and Legacy.
3. **Install ScriptHookVDotNet Enhanced (SHVDNE)** from <https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced/releases>
   - Extract the files from the zip's root into the game folder, including `ScriptHookVDotNet.asi`, `ScriptHookVDotNet2.dll`, `ScriptHookVDotNet3.dll`, and the `.ini`. You don't need `README.txt` or the two subfolders.
   - Requirements: [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) and the [Visual C++ Redistributable x64](https://aka.ms/vs/17/release/vc_redist.x64.exe).
4. **Install Skate in GTA.** Download `SkateInGTA.zip` from [Releases](../../releases).
   - Copy its `scripts` folder into the game folder.
   - You should end up with `<game folder>\scripts\SkateInGTA.dll` and `<game folder>\scripts\SkateInGTA.ini`.
5. **Start Story Mode** and press **F5**.

### GTA V Legacy (the older PC version)

The steps are the same. You can use either SHVDNE or the original [ScriptHookVDotNet v3](https://github.com/scripthookvdotnet/scripthookvdotnet/releases) in step 3.

### Alternative: no DLL needed

ScriptHookVDotNet can compile a raw C# file when the game starts:
- Copy `src/SkateInGTA.cs` into `scripts` and rename it to **`SkateInGTA.3.cs`**. The `.3` tells it to use API v3.
- Copy `SkateInGTA.ini` into `scripts` as well.

---

## Controls

| Action | Keyboard | Controller |
|---|---|---|
| Get on / off the board | **F5** (change in ini) | – |
| Push (speed up) | **W** | Left stick up |
| Brake | **S** | Left stick down |
| Steer | **A / D** | Left stick |
| Ollie | **Hold Space, release** (longer = higher) | Hold **X / ◻**, release |
| Kickflip *(in the air)* | **Q** | RB / R1 |
| Heelflip *(in the air)* | **E** | D-pad right |
| Pop shove-it *(in the air)* | **R** | B / ◯ |
| Spin 180 / 360 *(in the air)* | **A / D** | Left stick |

The controller column follows GTA's default bindings, because the mod reads GTA's own controls.

**Landing rules:**
- A trick must finish before you touch the ground.
- A spin must land straight or backwards (fakie).
- Steep slopes, big drops, water, and fast wall hits make you **bail** (ragdoll). Press F5 to get back on.

**Score:** each trick adds points and raises the combo multiplier. A clean landing banks the combo; a bail loses it.

---

## Settings (`SkateInGTA.ini`)

Every value is explained inside the file. The ones you'll most likely change:

| Setting | What it does |
|---|---|
| `[Controls] ToggleKey` | Key to get on/off |
| `[Board] Models` | Board prop(s) to use. Try others if the board is invisible |
| `[Board] YawOffset` | Turn the board model if it points sideways |
| `[Rider] StanceYaw` | `-90` regular, `90` goofy, `0` facing forward |
| `[Physics] PushMaxSpeed / MaxSpeed` | How fast you can go (m/s; ×3.6 for km/h) |
| `[Physics] OllieMax` | How high a full ollie goes |
| `[Tricks] TrickDuration` | Smaller = faster flips = easier to land |
| `[Camera] UseSkateCam` | `true` = Skate-style camera, `false` = normal GTA camera |

Save the file, then reload scripts in-game (SHVDN's reload key, **Insert** by default) or restart the game.

---

## How it works

All the code is in one file, [`src/SkateInGTA.cs`](src/SkateInGTA.cs), with comments throughout. These are the main ideas:

1. **Hand-written physics instead of a hidden vehicle.**
   - Most skateboard mods hide a BMX under the board and let GTA's vehicle physics do the work.
   - This mod moves an invisible *anchor* prop itself every frame: speed, steering, friction, slope gravity, and gravity in the air.
   - That gives full control over ollies, flips, and bails.
2. **Raycasts.** A raycast is an invisible laser beam.
   - One points **down** to find the ground. It sets the board's height and tilts the board to match slopes.
   - One points **forward** to find walls.
   - When the ground suddenly drops away (the end of a ramp, a ledge, stairs), you start flying. You keep your speed, so **ramps launch you**.
3. **Anchor + board.**
   - The player is attached to the invisible anchor.
   - The visible skateboard is attached to the anchor separately. Tricks only rotate the board, so a kickflip spins the board under your feet instead of spinning you.
4. **Skate camera.** A scripted camera smoothly follows behind the board, like in the Skate games.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Game closes with "unknown game version" | GTA updated. Download the newest Script Hook V (and SHVDNE if needed) and replace the files. |
| Nothing happens when pressing F5 | Check `ScriptHookVDotNet.log` in the game folder for errors. Make sure both the `.dll` and `.ini` are in `scripts`. |
| Board is invisible | Change `[Board] Models` in the ini, for example to `v_16_skateboard`. |
| Board points sideways | Change `[Board] YawOffset` (try `0` or `90`). |
| Player floats or sinks | Change `[Rider] Height`. |
| Camera feels weird | Set `[Camera] UseSkateCam=false`. |

---

## Build from source

You need the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer). It works on Windows, Linux, or macOS:

```
dotnet build src/SkateInGTA.csproj -c Release
```

The output is `src/bin/Release/net48/SkateInGTA.dll`. GitHub Actions builds every push. Pushing a `v*` tag creates a release with the zip.

---

## Ideas for later

- Grinds on rails and ledges
- Manuals
- More flip tricks (360 flip, hardflip) and grabs
- Spawnable skate ramps (`prop_skate_*`)
- Trick replay / clip camera

## Credits

- [Script Hook V](http://www.dev-c.com/gtav/scripthookv/) by Alexander Blade
- [ScriptHookVDotNet](https://github.com/scripthookvdotnet/scripthookvdotnet) and [ScriptHookVDotNet Enhanced](https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced)
- Research on how other creators built skateboard mods. These were used to pick the board model and riding animation; no code was copied:
  - [SkateV](https://www.gta5-mods.com/scripts/skatev) by jedijosh920
  - [Skate V Plus](https://www.gta5-mods.com/scripts/skate-v-plus) by gopro_2027
  - Open-source FiveM skate scripts such as [murderface-skate](https://github.com/fruitmob/murderface-skate)

## License

[MIT](LICENSE). This is a fan project, not affiliated with Rockstar Games, Take-Two, or EA.
