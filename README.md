# Skate in GTA

**Skate-style skateboarding for GTA V Story Mode.** Push, ollie, kickflip, heelflip, 360 flip, grind rails and ledges, boardslide, manual, powerslide and bail, all on the streets of Los Santos. Includes Skate's **Flick-it** trick system (hold the left mouse button and flick, like skate.'s mouse controls), 15 challenges, a step-by-step tutorial, the Hall of Meat, board sounds, a fisheye filmer cam and slow motion for clips, and a **Scramjet-style rocket boost**. Made for **mouse and keyboard**.

Inspired by the "GTA 5 in Skate" clips: those put the GTA map into a skate game. This mod does the opposite and puts skating into GTA. **You don't need Skate.** You only need GTA V on PC.

> **🤖 Made by Claude.** This project was written entirely by [Claude](https://claude.ai/code), an AI assistant made by Anthropic. The repo owner only gave the idea and directions; Claude wrote the code, the docs and the build setup.

> **Status: v0.10, early and untested in-game.** The code compiles against the real ScriptHookVDotNet API, but it hasn't been played yet. If something feels off, most of it can be tuned in `SkateInGTA.ini` without touching code. Bug reports are welcome under [Issues](../../issues).

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
   - You should end up with `<game folder>\scripts\SkateInGTA.dll`, `<game folder>\scripts\SkateInGTA.ini` and the sound folder `<game folder>\scripts\SkateInGTA\`.
5. **Start Story Mode** and press **K**.

### GTA V Legacy (the older PC version)

The steps are the same. You can use either SHVDNE or the original [ScriptHookVDotNet v3](https://github.com/scripthookvdotnet/scripthookvdotnet/releases) in step 3.

### Alternative: no DLL needed

ScriptHookVDotNet can compile a raw C# file when the game starts:
- Copy `src/SkateInGTA.cs` into `scripts` and rename it to **`SkateInGTA.3.cs`**. The `.3` tells it to use API v3.
- Copy `SkateInGTA.ini` into `scripts` as well, and the `sounds` folder as `scripts\SkateInGTA` (optional, for board sounds).

### Your first 5 minutes

On your very first ride, an **in-game tutorial** starts by itself. It shows one step at a time at the bottom of the screen (push, steer, brake, ollie, kickflip, the mouse Flick-it, spin, grab, manual, grind, rocket) and only moves on once you've done it. **T** skips it or starts it again.

Without the tutorial:


1. Press **H** any time to see all controls on screen. Press **K** on a quiet street. Hold **W** to push, steer with **A / D**.
2. Hold **Space** and let go: ollie. Then try **Q** (kickflip).
3. Find a curb or a bench, ride along it, ollie and land on its edge: you grind. Keep your balance with **A / D**.
4. Now the real Skate way: **hold the left mouse button, pull the mouse toward you, flick it up-left**. That's a Flick-it kickflip.
5. The challenge in the top left tells you what to try next. Progress is saved.

---

## Controls

There are two ways to do tricks, and you can mix them:
- **Simple keys:** one key = one trick.
- **Flick-it:** Skate's stick gestures with the mouse (hold the left mouse button), the arrow keys, or a controller's right stick. Flick-it tricks score **1.5x**.

### Basics

| Action | Keyboard | Controller |
|---|---|---|
| Get on / off the board | **K** (change in ini) | – |
| Push (speed up) | **W** | Left stick up |
| Brake | **S** | Left stick down |
| Steer | **A / D** | Left stick |
| Ollie | **Hold Space, release** (longer = higher) | Hold **X / ◻**, release |
| Spin 180 / 360 *(in the air)* | **Hold A / D**, let go to finish the spin | Left stick |

Spin assist: if you let go of A/D within 30 degrees of a clean 180 or 360, the spin finishes itself. You still have to commit to most of the spin. Turn it off with `SpinAssist=0`.

### Simple keys

They work on the ground (pop + trick in one press) and in the air.

| Trick | Keyboard | Controller |
|---|---|---|
| Kickflip | **Q** | RB / R1 |
| Heelflip | **E** | D-pad right |
| Pop shove-it | **R** | B / ◯ |

Tip: hold **Space** to crouch, then press **Q** for a higher kickflip.

Hold **Alt** for the harder versions:

| Trick | Keyboard |
|---|---|
| Hardflip | **Alt + Q** |
| Inward heelflip | **Alt + E** |
| 360 shove-it | **Alt + R** |

### Flick-it (like in Skate)

Every gesture starts by pulling the stick **down**, which crouches you. Then you flick it. The pop happens on the flick.

| Trick | Arrow keys | Mouse (hold left button) / right stick |
|---|---|---|
| Ollie | **↓** then **↑** | down, then flick up |
| Nollie | **↑** then **↓** | up, then flick down |
| Nollie kickflip | **↑** then **↓ + ←** together | up, then flick down-left |
| Nollie heelflip | **↑** then **↓ + →** together | up, then flick down-right |
| Kickflip | **↓** then **↑ + ←** together | down, then flick up-left |
| Heelflip | **↓** then **↑ + →** together | down, then flick up-right |
| Pop shove-it | **↓** then tap **←** | down, then left, let go |
| FS shove-it | **↓** then tap **→** | down, then right, let go |
| 360 flip | **↓**, **←** (short), then **↑** | down, roll along the left side, flick up |
| Varial heelflip | **↓**, **→** (short), then **↑** | down, roll along the right side, flick up |

- **Mouse:** hold the left mouse button, pull the mouse toward you, then flick it away. Let go of the button and the "stick" snaps back to the middle. This is how skate.'s own hidden mouse mode works.
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
| Grab *(in the air)* | **Hold F** (+ **W** = nosegrab, + **S** = tailgrab) | Hold Y / △ |

- **Manual:** a balance bar appears. Keep the marker in the middle with W / S. If it reaches the edge, you lose your balance and the combo.
- **Combos like in Skate:** land while holding Ctrl to go straight into a manual, then ollie out of it into the next trick. The combo only counts when you roll away normally.
- **Grab:** the longer you hold it, the more points. Let go before you land, or you bail.

### Grinds and slides

There are no special "grind rails": every edge in Los Santos works, including curbs, benches, ledges, handrails and the spawned `prop_skate_rail`.

| Action | Keyboard |
|---|---|
| Start a grind | Ollie and land on an edge while riding along it |
| 50-50 grind | Land with the board straight |
| Nosegrind / 5-0 | Hold **W** / **S** while landing on the edge (harder balance, more points) |
| Boardslide | Land with the board turned 90 degrees (spin a quarter turn with A / D) |
| Keep your balance | **A / D** |
| Ollie or flip out | **Space**, **Q / E / R** or a flick |
| Step off | **S** |

- The longer and further you grind, the more points. Grinding 3 meters completes a challenge.
- Lose your balance on a grind and you bail.

### Powerslide

Hold **C** while rolling: the board turns sideways and you slide to a stop. Scores a little if you hold it for a moment.

### Filming clips

| Action | Keyboard |
|---|---|
| Cycle camera: Skate cam / **filmer cam** (low fisheye, the Skate look) / far cam | **V** |
| Slow motion on / off | **B** |
| Record a clip with GTA's Rockstar Editor | **F1** start / stop (GTA's own key) |

### Challenges

The top left shows your next challenge, from "land an ollie" up to "score 5,000 in one combo". Each one gives +500 points. Progress and your best combo are saved in `scripts\SkateInGTA_progress.ini`; delete that file to start over.

### Skate spots and ramps

| Action | Keyboard |
|---|---|
| Save a skate spot | **J** |
| Go back to your spot (also right after a bail) | **U** |
| Spawn a ramp in front of you (press again for the next type) | **L** |
| Remove all spawned ramps | **Delete** |

The ramps are GTA's own skatepark props (kickers, funbox, quarter pipe, half pipe, rail, spine). Up to 8 can exist at once.

### Money (no trainer needed)

Press **F9** in Story Mode: you get $10,000,000 per press (change `MoneyAmount` in the ini). It stops at 2 billion.
The F4 menu from Script Hook V (Native Trainer) is steered with the **numpad** (NUM 2/4/6/8, NUM 5 to select, NumLock on), which laptops often don't have, so F9 is the easy way.

### Stance tuning (how you stand on the board)

Everybody's GTA looks a bit different, so you can fix the stance live. Press **F10**, then use the number row:

| Keys | What it changes |
|---|---|
| **1 / 2** | Player lower / higher (feet sinking into or floating over the board) |
| **3 / 4** | Turn the player on the board (regular / goofy / facing forward) |
| **5 / 6** | Turn the board (if it points sideways) |
| **7 / 8** | Board lower / higher |
| **9** | Save (survives restarts) |

### Animations

GTA has no skateboard animations, so the mod uses the closest stock clips: bent knees while riding, a crouch that follows how long you charge the ollie, a tuck in the air, knees absorbing the landing, arms out in manuals and grinds, and a hand-down grab. They are listed in `[Anim]` in the ini and you can swap any of them. Exact Skate animations would need custom clips, and those belong to EA.

### Hall of Meat

When you bail, the mod scores your crash after you stop tumbling: distance, speed and damage, like Skate's Hall of Meat. Press **X** to bail on purpose.

### Landing rules

- A trick must finish before you touch the ground. Landing in the last bit of a flip still counts, but it's **sketchy**.
- A spin must land straight or backwards (fakie). Up to 15 degrees off is clean, up to 35 is **sketchy**, more is a bail.
- **Sketchy** landings keep the combo but halve it and slow you down.
- Steep slopes, big drops, water, fast hits on walls or benches, and **cars** make you **bail** (ragdoll). Press **U** to go back to your saved spot or **K** to get back on.

**Score:** each trick adds points and raises the combo multiplier. A clean landing banks the combo; a bail loses it. The same trick twice in one combo is worth half, three times a quarter, so mix it up.

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
| `[Sound] Volume` | Board sound volume, `0.0` to `1.0` |

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
6. **Grinds without rail data.** GTA's map has no list of rails, so the mod finds edges with short downward raycasts beside the board. Where the surface ends and the ground next to it is clearly lower, that's an edge. Land on it while moving along it, and you grind.
7. **Sounds.** The board sounds are generated by [`tools/make_sounds.py`](tools/make_sounds.py) from noise and sine waves (no copied audio) and played with Windows' built-in MCI, so they can loop and overlap. Combo and challenge stingers are GTA's own frontend sounds.

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
| No board sounds | Check that `scripts\SkateInGTA\` contains all 7 `.wav` files from the zip (roll, grind, slide, pop, land, flip, bail). |
| Grinds start when I don't want them | Land a bit further from curbs and ledges, or step off with **S**. |

---

## Build from source

You need the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer). It works on Windows, Linux, or macOS:

```
dotnet build src/SkateInGTA.csproj -c Release
```

The output is `src/bin/Release/net48/SkateInGTA.dll`. GitHub Actions builds every push. Pushing a `v*` tag creates a release with the zip.

---

## Ideas for later

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
