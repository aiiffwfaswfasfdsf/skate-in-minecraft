# Skate in GTA

**Skate-style skateboarding for GTA V Story Mode.** Push, ollie, kickflip, heelflip, shove-it, 360 flip, spin, and bail, all on the streets of Los Santos. Includes Skate's **Flick-it** trick system, manuals, grabs, a skatepark spawner, the Hall of Meat, and a **Scramjet-style rocket boost**. Fully playable on **keyboard**.

Inspired by the "GTA 5 in Skate" clips: those put the GTA map into a skate game. This mod does the opposite and puts skating into GTA. **You don't need Skate.** You only need GTA V on PC.

> **🤖 Made by Claude.** This project was written entirely by [Claude](https://claude.ai/code), an AI assistant made by Anthropic. The repo owner only gave the idea and directions; Claude wrote the code, the docs and the build setup.

> **Status: v0.3, early and untested in-game.** The code compiles against the real ScriptHookVDotNet API, but it hasn't been played yet. If something feels off, most of it can be tuned in `SkateInGTA.ini` without touching code. Bug reports are welcome under [Issues](../../issues).

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
5. **Start Story Mode** and press **K**.

### GTA V Legacy (the older PC version)

The steps are the same. You can use either SHVDNE or the original [ScriptHookVDotNet v3](https://github.com/scripthookvdotnet/scripthookvdotnet/releases) in step 3.

### Alternative: no DLL needed

ScriptHookVDotNet can compile a raw C# file when the game starts:
- Copy `src/SkateInGTA.cs` into `scripts` and rename it to **`SkateInGTA.3.cs`**. The `.3` tells it to use API v3.
- Copy `SkateInGTA.ini` into `scripts` as well.

---

## Controls

There are two ways to do tricks, and you can mix them:
- **Simple keys:** one key = one trick.
- **Flick-it:** Skate's stick gestures, on the arrow keys, the mouse, or a controller's right stick.

### Basics

| Action | Keyboard | Controller |
|---|---|---|
| Get on / off the board | **K** (change in ini) | – |
| Push (speed up) | **W** | Left stick up |
| Brake | **S** | Left stick down |
| Steer | **A / D** | Left stick |
| Ollie | **Hold Space, release** (longer = higher) | Hold **X / ◻**, release |
| Spin 180 / 360 *(in the air)* | **Hold A / D**, let go to finish the spin | Left stick |

Spin assist: when you let go of A/D in the air, the spin automatically finishes to the nearest straight or backwards (fakie) position. 180s are easy on a keyboard this way. You can turn it off with `SpinAssist=0`.

### Simple keys

They work on the ground (pop + trick in one press) and in the air.

| Trick | Keyboard | Controller |
|---|---|---|
| Kickflip | **Q** | RB / R1 |
| Heelflip | **E** | D-pad right |
| Pop shove-it | **R** | B / ◯ |

Tip: hold **Space** to crouch, then press **Q** for a higher kickflip.

### Flick-it (like in Skate)

Every gesture starts by pulling the stick **down**, which crouches you. Then you flick it. The pop happens on the flick.

| Trick | Arrow keys | Right stick / mouse |
|---|---|---|
| Ollie | **↓** then **↑** | down, then flick up |
| Nollie | **↑** then **↓** | up, then flick down |
| Kickflip | **↓** then **↑ + ←** together | down, then flick up-left |
| Heelflip | **↓** then **↑ + →** together | down, then flick up-right |
| Pop shove-it | **↓** then tap **←** | down, then left, let go |
| FS shove-it | **↓** then tap **→** | down, then right, let go |
| 360 flip | **↓**, **←** (short), then **↑** | down, roll along the left side, flick up |
| Varial heelflip | **↓**, **→** (short), then **↑** | down, roll along the right side, flick up |

- **Mouse:** pull the mouse toward you, then flick it away. The "stick" springs back to the middle on its own.
- **Stick display:** the small box in the bottom right shows what the mod sees. A yellow dot means you are crouched and ready to flick. If mouse flicks don't register, raise `MouseSensitivity` in the ini; if tricks happen by accident, lower it or set `Mouse=false`.
- **Phone and camera:** while you ride, the arrow keys don't open the phone, and the mouse doesn't turn the camera (the Skate camera follows you instead).

### Rocket boost (like the Scramjet)

| Action | Keyboard | Controller |
|---|---|---|
| Rocket boost | **Hold Shift** | Hold A / ✕ |
| Rocket jump | **Hold Shift + ollie** (Space, Q/E/R or a flick) | Hold A / ✕ + ollie |

- The orange bar above the stick box is your fuel. A full bar lasts about 3 seconds and recharges a second after you stop boosting.
- A rocket jump uses about a third of the bar. The bar is bright orange when there is enough fuel for one.
- Boosting in the air pushes you forward and a little up, so you can glide over gaps.
- Careful: hitting a wall at rocket speed is a guaranteed bail.

### Manuals and grabs

| Action | Keyboard | Controller |
|---|---|---|
| Manual (ride on the back wheels) | **Hold Ctrl** | Hold L3 |
| Keep your balance in a manual | **W / S** | Left stick up / down |
| Grab *(in the air)* | **Hold F** | Hold Y / △ |

- **Manual:** a balance bar appears. Keep the marker in the middle with W / S. If it reaches the edge, you lose your balance and the combo.
- **Combos like in Skate:** land while holding Ctrl to go straight into a manual, then ollie out of it into the next trick. The combo only counts when you roll away normally.
- **Grab:** the longer you hold it, the more points. Let go before you land, or you bail.

### Skate spots and ramps

| Action | Keyboard |
|---|---|
| Save a skate spot | **J** |
| Go back to your spot (also right after a bail) | **U** |
| Spawn a ramp in front of you (press again for the next type) | **L** |
| Remove all spawned ramps | **Delete** |

The ramps are GTA's own skatepark props (kickers, funbox, quarter pipe, half pipe, rail, spine). Up to 8 can exist at once.

### Hall of Meat

When you bail, the mod scores your crash after you stop tumbling: distance, speed and damage, like Skate's Hall of Meat.

### Landing rules

- A trick must finish before you touch the ground.
- A spin must land straight or backwards (fakie).
- Steep slopes, big drops, water, and fast wall hits make you **bail** (ragdoll). Press K to get back on.

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
| `[Tricks] SpinAssist` | Auto-finish spins when you let go (`0` = off) |
| `[Rocket] Thrust / MaxSpeed` | How hard and how fast the rocket pushes |
| `[Rocket] JumpBoost / JumpCost` | Rocket jump height and fuel cost |
| `[Rocket] FlamePitch / FlameYaw` | Turn the flames if they point the wrong way |
| `[Manual] Difficulty` | Higher = harder to balance |
| `[Ramps] YawOffset` | Turn spawned ramps if they face the wrong way (try `180`) |
| `[FlickIt] ArrowKeys / Mouse` | Turn the flick inputs on or off |
| `[FlickIt] MouseSensitivity` | How strongly mouse movement moves the flick stick |
| `[FlickIt] ShowStick` | Show the small stick display |
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
5. **Flick-it gestures.**
   - The arrow keys, the mouse, or the right stick become one "virtual stick".
   - A small state machine watches it: *down* = crouch, then the direction of the flick picks the trick. Passing along a side on the way up turns it into a 360 flip or varial.
   - On the keyboard it waits a few milliseconds before deciding, so pressing **↑ + ←** "together" still counts as a diagonal.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Game closes with "unknown game version" | GTA updated. Download the newest Script Hook V (and SHVDNE if needed) and replace the files. |
| Pressing F5 switches to Michael | That's GTA's character-switch key. The mod uses **K** since v0.2. |
| Nothing happens when pressing K | Check `ScriptHookVDotNet.log` in the game folder for errors. Make sure both the `.dll` and `.ini` are in `scripts`. |
| Board is invisible | Change `[Board] Models` in the ini, for example to `v_16_skateboard`. |
| Board points sideways | Change `[Board] YawOffset` (try `0` or `90`). |
| Player floats or sinks | Change `[Rider] Height`. |
| Camera feels weird | Set `[Camera] UseSkateCam=false`. |
| Rocket flames point up or sideways | Change `[Rocket] FlamePitch` or `FlameYaw` (try `90`, `-90` or `180`). |
| Ramps face the wrong way | Set `[Ramps] YawOffset=180`. |

---

## Build from source

You need the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer). It works on Windows, Linux, or macOS:

```
dotnet build src/SkateInGTA.csproj -c Release
```

The output is `src/bin/Release/net48/SkateInGTA.dll`. GitHub Actions builds every push. Pushing a `v*` tag creates a release with the zip.

---

## Ideas for later

- Grinds on rails and ledges (you can already ride on top of a spawned rail)
- More flip tricks (hardflip, impossible) and more grab types
- Trick replay / clip camera

## Credits

- [Script Hook V](http://www.dev-c.com/gtav/scripthookv/) by Alexander Blade
- [ScriptHookVDotNet](https://github.com/scripthookvdotnet/scripthookvdotnet) and [ScriptHookVDotNet Enhanced](https://github.com/Chiheb-Bacha/ScriptHookVDotNetEnhanced)
- Written by [Claude](https://claude.ai/code) (Anthropic) at the request of the repo owner
- Research on how other creators built skateboard mods. These were used to pick the board model and riding animation; no code was copied:
  - [SkateV](https://www.gta5-mods.com/scripts/skatev) by jedijosh920
  - [Skate V Plus](https://www.gta5-mods.com/scripts/skate-v-plus) by gopro_2027
  - Open-source FiveM skate scripts such as [murderface-skate](https://github.com/fruitmob/murderface-skate)

## License

[MIT](LICENSE). This is a fan project, not affiliated with Rockstar Games, Take-Two, or EA.
