// Skate in GTA - Skate-style skateboarding for GTA V Story Mode.
// Written by Claude (an AI assistant by Anthropic) at the request of the repo owner.
// Built on the ScriptHookVDotNet v3 API (works with SHVDN on Legacy and SHVDNE on Enhanced).
//
// The code is written in C# 5 on purpose: ScriptHookVDotNet can compile a raw .cs file
// at game start, and its compiler does not understand newer syntax like $"" or ?.
//
// How it works (short version):
//   * The player is attached to an invisible "anchor" prop. We move the anchor ourselves
//     every frame with a small hand-written physics model (no GTA vehicle involved).
//   * A visible skateboard prop is attached to the anchor. Tricks only rotate the board,
//     so a kickflip spins the board and not the player.
//   * Raycasts (invisible laser beams) find the ground under the board and walls in front of it.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using Control = GTA.Control;
using Font = GTA.UI.Font;
using Keys = System.Windows.Forms.Keys;

public class SkateInGTA : Script
{
    // ---------------------------------------------------------------- settings (from SkateInGTA.ini)

    Keys toggleKey;
    string[] boardModelNames;
    string anchorModelName;
    string rideAnimDict;
    string rideAnimClip;
    bool useSkateCam;

    float pushAccel;        // m/s^2 added while pushing
    float pushMaxSpeed;     // pushing stops helping above this speed (m/s)
    float maxSpeed;         // hard speed limit (m/s)
    float brakeDecel;       // m/s^2 removed while braking
    float friction;         // fraction of speed lost per second while rolling
    float slopeFactor;      // how strongly hills speed you up / slow you down
    float turnRate;         // degrees per second at full steer
    float gravity;          // m/s^2
    float ollieMin;         // jump speed for a quick tap (m/s)
    float ollieMax;         // jump speed for a fully charged ollie (m/s)
    float bailSpeed;        // hitting a wall faster than this makes you bail (m/s)
    float maxLandingSpeed;  // falling faster than this makes you bail (m/s)
    float landTolerance;    // spin must land within this many degrees of 0 or 180
    float trickDuration;    // seconds for one flip trick
    float airSpinRate;      // degrees per second of body spin in the air
    float spinAssist;       // degrees per second the spin snaps to 0/180 when you let go (0 = off)

    // Rocket (Scramjet-style boost and rocket jump)
    bool rocketEnabled;
    float rocketThrust;         // m/s^2 of forward thrust
    float rocketMaxSpeed;       // top speed while boosting (m/s)
    float rocketAirLift;        // upward thrust in the air, lets you glide (m/s^2)
    float rocketJumpBoost;      // extra jump speed for a rocket jump (m/s)
    float rocketJumpCost;       // fuel one rocket jump uses (0..1)
    float rocketDrain;          // fuel used per second of boost
    float rocketRecharge;       // fuel regained per second
    float rocketRechargeDelay;  // seconds after boosting before fuel recharges
    float flameScale, flamePitch, flameYaw;

    // Manuals
    float manualPointsPerSecond;
    float manualDifficulty;

    // Ramps
    string[] rampModelNames;
    int maxRamps;
    float rampDistance;
    float rampYawOffset;

    Keys markerSetKey, markerReturnKey, rampKey, clearRampsKey, bailKey, slowMoKey, cameraKey, helpKey;

    bool soundEnabled;
    float soundVolume;

    // Flick-it (Skate-style tricks by flicking a stick)
    bool flickEnabled;
    bool arrowFlick;        // arrow keys act as a stick
    bool mouseFlick;        // the mouse acts as a stick
    float mouseSensitivity;
    bool invertMouseY;
    bool showStick;         // small on-screen stick so you can see your flicks
    float pedHeight;        // how high the player stands above the anchor
    float stanceYaw;        // -90 = regular stance, 90 = goofy, 0 = facing forward
    float boardHeight;      // board offset above the anchor
    float boardYawOffset;   // rotate the board model if it points the wrong way
    float camDistance;
    float camHeight;

    // ---------------------------------------------------------------- riding state

    bool riding;
    Ped rider;              // the character that got on (switching characters ends the ride)
    DateTime animGiveUpTime; // stop trying to load the ride animation after this
    Prop anchor;            // invisible, carries the player
    Prop board;             // visible skateboard (may be null if no model could be loaded)
    Camera cam;
    Vector3 camPos;

    Vector3 pos;            // anchor position (on the ground when not airborne)
    Vector3 groundNormal = Vector3.WorldUp;
    float heading;          // direction of travel in degrees (GTA style: 0 = north, counter-clockwise)
    float speed;            // forward speed on the ground (m/s)
    float groundVz;         // smoothed vertical speed while rolling (ramps launch you with this)
    bool fakie;             // riding backwards after a 180

    bool airborne;
    Vector3 airVelocity;    // full 3D velocity while in the air
    float airSpin;          // body spin in the air (degrees)

    float ollieCharge;      // 0..1 while holding the ollie button
    bool jumpLatch;         // ignore the ollie button until it is released again

    // Flick-it gesture state
    const int FlickIdle = 0, FlickFromDown = 1, FlickFromUp = 2;
    int flickState;
    bool flickNeedsCenter;  // after a trick the stick must go back to the middle first
    float flickCharge;      // crouch / pop power built up while the stick is held down
    float flickCenterTime;  // how long the stick has been near the middle
    int flickSide;          // -1 left, 1 right, 0 none: side the stick is resting on
    float flickSideTime;
    int flickSweep;         // side the stick swept through on its way up (-1 / 1 / 0)
    bool flickSettling;     // waiting a moment so diagonals on the keyboard register
    float flickSettleTime;
    float flickSettleAngle;
    float stickX, stickY;   // current flick stick, -1..1 (up and right are positive)
    float mouseStickX, mouseStickY;

    // tricks
    string trickName;       // null when no trick is running
    float trickTime;        // 0..1 progress of the current trick
    float trickRoll;        // total board roll for the trick (kickflip = 360)
    float trickYaw;         // total board yaw for the trick (shove-it = 180)
    int trickPoints;
    float trickScale;       // longer tricks (like the 360 flip) take more time

    // rocket
    float rocketFuel = 1f;
    float rocketCooldown;
    bool boosting;
    ParticleEffect rocketFlame;
    float camFov = 60f;

    // manual: balance goes from -1 (nose down) to 1 (tail down); past either end you lose it
    bool manual;
    float manualTime;
    float manualBalance;
    float manualNoise;
    float manualNoiseTimer;
    float manualPoints;

    // grab
    bool grabbing;
    float grabTime;
    string grabName;            // INDY GRAB, NOSEGRAB or TAILGRAB, picked by W / S when the grab starts
    bool showHelp;

    // grind: 50-50 when you land straight on an edge, boardslide when you land turned 90 degrees
    bool grinding;
    bool grindSlide;
    string grindName;           // 50-50, NOSEGRIND, 5-0 or BOARDSLIDE
    int grindSide;              // side the edge drops off on (-1 left, 1 right), used to hop off
    float grindDistance, grindTime, grindPoints, grindCooldown;
    ParticleEffect grindSparks;

    // powerslide (C)
    bool powersliding;
    float slideTime, slideYaw;
    int slideDir;
    float carve;                // smoothed steering, used to lean the board into turns

    // camera modes and slow motion, for filming clips
    int camMode;                // 0 = skate cam, 1 = filmer (low fisheye), 2 = far follow
    bool slowMo;

    // session marker (Skate's "set spot / go back")
    bool markerSet;
    Vector3 markerPos;
    float markerHeading;

    // spawned skatepark props
    readonly List<Prop> ramps = new List<Prop>();
    int nextRamp;

    // Hall of Meat: score your bails
    bool meatActive;
    Ped meatPed;
    Vector3 meatStart;
    float meatTime, meatDistance, meatSpeed;
    int meatHealth;

    // score
    int comboPoints;
    int comboMultiplier;
    int totalScore;
    int bestCombo;
    bool trickFromFlick;                                    // flick-it tricks score 1.5x
    readonly List<string> comboTricks = new List<string>(); // shown in the trick feed
    readonly HashSet<string> comboTags = new HashSet<string>(); // facts the challenges check

    // HUD flash ("+1200", "BAIL!", "SKETCHY", ...)
    string flashText, flashSub;
    Color flashColor;
    float flashTime;

    // challenges, saved in scripts\SkateInGTA_progress.ini
    ScriptSettings progress;
    readonly bool[] challengeDone = new bool[ChallengeIds.Length];
    bool greeted;

    static readonly string[] ChallengeIds =
    {
        "ollie", "kickflip", "heelflip", "flick", "spin", "manual", "grind",
        "boardslide", "flip360", "rocket", "combo5", "score5000", "meat"
    };
    static readonly string[] ChallengeTexts =
    {
        "Land an ollie (hold SPACE, release)",
        "Land a kickflip (Q)",
        "Land a heelflip (E)",
        "Flick-it kickflip: hold left mouse, pull down, flick up-left",
        "Land a 180 (hold A or D in the air)",
        "Manual for 3 seconds (hold CTRL, balance with W / S)",
        "Grind 3 meters on a rail, ledge or curb",
        "Land a boardslide (land on a rail turned 90 degrees)",
        "Land a 360 flip (flick: down, left, up)",
        "Rocket jump and land a flip (SHIFT + SPACE, then Q)",
        "Land a 5-trick combo",
        "Score 5,000 in one combo",
        "Hall of Meat: score 1,000 with a bail (X)"
    };
    static readonly string[] FlipTricks = { "KICKFLIP", "HEELFLIP", "POP SHOVE-IT", "FS SHOVE-IT", "360 FLIP", "VARIAL HEELFLIP" };

    static readonly Random rng = new Random();
    const string RocketFxAsset = "veh_impexp_rocket";   // Rocket Voltic boost flames
    const string RocketFxName = "veh_rocket_boost";
    static readonly float[] RailCatch = { 0.12f, -0.12f, 0.24f, -0.24f };
    static readonly float[] SurfaceSearch = { 0f, 0.06f, -0.06f, 0.12f, -0.12f, 0.2f, -0.2f };
    const float ChargeTime = 0.6f;          // seconds to fully charge an ollie
    const float FlickSettle = 0.07f;        // seconds to wait before reading a flick's direction
    const float FlickMinSideTime = 0.06f;   // the stick must rest on a side this long to count as a sweep
    const IntersectFlags GroundFlags = IntersectFlags.Map | IntersectFlags.Vehicles | IntersectFlags.Objects;

    public SkateInGTA()
    {
        LoadSettings();
        LoadProgress();
        StartSound();
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
        Interval = 0;
    }

    void LoadSettings()
    {
        ScriptSettings s = ScriptSettings.Load("scripts\\SkateInGTA.ini");

        toggleKey = s.GetValue<Keys>("Controls", "ToggleKey", Keys.K);

        markerSetKey = s.GetValue<Keys>("Controls", "MarkerSetKey", Keys.J);
        markerReturnKey = s.GetValue<Keys>("Controls", "MarkerReturnKey", Keys.U);
        rampKey = s.GetValue<Keys>("Controls", "RampKey", Keys.L);
        clearRampsKey = s.GetValue<Keys>("Controls", "ClearRampsKey", Keys.Delete);
        bailKey = s.GetValue<Keys>("Controls", "BailKey", Keys.X);
        slowMoKey = s.GetValue<Keys>("Controls", "SlowMoKey", Keys.B);
        cameraKey = s.GetValue<Keys>("Controls", "CameraKey", Keys.V);
        helpKey = s.GetValue<Keys>("Controls", "HelpKey", Keys.H);

        soundEnabled = s.GetValue<bool>("Sound", "Enabled", true);
        soundVolume = GetFloat(s, "Sound", "Volume", 0.7f);

        rocketEnabled = s.GetValue<bool>("Rocket", "Enabled", true);
        rocketThrust = GetFloat(s, "Rocket", "Thrust", 16f);
        rocketMaxSpeed = GetFloat(s, "Rocket", "MaxSpeed", 38f);
        rocketAirLift = GetFloat(s, "Rocket", "AirLift", 6f);
        rocketJumpBoost = GetFloat(s, "Rocket", "JumpBoost", 8f);
        rocketJumpCost = GetFloat(s, "Rocket", "JumpCost", 0.35f);
        rocketDrain = GetFloat(s, "Rocket", "DrainPerSecond", 0.33f);
        rocketRecharge = GetFloat(s, "Rocket", "RechargePerSecond", 0.2f);
        rocketRechargeDelay = GetFloat(s, "Rocket", "RechargeDelay", 1f);
        flameScale = GetFloat(s, "Rocket", "FlameScale", 0.6f);
        flamePitch = GetFloat(s, "Rocket", "FlamePitch", 0f);
        flameYaw = GetFloat(s, "Rocket", "FlameYaw", 0f);

        manualPointsPerSecond = GetFloat(s, "Manual", "PointsPerSecond", 60f);
        manualDifficulty = GetFloat(s, "Manual", "Difficulty", 1f);

        rampModelNames = s.GetValue<string>("Ramps", "Models",
            "prop_skate_kickers,prop_skate_flatramp,prop_skate_funbox,prop_skate_quartpipe,prop_skate_halfpipe,prop_skate_rail,prop_skate_spiner")
            .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        maxRamps = s.GetValue<int>("Ramps", "Max", 8);
        rampDistance = GetFloat(s, "Ramps", "Distance", 8f);
        rampYawOffset = GetFloat(s, "Ramps", "YawOffset", 0f);

        string models = s.GetValue<string>("Board", "Models", "v_res_skateboard,v_16_skateboard");
        boardModelNames = models.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        anchorModelName = s.GetValue<string>("Board", "AnchorModel", "prop_golf_ball");
        boardHeight = GetFloat(s, "Board", "Height", 0.05f);
        boardYawOffset = GetFloat(s, "Board", "YawOffset", 90f);

        rideAnimDict = s.GetValue<string>("Rider", "AnimDict", "move_strafe@stealth");
        rideAnimClip = s.GetValue<string>("Rider", "AnimClip", "idle");
        pedHeight = GetFloat(s, "Rider", "Height", 1.1f);
        stanceYaw = GetFloat(s, "Rider", "StanceYaw", -90f);

        pushAccel = GetFloat(s, "Physics", "PushAccel", 4.0f);
        pushMaxSpeed = GetFloat(s, "Physics", "PushMaxSpeed", 9.0f);
        maxSpeed = GetFloat(s, "Physics", "MaxSpeed", 22.0f);
        brakeDecel = GetFloat(s, "Physics", "BrakeDecel", 7.0f);
        friction = GetFloat(s, "Physics", "Friction", 0.08f);
        slopeFactor = GetFloat(s, "Physics", "SlopeFactor", 1.0f);
        turnRate = GetFloat(s, "Physics", "TurnRate", 110f);
        gravity = GetFloat(s, "Physics", "Gravity", 9.81f);
        ollieMin = GetFloat(s, "Physics", "OllieMin", 2.8f);
        ollieMax = GetFloat(s, "Physics", "OllieMax", 5.0f);
        bailSpeed = GetFloat(s, "Physics", "BailSpeed", 8.0f);
        maxLandingSpeed = GetFloat(s, "Physics", "MaxLandingSpeed", 15.0f);

        landTolerance = GetFloat(s, "Tricks", "LandTolerance", 35f);
        trickDuration = GetFloat(s, "Tricks", "TrickDuration", 0.45f);
        airSpinRate = GetFloat(s, "Tricks", "AirSpinRate", 420f);
        spinAssist = GetFloat(s, "Tricks", "SpinAssist", 260f);

        flickEnabled = s.GetValue<bool>("FlickIt", "Enabled", true);
        arrowFlick = s.GetValue<bool>("FlickIt", "ArrowKeys", true);
        mouseFlick = s.GetValue<bool>("FlickIt", "Mouse", true);
        mouseSensitivity = GetFloat(s, "FlickIt", "MouseSensitivity", 0.5f);
        invertMouseY = s.GetValue<bool>("FlickIt", "InvertMouseY", false);
        showStick = s.GetValue<bool>("FlickIt", "ShowStick", true);

        useSkateCam = s.GetValue<bool>("Camera", "UseSkateCam", true);
        camDistance = GetFloat(s, "Camera", "Distance", 4.0f);
        camHeight = GetFloat(s, "Camera", "Height", 1.6f);
    }

    // Reads a number with "." as the decimal point on every PC (a German Windows would otherwise expect ",").
    static float GetFloat(ScriptSettings s, string section, string key, float fallback)
    {
        string raw = s.GetValue<string>(section, key, null);
        float value;
        if (raw != null && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
        return fallback;
    }

    // ================================================================ start / stop

    void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (Game.IsPaused) return;

        if (e.KeyCode == toggleKey)
        {
            if (riding) StopRiding();
            else StartRiding(false);
        }
        else if (e.KeyCode == markerSetKey) SetMarker();
        else if (e.KeyCode == markerReturnKey) ReturnToMarker();
        else if (e.KeyCode == rampKey) SpawnRamp();
        else if (e.KeyCode == clearRampsKey) ClearRamps(true);
        else if (e.KeyCode == bailKey && riding) Bail("Bailed on purpose.");
        else if (e.KeyCode == slowMoKey)
        {
            slowMo = !slowMo;
            Game.TimeScale = slowMo ? 0.4f : 1f;
            Screen.ShowSubtitle(slowMo ? "Slow motion ON" : "Slow motion OFF", 1000);
        }
        else if (e.KeyCode == helpKey) showHelp = !showHelp;
        else if (e.KeyCode == cameraKey && riding)
        {
            camMode = (camMode + 1) % 3;
            Screen.ShowSubtitle(camMode == 0 ? "Skate cam" : camMode == 1 ? "Filmer cam (fisheye)" : "Far cam", 1000);
        }
    }

    void StartRiding(bool ignoreRagdoll)
    {
        Ped ped = Game.Player.Character;
        if (ped.IsDead || ped.IsInVehicle() || (ped.IsRagdoll && !ignoreRagdoll) || Game.IsCutsceneActive)
        {
            Notification.Show("~r~Skate in GTA:~s~ get on your feet first.");
            return;
        }

        Model anchorModel = LoadModel(anchorModelName);
        Model boardModel = null;
        foreach (string name in boardModelNames)
        {
            boardModel = LoadModel(name.Trim());
            if (boardModel != null) break;
        }

        if (anchorModel == null && boardModel == null)
        {
            Notification.Show("~r~Skate in GTA:~s~ could not load any prop model. Check SkateInGTA.ini.");
            return;
        }
        if (anchorModel == null) anchorModel = boardModel;
        if (boardModel == null)
            Notification.Show("~y~Skate in GTA:~s~ board model not found, riding an invisible board. Change [Board] Models in the ini.");

        // Start on the ground right under the player.
        heading = ped.Heading;
        pos = ped.Position;
        RaycastResult ground = World.Raycast(pos + Vector3.WorldUp, pos - Vector3.WorldUp * 3f, GroundFlags, ped);
        if (ground.DidHit)
        {
            pos = ground.HitPosition;
            groundNormal = ground.SurfaceNormal;
        }

        anchor = World.CreateProp(anchorModel, pos, false, false);
        if (anchor == null)
        {
            Notification.Show("~r~Skate in GTA:~s~ failed to spawn the board.");
            return;
        }
        anchor.IsPositionFrozen = true;
        anchor.IsCollisionEnabled = false;
        anchor.IsVisible = false;

        if (boardModel != null)
        {
            board = World.CreateProp(boardModel, pos, false, false);
            if (board != null)
            {
                board.IsCollisionEnabled = false;
            }
        }

        anchorModel.MarkAsNoLongerNeeded();
        if (boardModel != null) boardModel.MarkAsNoLongerNeeded();

        ped.CanRagdoll = false;
        ped.Weapons.Select(WeaponHash.Unarmed, true); // no skating with a rifle in your hand
        rider = ped;
        animGiveUpTime = DateTime.Now.AddSeconds(3);
        PlayRideAnimation(ped);

        speed = 0f;
        groundVz = 0f;
        fakie = false;
        airborne = false;
        airSpin = 0f;
        ollieCharge = 0f;
        jumpLatch = Pressed(Control.Jump);
        ResetFlick();
        flickNeedsCenter = true;
        mouseStickX = 0f;
        mouseStickY = 0f;
        ResetMoves();
        boosting = false;
        meatActive = false;
        camFov = 60f;
        if (rocketEnabled) new ParticleEffectAsset(RocketFxAsset).Request(); // load the flames now, not mid-boost
        new ParticleEffectAsset("core").Request();
        riding = true;

        UpdateEntities(ped);

        if (useSkateCam)
        {
            camPos = pos - ForwardFromHeading(heading) * camDistance + Vector3.WorldUp * camHeight;
            cam = World.CreateCamera(camPos, Vector3.Zero, 60f);
            World.RenderingCamera = cam;
        }

        Notification.Show("~g~Skate in GTA~s~~n~W push, S brake, A/D steer, SPACE ollie~n~Q/E/R flips, hold LMB + flick mouse = Skate tricks~n~SHIFT rocket, CTRL manual, F grab, C powerslide~n~Land on rails / ledges / curbs to grind~n~" + cameraKey + " camera, " + slowMoKey + " slow-mo, " + bailKey + " bail, " + toggleKey + " get off");
    }

    // Ends the session cleanly. Used for hopping off, dying, entering a car, or the script unloading.
    void StopRiding()
    {
        Ped ped = rider != null ? rider : Game.Player.Character;
        riding = false;
        rider = null;

        if (ped != null && ped.Exists())
        {
            ped.Detach();
            ped.CanRagdoll = true;
            ped.Task.ClearAll();
            if (!ped.IsDead) ped.Position = pos + Vector3.WorldUp; // ped position is its hips, ~1 m above the feet
        }

        ResetMoves();
        DeleteProps();
        DestroyCamera();
    }

    // Clears every trick / move state. Points of an unfinished combo are lost.
    void ResetMoves()
    {
        trickName = null;
        manual = false;
        grabbing = false;
        grinding = false;
        powersliding = false;
        slideYaw = 0f;
        StopGrindSparks();
        comboPoints = 0;
        comboMultiplier = 0;
        comboTricks.Clear();
        comboTags.Clear();
    }

    void Bail(string reason)
    {
        Ped ped = rider;
        Vector3 velocity = airborne ? airVelocity : ForwardFromHeading(heading) * speed;

        riding = false;
        rider = null;
        ped.Detach();
        ped.CanRagdoll = true;
        ped.Ragdoll(2500, RagdollType.Balance);
        ped.Velocity = velocity;
        StartHallOfMeat(ped, velocity);
        ResetMoves();

        // Let the board fly away on its own physics, then let the game clean it up.
        if (board != null && board.Exists())
        {
            board.Detach();
            board.IsCollisionEnabled = true;
            board.IsPositionFrozen = false;
            board.Velocity = velocity * 1.2f + Vector3.WorldUp * 2f;
            board.MarkAsNoLongerNeeded();
            board = null;
        }
        DeleteProps();
        DestroyCamera();

        Flash("BAIL!", reason + "   " + markerReturnKey + " = back to your spot, " + toggleKey + " = ride again", Color.FromArgb(255, 70, 70));
        FrontendSound("CHECKPOINT_MISSED", "HUD_MINI_GAME_SOUNDSET");
    }

    void OnAborted(object sender, EventArgs e)
    {
        if (slowMo) Game.TimeScale = 1f;
        StopSound();
        if (riding) StopRiding();
        else
        {
            DeleteProps();
            DestroyCamera();
        }
        ClearRamps(false);
    }

    void DeleteProps()
    {
        StopRocket();
        if (board != null && board.Exists()) board.Delete();
        if (anchor != null && anchor.Exists()) anchor.Delete();
        board = null;
        anchor = null;
    }

    void DestroyCamera()
    {
        if (cam != null)
        {
            World.RenderingCamera = null;
            cam.Delete();
            cam = null;
        }
    }

    // ================================================================ main loop

    void OnTick(object sender, EventArgs e)
    {
        if (!greeted && !Game.IsLoading && Game.Player.Character.Exists())
        {
            greeted = true;
            Notification.Show("~g~Skate in GTA~s~ loaded. Press ~b~" + toggleKey + "~s~ to skate, ~b~" + helpKey + "~s~ for all controls.~n~Challenges done: " + ChallengesDone() + " / " + ChallengeIds.Length);
        }
        UpdateSoundLoops();
        DrawFlash();
        if (showHelp) DrawHelp();
        if (meatActive) UpdateHallOfMeat();
        if (!riding) return;

        Ped ped = rider;
        if (ped == null || !ped.Exists() || ped != Game.Player.Character || ped.IsDead || ped.IsInVehicle() || Game.IsCutsceneActive || anchor == null || !anchor.Exists())
        {
            StopRiding();
            return;
        }
        if (Game.IsPaused) return;

        float dt = Game.LastFrameTime;
        if (dt <= 0f) return;
        if (dt > 0.05f) dt = 0.05f; // avoid huge jumps after a lag spike
        if (grindCooldown > 0f) grindCooldown -= dt;

        DisableOnFootControls();

        if (ped.IsInWater)
        {
            Bail("Skateboards don't float.");
            return;
        }

        ReadFlickStick(dt);
        UpdateRocket(dt);

        if (airborne) UpdateAir(dt);
        else UpdateGround(dt);

        if (!riding) return; // we may have bailed above

        UpdateEntities(ped);
        UpdateCamera(dt);
        DrawStick();
        DrawHud();
        DrawTrickFeed();
    }

    // Stop GTA from using our buttons for running, jumping, punching, etc.
    void DisableOnFootControls()
    {
        Control[] controls = new Control[]
        {
            Control.MoveLeftRight, Control.MoveUpDown, Control.MoveUpOnly, Control.MoveDownOnly,
            Control.MoveLeftOnly, Control.MoveRightOnly, Control.Jump, Control.Sprint, Control.Duck,
            Control.Cover, Control.Context, Control.Reload, Control.Enter, Control.Attack, Control.Aim,
            Control.MeleeAttackLight, Control.MeleeAttackHeavy, Control.MeleeAttackAlternate,
            Control.SelectWeapon, Control.WeaponWheelNext, Control.WeaponWheelPrev,
            Control.Phone, Control.PhoneUp, Control.PhoneDown, Control.PhoneLeft, Control.PhoneRight,
            Control.CharacterWheel, Control.SelectCharacterMichael, Control.SelectCharacterFranklin,
            Control.SelectCharacterTrevor, Control.LookLeftRight, Control.LookUpDown, Control.LookBehind
        };
        foreach (Control c in controls) Game.DisableControlThisFrame(c);
    }

    // ---------------------------------------------------------------- ground physics

    void UpdateGround(float dt)
    {
        if (grinding)
        {
            UpdateGrind(dt);
            return;
        }

        float steer = Game.GetDisabledControlValueNormalized(Control.MoveLeftRight);  // -1 left .. 1 right
        float throttle = -Game.GetDisabledControlValueNormalized(Control.MoveUpDown); // 1 = W / stick up

        // Manual: hold CTRL to roll on the back wheels. W / S keep your balance instead of pushing.
        if (manual)
        {
            if (!Pressed(Control.Duck) || speed < 1f)
            {
                EndManual();
                BankCombo();
            }
            else if (UpdateBalance(dt, throttle, manualDifficulty))
            {
                manual = false;
                comboPoints = 0;
                comboMultiplier = 0;
                comboTricks.Clear();
                comboTags.Clear();
                Flash("LOST BALANCE", "combo lost", Color.FromArgb(255, 70, 70));
                return;
            }
            else
            {
                manualPoints += manualPointsPerSecond * dt;
                manualTime += dt;
            }
        }
        else if (Pressed(Control.Duck) && speed > 2f)
        {
            StartManual();
        }

        // Powerslide: hold C while rolling. Slows you down fast, looks cool, scores a little.
        if (Pressed(Control.LookBehind) && speed > 3f && !manual)
        {
            if (!powersliding)
            {
                powersliding = true;
                slideTime = 0f;
                slideDir = steer < 0f ? -1 : 1;
            }
            slideTime += dt;
            speed = Math.Max(0f, speed - 9f * dt);
        }
        else if (powersliding)
        {
            powersliding = false;
            if (slideTime > 0.4f)
            {
                AddTrickText("POWERSLIDE", 30 + (int)(slideTime * 40f));
                BankCombo();
            }
        }

        // Push and brake.
        if (!manual)
        {
            if (throttle > 0.1f && speed < pushMaxSpeed) speed += pushAccel * throttle * dt;
            if (throttle < -0.1f) speed -= brakeDecel * -throttle * dt;
        }

        // Rocket boost.
        if (boosting && speed < rocketMaxSpeed) speed += rocketThrust * dt;

        // Rolling friction.
        speed -= speed * friction * dt;

        // Hills: gravity pulls you down the slope.
        Vector3 forward = ForwardFromHeading(heading);
        float uphill = -Vector3.Dot(groundNormal, forward); // > 0 when the ground rises in front of us
        speed -= gravity * uphill * slopeFactor * dt;

        if (speed < 0f) speed = 0f;
        float limit = boosting ? rocketMaxSpeed : maxSpeed;
        if (speed > limit) speed = Math.Max(limit, speed - 8f * dt); // rocket speed fades out smoothly

        // Steering. Turning is a bit sharper at low speed, like a real board.
        float turnScale = 1.0f - 0.4f * Math.Min(speed / maxSpeed, 1f);
        heading -= steer * turnRate * turnScale * dt;

        if (HandlePopInputs(dt)) return;

        // Walls: look a little ahead of the board.
        float step = speed * dt;
        if (step > 0.0001f && HitWall(pos, forward, step + 0.45f))
        {
            if (speed > bailSpeed)
            {
                Bail("You hit a wall.");
                return;
            }
            speed = 0f;
            step = 0f;
        }

        // Follow the ground. We search from a bit above (to climb curbs) to a bit below (to roll down slopes).
        Vector3 next = pos + forward * step;
        float maxDrop = 0.35f + step;
        RaycastResult ground = World.Raycast(next + Vector3.WorldUp * 0.6f, next - Vector3.WorldUp * (maxDrop + 0.2f), GroundFlags, anchor);

        if (ground.DidHit && ground.HitPosition.Z >= next.Z - maxDrop && ground.SurfaceNormal.Z > 0.45f)
        {
            float vz = dt > 0f ? (ground.HitPosition.Z - pos.Z) / dt : 0f;
            groundVz = groundVz + (vz - groundVz) * Math.Min(dt * 12f, 1f);
            pos = ground.HitPosition;
            groundNormal = ground.SurfaceNormal;
        }
        else
        {
            // The ground fell away (edge of a ramp, stairs, a ledge): we're flying.
            pos = next;
            Launch(forward * speed + Vector3.WorldUp * Math.Max(groundVz, 0f));
        }
    }

    // Ollie (hold SPACE, release), one-key tricks (Q / E / R) and Flick-it. Returns true if we popped.
    bool HandlePopInputs(float dt)
    {
        if (jumpLatch && !Pressed(Control.Jump)) jumpLatch = false;
        if (!jumpLatch && Pressed(Control.Jump))
        {
            ollieCharge = Math.Min(ollieCharge + dt / ChargeTime, 1f);
        }
        else if (ollieCharge > 0f)
        {
            if (!Pop(ollieCharge)) AddTrickText("OLLIE", 0);
            return true;
        }

        // One-key tricks pop and flip in one go (hold SPACE first for a higher pop).
        if (TryButtonTrick(true)) return true;

        return UpdateFlick(dt);
    }

    // Jump off the ground. charge 0..1 decides how high. Holding the rocket button makes it a
    // rocket jump, like the Scramjet's jump. Returns true for a rocket jump.
    bool Pop(float charge)
    {
        float pop = ollieMin + (ollieMax - ollieMin) * charge;
        if (Pressed(Control.Jump)) jumpLatch = true;
        ollieCharge = 0f;
        if (manual) EndManual(); // ollie out of a manual or grind: the combo keeps going
        if (grinding) EndGrind();
        PlaySound("pop");

        bool rocketJump = rocketEnabled && Pressed(Control.Sprint) && rocketFuel >= rocketJumpCost;
        if (rocketJump)
        {
            pop += rocketJumpBoost;
            rocketFuel -= rocketJumpCost;
            rocketCooldown = rocketRechargeDelay;
            AddTrickText("ROCKET JUMP", 50);
            comboTags.Add("ROCKET");
        }
        Launch(ForwardFromHeading(heading) * speed + Vector3.WorldUp * (pop + Math.Max(groundVz, 0f)));
        return rocketJump;
    }

    // Q / E / R tricks. On the ground they pop first, in the air they just flip.
    bool TryButtonTrick(bool onGround)
    {
        if (trickName != null) return false;

        string name = null;
        if (JustPressed(Control.Cover)) name = "KICKFLIP";
        else if (JustPressed(Control.Context)) name = "HEELFLIP";
        else if (JustPressed(Control.Reload)) name = "POP SHOVE-IT";
        if (name == null) return false;

        if (onGround) Pop(Math.Max(ollieCharge, 0.5f));
        StartNamedTrick(name);
        trickFromFlick = false;
        return onGround;
    }

    void Launch(Vector3 velocity)
    {
        airborne = true;
        airVelocity = velocity;
        airSpin = 0f;
        powersliding = false;
        pos += Vector3.WorldUp * 0.05f;
    }

    // ---------------------------------------------------------------- air physics and tricks

    void UpdateAir(float dt)
    {
        float steer = Game.GetDisabledControlValueNormalized(Control.MoveLeftRight);
        if (Math.Abs(steer) > 0.1f)
        {
            airSpin -= steer * airSpinRate * dt;
        }
        else if (spinAssist > 0f)
        {
            // Spin assist: when you let go close to 0 / 180 / 360..., it finishes the last bit for you.
            // Only the last 30 degrees, so you still have to commit to the spin yourself.
            float target = (float)Math.Round(airSpin / 180f) * 180f;
            float diff = target - airSpin;
            float stepDeg = spinAssist * dt;
            if (Math.Abs(diff) <= 30f) airSpin += Math.Abs(diff) <= stepDeg ? diff : Math.Sign(diff) * stepDeg;
        }

        // Start a flip trick if none is running.
        if (trickName == null)
        {
            if (!TryButtonTrick(false)) UpdateFlick(dt);
        }
        else
        {
            UpdateFlick(dt); // keep the gesture state up to date
            trickTime += dt / (trickDuration * trickScale);
            if (trickTime >= 1f) CompleteTrick();
        }

        // Grab: hold F. Let go before you land!
        if (Pressed(Control.Enter))
        {
            if (!grabbing)
            {
                float t = -Game.GetDisabledControlValueNormalized(Control.MoveUpDown);
                grabName = t > 0.5f ? "NOSEGRAB" : t < -0.5f ? "TAILGRAB" : "INDY GRAB";
                grabTime = 0f;
            }
            grabbing = true;
            grabTime += dt;
        }
        else if (grabbing)
        {
            EndGrab();
        }

        if (boosting)
        {
            airVelocity += ForwardFromHeading(heading) * rocketThrust * dt + Vector3.WorldUp * rocketAirLift * dt;
            float fs = (float)Math.Sqrt(airVelocity.X * airVelocity.X + airVelocity.Y * airVelocity.Y);
            if (fs > rocketMaxSpeed)
            {
                airVelocity.X *= rocketMaxSpeed / fs;
                airVelocity.Y *= rocketMaxSpeed / fs;
            }
        }

        airVelocity.Z -= gravity * dt;
        Vector3 next = pos + airVelocity * dt;

        // Walls while flying.
        Vector3 flat = new Vector3(airVelocity.X, airVelocity.Y, 0f);
        float flatSpeed = flat.Length();
        if (flatSpeed > 0.01f && HitWall(pos, flat / flatSpeed, flatSpeed * dt + 0.45f))
        {
            if (flatSpeed > bailSpeed)
            {
                Bail("You hit a wall.");
                return;
            }
            airVelocity.X = 0f;
            airVelocity.Y = 0f;
            next = new Vector3(pos.X, pos.Y, next.Z);
        }

        // Landing: only check while falling.
        if (airVelocity.Z <= 0f)
        {
            RaycastResult ground = World.Raycast(new Vector3(next.X, next.Y, pos.Z + 0.5f), next - Vector3.WorldUp * 0.05f, GroundFlags, anchor);
            if (!ground.DidHit && grindCooldown <= 0f && trickName == null && !grabbing && -airVelocity.Z <= maxLandingSpeed)
            {
                // Thin rails are easy to miss with one ray, so look a little to the sides too ("rail magnet").
                Vector3 fwd = ForwardFromHeading(heading);
                Vector3 right = new Vector3(fwd.Y, -fwd.X, 0f);
                foreach (float side in RailCatch)
                {
                    Vector3 q = next + right * side;
                    RaycastResult r = World.Raycast(new Vector3(q.X, q.Y, pos.Z + 0.5f), q - Vector3.WorldUp * 0.05f, GroundFlags, anchor);
                    if (r.DidHit && TryStartGrind(r, flatSpeed)) return;
                }
            }
            if (ground.DidHit)
            {
                Land(ground, flatSpeed);
                return;
            }
        }

        pos = next;
    }

    void CompleteTrick()
    {
        int points = trickFromFlick ? trickPoints * 3 / 2 : trickPoints;
        if (trickFromFlick) comboTags.Add("FLICK " + trickName);
        AddTrickText(trickName, points);
        trickName = null;
        trickTime = 0f;
    }

    void StartTrick(string name, float roll, float yaw, int points, float scale)
    {
        PlaySound("flip");
        trickName = name;
        trickTime = 0f;
        trickRoll = roll;
        trickYaw = yaw;
        trickPoints = points;
        trickScale = scale;
    }

    // All flip tricks in one place: board roll (flip), board yaw (shove-it), points, time.
    void StartNamedTrick(string name)
    {
        if (name == "KICKFLIP") StartTrick(name, 360f, 0f, 100, 1f);
        else if (name == "HEELFLIP") StartTrick(name, -360f, 0f, 100, 1f);
        else if (name == "POP SHOVE-IT") StartTrick(name, 0f, 180f, 80, 1f);
        else if (name == "FS SHOVE-IT") StartTrick(name, 0f, -180f, 80, 1f);
        else if (name == "360 FLIP") StartTrick(name, 360f, 360f, 250, 1.3f);
        else if (name == "VARIAL HEELFLIP") StartTrick(name, -360f, -180f, 180, 1.15f);
    }

    // ================================================================ Flick-it
    //
    // In Skate you do tricks by "flicking" the right stick. Here the stick can be a controller's
    // right stick, the arrow keys, or the mouse. All gestures start by pulling the stick DOWN
    // (crouch), then flicking it somewhere (pop + trick). Regular stance:
    //
    //   down, up            OLLIE              up, down            NOLLIE
    //   down, up-left       KICKFLIP           down, up-right      HEELFLIP
    //   down, left          POP SHOVE-IT       down, right         FS SHOVE-IT
    //   down, left, up      360 FLIP           down, right, up     VARIAL HEELFLIP

    void ReadFlickStick(float dt)
    {
        stickX = 0f;
        stickY = 0f;

        // Hold the left mouse button to grab the "stick" with the mouse; let go and it snaps back
        // to the middle. Same idea as skate.'s hidden mouse controls, and no accidental tricks.
        if (!mouseFlick || !Pressed(Control.Attack))
        {
            mouseStickX = 0f;
            mouseStickY = 0f;
        }
        else
        {
            mouseStickX += Game.GetDisabledControlValueNormalized(Control.LookLeftRight) * mouseSensitivity;
            mouseStickY -= Game.GetDisabledControlValueNormalized(Control.LookUpDown) * mouseSensitivity * (invertMouseY ? -1f : 1f);
            float len = (float)Math.Sqrt(mouseStickX * mouseStickX + mouseStickY * mouseStickY);
            if (len > 1f)
            {
                mouseStickX /= len;
                mouseStickY /= len;
            }
        }

        if (!flickEnabled) return;

        if (Game.LastInputMethod == InputMethod.GamePad)
        {
            stickX = Game.GetDisabledControlValueNormalized(Control.LookLeftRight);
            stickY = -Game.GetDisabledControlValueNormalized(Control.LookUpDown);
            return;
        }

        if (arrowFlick)
        {
            float ax = (Game.IsKeyPressed(Keys.Right) ? 1f : 0f) - (Game.IsKeyPressed(Keys.Left) ? 1f : 0f);
            float ay = (Game.IsKeyPressed(Keys.Up) ? 1f : 0f) - (Game.IsKeyPressed(Keys.Down) ? 1f : 0f);
            if (ax != 0f || ay != 0f)
            {
                float len = (float)Math.Sqrt(ax * ax + ay * ay);
                stickX = ax / len;
                stickY = ay / len;
                return;
            }
        }

        if (mouseFlick)
        {
            stickX = mouseStickX;
            stickY = mouseStickY;
        }
    }

    void ResetFlick()
    {
        flickState = FlickIdle;
        flickCharge = 0f;
        flickCenterTime = 0f;
        flickSide = 0;
        flickSideTime = 0f;
        flickSweep = 0;
        flickSettling = false;
        flickSettleTime = 0f;
    }

    // Returns true if the gesture made us jump off the ground this frame.
    bool UpdateFlick(float dt)
    {
        if (!flickEnabled) return false;

        float m = (float)Math.Sqrt(stickX * stickX + stickY * stickY);
        float a = RadToDeg((float)Math.Atan2(stickY, stickX)); // 0 right, 90 up, 180 / -180 left, -90 down
        bool far = m > 0.7f;
        bool isDown = far && a >= -135f && a <= -45f;
        bool isUp = far && a >= 45f && a <= 135f;
        bool upperHalf = far && a > 20f && a < 160f;
        int side = 0;
        if (far && Math.Abs(a) >= 150f) side = -1;
        else if (far && Math.Abs(a) <= 30f) side = 1;

        if (flickNeedsCenter)
        {
            if (m < 0.3f) flickNeedsCenter = false;
            return false;
        }

        if (flickState == FlickIdle)
        {
            if (isDown) { ResetFlick(); flickState = FlickFromDown; }
            else if (isUp) { ResetFlick(); flickState = FlickFromUp; }
            return false;
        }

        if (flickState == FlickFromUp)
        {
            if (isDown) return FireFlick("NOLLIE", 0.6f);
            if (m < 0.3f)
            {
                flickCenterTime += dt;
                if (flickCenterTime > 0.25f) ResetFlick();
            }
            else flickCenterTime = 0f;
            return false;
        }

        // FlickFromDown: crouching, waiting for the flick.
        flickCharge = Math.Min(flickCharge + dt / ChargeTime, 1f);

        if (flickSettling)
        {
            flickSettleTime += dt;
            if (upperHalf) flickSettleAngle = a;
            if (flickSettleTime < FlickSettle) return false;

            string name;
            if (flickSweep < 0) name = "360 FLIP";
            else if (flickSweep > 0) name = "VARIAL HEELFLIP";
            else if (flickSettleAngle >= 112.5f) name = "KICKFLIP";
            else if (flickSettleAngle <= 67.5f) name = "HEELFLIP";
            else name = "OLLIE";
            return FireFlick(name, Math.Max(flickCharge, 0.4f));
        }

        if (upperHalf)
        {
            if (flickSide != 0 && flickSideTime >= FlickMinSideTime) flickSweep = flickSide;
            flickSettling = true;
            flickSettleTime = 0f;
            flickSettleAngle = a;
            return false;
        }

        if (side != 0)
        {
            flickCenterTime = 0f;
            if (side == flickSide) flickSideTime += dt;
            else { flickSide = side; flickSideTime = dt; }
            if (flickSideTime > 0.35f) return FireFlick(side < 0 ? "POP SHOVE-IT" : "FS SHOVE-IT", Math.Max(flickCharge, 0.4f));
            return false;
        }

        if (m < 0.3f)
        {
            flickCenterTime += dt;
            // Down, side, let go = shove-it. Down and let go = cancel.
            if (flickSide != 0 && flickSideTime >= FlickMinSideTime && flickCenterTime > 0.12f)
                return FireFlick(flickSide < 0 ? "POP SHOVE-IT" : "FS SHOVE-IT", Math.Max(flickCharge, 0.4f));
            if (flickCenterTime > 0.25f) ResetFlick();
        }
        else flickCenterTime = 0f;

        return false;
    }

    bool FireFlick(string name, float charge)
    {
        ResetFlick();
        flickNeedsCenter = true;

        bool popped = false;
        if (!airborne)
        {
            Pop(charge);
            popped = true;
        }

        if (name == "OLLIE" || name == "NOLLIE")
        {
            if (popped) AddTrickText(name, name == "NOLLIE" ? 20 : 0);
        }
        else if (trickName == null)
        {
            StartNamedTrick(name);
            trickFromFlick = true;
        }
        return popped;
    }

    // Small stick display in the bottom right corner, so you can see what your flicks do.
    void DrawStick()
    {
        if (!showStick || !flickEnabled) return;

        const float cx = 0.93f, cy = 0.82f, size = 0.07f;
        float aspect = Screen.AspectRatio;
        Function.Call(Hash.DRAW_RECT, cx, cy, size / aspect, size, 0, 0, 0, 120, false);
        Function.Call(Hash.DRAW_RECT, cx, cy, 0.003f / aspect, size, 255, 255, 255, 40, false);
        Function.Call(Hash.DRAW_RECT, cx, cy, size / aspect, 0.003f, 255, 255, 255, 40, false);

        float dotX = cx + stickX * (size * 0.45f) / aspect;
        float dotY = cy - stickY * (size * 0.45f);
        bool crouching = flickState == FlickFromDown;
        Function.Call(Hash.DRAW_RECT, dotX, dotY, 0.012f / aspect, 0.012f, 255, crouching ? 200 : 255, crouching ? 0 : 255, 230, false);
    }

    void Land(RaycastResult ground, float flatSpeed)
    {
        pos = ground.HitPosition;
        groundNormal = ground.SurfaceNormal;
        bool sketchy = false;

        if (trickName != null)
        {
            if (trickTime < 0.85f)
            {
                Bail("Didn't finish the " + trickName.ToLower() + ".");
                return;
            }
            CompleteTrick(); // almost done: you land it, but it's sketchy
            sketchy = true;
        }
        if (grabbing)
        {
            Bail("Still holding the grab.");
            return;
        }
        if (-airVelocity.Z > maxLandingSpeed)
        {
            Bail("That drop was too big.");
            return;
        }
        if (!sketchy && TryStartGrind(ground, flatSpeed)) return;
        if (ground.SurfaceNormal.Z < 0.6f)
        {
            Bail("Landed on a slope that's too steep.");
            return;
        }

        // Spins must land close to a multiple of 180 degrees.
        float spin = Math.Abs(airSpin) % 360f;
        float offStraight = Math.Min(spin, 360f - spin);
        float offBackward = Math.Abs(spin - 180f);
        if (Math.Min(offStraight, offBackward) > landTolerance)
        {
            Bail("Landed sideways.");
            return;
        }
        if (Math.Min(offStraight, offBackward) > 15f) sketchy = true;
        int halfTurns = (int)Math.Round(Math.Abs(airSpin) / 180f);
        if (halfTurns > 0)
        {
            AddTrickText((halfTurns * 180) + (offBackward < offStraight ? " (FAKIE)" : ""), halfTurns * 75);
            comboTags.Add("SPIN");
        }
        if (offBackward < offStraight) fakie = !fakie;

        float fallSpeed = -airVelocity.Z;
        airborne = false;
        airSpin = 0f;
        speed = Math.Min(flatSpeed * 0.95f, rocketMaxSpeed);
        groundVz = 0f;
        PlaySound("land");
        if (cam != null && fallSpeed > 7f) cam.Shake(CameraShake.Jolt, Math.Min(fallSpeed / 15f, 1f));

        if (sketchy)
        {
            // Sloppy landing: you keep the combo, but only half of it, and you lose speed.
            comboPoints /= 2;
            speed *= 0.6f;
            Flash("SKETCHY", "land straighter for full points", Color.FromArgb(255, 170, 40));
        }

        // Landing in a manual (holding CTRL) keeps the combo going, like in Skate.
        if (Pressed(Control.Duck)) StartManual();
        else BankCombo();
    }

    void BankCombo()
    {
        int earned = comboPoints * Math.Max(comboMultiplier, 1);
        if (earned > 0)
        {
            totalScore += earned;
            bool record = earned > bestCombo;
            if (record)
            {
                bestCombo = earned;
                progress.SetValue("Stats", "BestCombo", bestCombo);
                progress.Save();
            }
            Flash("+" + earned, record ? "NEW BEST COMBO!" : string.Join(" + ", comboTricks.ToArray()), Color.FromArgb(90, 255, 120));
            if (earned >= 1000) FrontendSound("CHECKPOINT_PERFECT", "HUD_MINI_GAME_SOUNDSET");
            else FrontendSound("CHECKPOINT_NORMAL", "HUD_MINI_GAME_SOUNDSET");
        }
        CheckComboChallenges(earned);
        comboPoints = 0;
        comboMultiplier = 0;
        comboTricks.Clear();
        comboTags.Clear();
    }

    // ================================================================ grinds
    //
    // There is no "rail" in GTA's map data, so we find edges ourselves: an edge is where the
    // surface we stand on ends and the ground beside it is clearly lower (rails, ledges, benches,
    // curbs, spawned prop_skate_rail). Land on one while moving along it and you grind it.

    // Lateral offset (meters, along `right`) of the nearest edge of the surface at height topZ,
    // or NaN if there is no surface under p or no edge within reach * 10 cm.
    float FindEdge(Vector3 p, Vector3 right, float topZ, int reach)
    {
        if (!Supported(p, topZ)) return float.NaN;
        for (int i = 1; i <= reach; i++)
        {
            float d = i * 0.1f;
            if (!Supported(p - right * d, topZ)) return -(d - 0.05f);
            if (!Supported(p + right * d, topZ)) return d - 0.05f;
        }
        return float.NaN;
    }

    // Finds a surface at about height z under p, searching a little to the sides for thin rails.
    // offset = how far to the side it was found (along `right`).
    bool FindSurfaceNear(Vector3 p, Vector3 right, float z, out RaycastResult hit, out float offset)
    {
        foreach (float o in SurfaceSearch)
        {
            Vector3 q = p + right * o;
            hit = World.Raycast(new Vector3(q.X, q.Y, z + 0.5f), new Vector3(q.X, q.Y, z - 0.5f), GroundFlags, anchor);
            if (hit.DidHit && Math.Abs(hit.HitPosition.Z - z) < 0.3f)
            {
                offset = o;
                return true;
            }
        }
        hit = default(RaycastResult);
        offset = 0f;
        return false;
    }

    // True if there is a surface at about height topZ under p (not more than 12 cm lower).
    bool Supported(Vector3 p, float topZ)
    {
        RaycastResult hit = World.Raycast(new Vector3(p.X, p.Y, topZ + 0.4f), new Vector3(p.X, p.Y, topZ - 0.5f), GroundFlags, anchor);
        return hit.DidHit && hit.HitPosition.Z > topZ - 0.12f;
    }

    bool TryStartGrind(RaycastResult ground, float flatSpeed)
    {
        if (grindCooldown > 0f || flatSpeed < 2f) return false;

        Vector3 fwd = ForwardFromHeading(heading);
        Vector3 right = new Vector3(fwd.Y, -fwd.X, 0f);
        Vector3 p0 = ground.HitPosition;
        float e0 = FindEdge(p0, right, p0.Z, 3); // land within 30 cm of the edge
        if (float.IsNaN(e0)) return false;

        // The edge must continue ahead of us, and we must travel along it (within 30 degrees).
        RaycastResult ahead;
        float aheadOffset;
        if (!FindSurfaceNear(p0 + fwd * 0.6f, right, p0.Z, out ahead, out aheadOffset)) return false;
        float e1 = FindEdge(ahead.HitPosition, right, ahead.HitPosition.Z, 3);
        if (float.IsNaN(e1)) return false;
        Vector3 edgeDir = (ahead.HitPosition + right * e1) - (p0 + right * e0);
        float turn = RadToDeg((float)Math.Atan2(Vector3.Dot(edgeDir, right), Vector3.Dot(edgeDir, fwd)));
        if (Math.Abs(turn) > 30f) return false;

        // Board angle decides the grind: straight (or fakie) = 50-50, turned 90 degrees = boardslide.
        float a = ((airSpin % 360f) + 360f) % 360f;
        float off180 = Math.Min(a % 180f, 180f - a % 180f);
        bool slide;
        if (off180 <= landTolerance) slide = false;
        else if (Math.Abs(off180 - 90f) <= landTolerance) slide = true;
        else return false;
        if (!slide && Math.Abs(a - 180f) < 90f) fakie = !fakie;

        heading -= turn;
        grinding = true;
        grindSlide = slide;
        float lean = -Game.GetDisabledControlValueNormalized(Control.MoveUpDown);
        grindName = slide ? "BOARDSLIDE" : lean > 0.5f ? "NOSEGRIND" : lean < -0.5f ? "5-0 GRIND" : "50-50 GRIND";
        grindSide = e0 < 0f ? -1 : 1;
        grindDistance = 0f;
        grindTime = 0f;
        grindPoints = 0f;
        manualBalance = (float)(rng.NextDouble() - 0.5) * 0.1f;
        manualNoiseTimer = 0f;
        airborne = false;
        airSpin = slide ? 90f : 0f;
        pos = p0;
        groundNormal = Vector3.WorldUp;
        speed = Math.Min(flatSpeed * 0.9f, rocketMaxSpeed);
        PlaySound("land");

        ParticleEffectAsset core = new ParticleEffectAsset("core");
        if (core.IsLoaded)
            grindSparks = World.CreateParticleEffect(core, "veh_train_sparks", anchor, new Vector3(0f, 0f, 0.05f), Vector3.Zero, 0.6f, InvertAxisFlags.None);
        return true;
    }

    void UpdateGrind(float dt)
    {
        float steer = Game.GetDisabledControlValueNormalized(Control.MoveLeftRight);
        float throttle = -Game.GetDisabledControlValueNormalized(Control.MoveUpDown);
        Vector3 fwd = ForwardFromHeading(heading);
        Vector3 right = new Vector3(fwd.Y, -fwd.X, 0f);

        if (HandlePopInputs(dt)) return;   // ollie or flip out of the grind (Pop ends it)
        if (throttle < -0.5f || speed < 1.5f)
        {
            HopOffGrind();                 // S steps off, like the brake button in Skate
            return;
        }

        if (UpdateBalance(dt, -steer, manualDifficulty * (grindSlide ? 1.1f : grindName == "50-50 GRIND" ? 0.7f : 0.95f)))
        {
            Bail(grindSlide ? "Slipped off the boardslide." : "Lost balance on the grind.");
            return;
        }

        speed -= speed * 0.3f * dt;        // grinding is slower than rolling
        if (boosting && speed < rocketMaxSpeed) speed += rocketThrust * dt;

        float step = speed * dt;
        if (HitWall(pos, fwd, step + 0.45f))
        {
            if (speed > bailSpeed)
            {
                Bail("You hit a wall.");
                return;
            }
            HopOffGrind();
            return;
        }

        Vector3 next = pos + fwd * step;
        RaycastResult top;
        float off;
        if (!FindSurfaceNear(next, right, pos.Z, out top, out off))
        {
            // End of the rail: keep the speed and fly off it.
            EndGrind();
            pos = next;
            Launch(fwd * speed + Vector3.WorldUp * 1.5f);
            return;
        }
        if (off != 0f) heading -= RadToDeg((float)Math.Atan2(off, 1f)); // steer along a rail that bends or sits at an angle

        float edge = FindEdge(top.HitPosition, right, top.HitPosition.Z, 5);
        if (float.IsNaN(edge))
        {
            // The edge ended but the surface goes on: just roll away.
            EndGrind();
            pos = top.HitPosition;
            groundNormal = top.SurfaceNormal;
            return;
        }

        // Stay on the edge: on wide ledges slide toward it (10 cm inside), on thin rails we are already there.
        Vector3 onEdge = top.HitPosition;
        if (Math.Abs(edge) > 0.06f) onEdge += right * ((edge - Math.Sign(edge) * 0.1f) * Math.Min(dt * 8f, 1f));

        // Handrails go down stairs: gravity speeds you up there, like on a hill.
        speed -= gravity * ((onEdge.Z - pos.Z) / Math.Max(step, 0.01f)) * slopeFactor * dt;
        pos = onEdge;
        grindDistance += step;
        grindTime += dt;
        grindPoints += (grindSlide ? 70f : grindName == "50-50 GRIND" ? 50f : 65f) * dt;
    }

    // Leaves the grind and adds its points to the combo (without banking it).
    void EndGrind()
    {
        if (!grinding) return;
        grinding = false;
        grindCooldown = 0.35f;
        airSpin = 0f;
        StopGrindSparks();
        int pts = (int)grindPoints + (int)(grindDistance * 10f);
        AddTrickText(grindName, pts);
        if (grindSlide) comboTags.Add("BOARDSLIDE");
        if (grindDistance >= 3f) comboTags.Add("GRIND 3M");
    }

    void HopOffGrind()
    {
        Vector3 fwd = ForwardFromHeading(heading);
        Vector3 right = new Vector3(fwd.Y, -fwd.X, 0f);
        int side = grindSide;
        EndGrind();
        Launch(fwd * speed * 0.8f + right * (side * 2.5f) + Vector3.WorldUp * 2f);
    }

    void StopGrindSparks()
    {
        if (grindSparks != null && grindSparks.Exists()) grindSparks.Delete();
        grindSparks = null;
    }

    // ================================================================ manuals and grabs

    void StartManual()
    {
        manual = true;
        manualBalance = (float)(rng.NextDouble() - 0.5) * 0.1f;
        manualNoiseTimer = 0f;
        manualPoints = 0f;
        manualTime = 0f;
    }

    // Shared balance mini-game for manuals and grinds. Returns true if you lost your balance.
    // input pushes the marker: W / S in a manual, A / D on a grind.
    bool UpdateBalance(float dt, float input, float difficulty)
    {
        manualNoiseTimer -= dt;
        if (manualNoiseTimer <= 0f)
        {
            manualNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
            manualNoiseTimer = 0.3f;
        }
        // The further you lean, the faster you tip over.
        manualBalance += (manualBalance * 1.2f + manualNoise * 0.6f) * difficulty * dt;
        manualBalance -= input * 2.2f * dt;
        return Math.Abs(manualBalance) > 1f;
    }

    // Leaves the manual and adds its points to the combo (without banking it).
    void EndManual()
    {
        manual = false;
        int pts = (int)manualPoints;
        manualPoints = 0f;
        if (pts > 0) AddTrickText("MANUAL", pts);
        if (manualTime >= 3f) comboTags.Add("MANUAL 3S");
    }

    void EndGrab()
    {
        grabbing = false;
        if (grabTime > 0.15f) AddTrickText(grabName, (grabName == "INDY GRAB" ? 20 : 30) + (int)(grabTime * 150f));
    }

    // ================================================================ rocket

    void UpdateRocket(float dt)
    {
        bool on = rocketEnabled && Pressed(Control.Sprint) && rocketFuel > 0f && !manual;
        if (on)
        {
            rocketFuel = Math.Max(0f, rocketFuel - rocketDrain * dt);
            rocketCooldown = rocketRechargeDelay;
        }
        else if (rocketCooldown > 0f)
        {
            rocketCooldown -= dt;
        }
        else
        {
            rocketFuel = Math.Min(1f, rocketFuel + rocketRecharge * dt);
        }

        if (on && !boosting)
        {
            ParticleEffectAsset asset = new ParticleEffectAsset(RocketFxAsset);
            if (asset.IsLoaded)
                rocketFlame = World.CreateParticleEffect(asset, RocketFxName, anchor, new Vector3(0f, -0.5f, 0.15f),
                    new Vector3(flamePitch, 0f, flameYaw), flameScale, InvertAxisFlags.None);
            Screen.StartEffect(ScreenEffect.RaceTurbo, 0, false);
            if (cam != null) cam.Shake(CameraShake.RoadVibration, 0.6f);
            boosting = true;
        }
        else if (!on && boosting)
        {
            StopRocket();
        }
    }

    void StopRocket()
    {
        if (rocketFlame != null && rocketFlame.Exists()) rocketFlame.Delete();
        rocketFlame = null;
        if (boosting) Screen.StopEffect(ScreenEffect.RaceTurbo);
        if (cam != null) cam.StopShaking();
        boosting = false;
    }

    // ================================================================ spots, ramps, Hall of Meat

    void SetMarker()
    {
        Ped ped = Game.Player.Character;
        markerPos = riding ? pos + Vector3.WorldUp : ped.Position;
        markerHeading = riding ? heading : ped.Heading;
        markerSet = true;
        Notification.Show("~b~Skate spot saved.~s~ Press " + markerReturnKey + " to come back here.");
    }

    void ReturnToMarker()
    {
        if (!markerSet)
        {
            Notification.Show("No skate spot yet. Press " + markerSetKey + " to save one.");
            return;
        }
        Ped ped = Game.Player.Character;
        if (ped.IsDead || ped.IsInVehicle()) return;
        meatActive = false;

        if (riding)
        {
            pos = SnapToGround(markerPos);
            heading = markerHeading;
            speed = 0f;
            airborne = false;
            airSpin = 0f;
            fakie = false;
            ResetMoves();
            camPos = pos - ForwardFromHeading(heading) * camDistance + Vector3.WorldUp * camHeight;
            return;
        }

        ped.Task.ClearAllImmediately(); // also stops a ragdoll after a bail
        ped.PositionNoOffset = markerPos;
        ped.Heading = markerHeading;
        StartRiding(true);
    }

    // Skate 3-style object dropper: spawns GTA's own skatepark props in front of you.
    void SpawnRamp()
    {
        if (rampModelNames.Length == 0) return;
        Ped ped = Game.Player.Character;
        Vector3 origin = riding ? pos : ped.Position;
        float h = riding ? heading : ped.Heading;

        string name = rampModelNames[nextRamp % rampModelNames.Length].Trim();
        nextRamp++;
        Model m = LoadModel(name);
        if (m == null)
        {
            Notification.Show("~r~Ramp model not found:~s~ " + name);
            return;
        }

        Vector3 at = SnapToGround(origin + ForwardFromHeading(h) * rampDistance + Vector3.WorldUp);
        Prop ramp = World.CreateProp(m, at, new Vector3(0f, 0f, h + rampYawOffset), false, true);
        m.MarkAsNoLongerNeeded();
        if (ramp == null) return;
        ramp.IsPositionFrozen = true;

        ramps.Add(ramp);
        while (ramps.Count > maxRamps)
        {
            if (ramps[0].Exists()) ramps[0].Delete();
            ramps.RemoveAt(0);
        }
        Screen.ShowSubtitle("~y~" + name + "~s~   " + rampKey + " = next ramp, " + clearRampsKey + " = remove all", 2500);
    }

    void ClearRamps(bool notify)
    {
        foreach (Prop r in ramps)
            if (r != null && r.Exists()) r.Delete();
        ramps.Clear();
        if (notify) Screen.ShowSubtitle("Ramps removed.", 1500);
    }

    void StartHallOfMeat(Ped ped, Vector3 velocity)
    {
        meatActive = true;
        meatPed = ped;
        meatStart = ped.Position;
        meatTime = 0f;
        meatDistance = 0f;
        meatSpeed = velocity.Length();
        meatHealth = ped.Health;
    }

    // Waits until the ragdoll is over, then scores the bail by distance, speed and damage.
    void UpdateHallOfMeat()
    {
        if (meatPed == null || !meatPed.Exists())
        {
            meatActive = false;
            return;
        }
        meatTime += Game.LastFrameTime;
        meatDistance = Math.Max(meatDistance, meatPed.Position.DistanceTo(meatStart));
        if (!meatPed.IsDead && meatTime < 8f && (meatTime < 1.2f || meatPed.IsRagdoll)) return;

        meatActive = false;
        int damage = Math.Max(0, meatHealth - meatPed.Health);
        int score = (int)(meatDistance * 25f + meatSpeed * 15f + damage * 10f);
        Flash("HALL OF MEAT: " + score, meatDistance.ToString("0.0", CultureInfo.InvariantCulture) + " m, " + damage + " damage",
            Color.FromArgb(255, 110, 110));
        FrontendSound("MEDAL_UP", "HUD_MINI_GAME_SOUNDSET");
        if (score >= 1000) CompleteChallenge("meat");
    }

    Vector3 SnapToGround(Vector3 p)
    {
        Entity ignore = anchor != null ? (Entity)anchor : Game.Player.Character;
        RaycastResult hit = World.Raycast(p + Vector3.WorldUp * 2f, p - Vector3.WorldUp * 6f, GroundFlags, ignore);
        return hit.DidHit ? hit.HitPosition : p;
    }

    // ================================================================ HUD

    void DrawHud()
    {
        float aspect = Screen.AspectRatio;

        if (rocketEnabled)
        {
            // Fuel bar above the stick box. Orange = enough for a rocket jump.
            const float x = 0.93f, y = 0.77f, w = 0.07f, h = 0.012f;
            Rect(x, y, w / aspect, h, 0, 0, 0, 150);
            float fw = w * rocketFuel;
            bool jumpReady = rocketFuel >= rocketJumpCost;
            Rect(x - (w - fw) / 2f / aspect, y, fw / aspect, h, 255, jumpReady ? 140 : 60, 0, 230);
        }

        if (manual || grinding)
        {
            // Balance bar: keep the marker in the middle (W / S in a manual, A / D on a grind).
            const float x = 0.5f, y = 0.86f, w = 0.16f, h = 0.012f;
            Rect(x, y, w / aspect, h, 0, 0, 0, 150);
            Rect(x, y, 0.003f / aspect, h * 1.6f, 255, 255, 255, 120);
            int danger = (int)(Math.Min(Math.Abs(manualBalance), 1f) * 255f);
            Rect(x + manualBalance * (w / 2f) / aspect, y, 0.008f / aspect, h * 1.8f, 255, 255 - danger, 255 - danger, 240);
        }
    }

    static void Rect(float x, float y, float w, float h, int r, int g, int b, int a)
    {
        Function.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, a, false);
    }

    // Adds a trick to the running combo. Doing the same trick again in one combo is worth less
    // (half, then a quarter...), so mixing tricks pays off.
    void AddTrickText(string name, int points)
    {
        int repeats = 0;
        foreach (string t in comboTricks) if (t == name) repeats++;
        if (points > 0)
        {
            comboPoints += points >> Math.Min(repeats, 4);
            comboMultiplier++;
        }
        comboTricks.Add(name);
    }

    // Big text in the middle of the screen for a moment: "+1200", "BAIL!", "SKETCHY"...
    void Flash(string text, string sub, Color color)
    {
        flashText = text;
        flashSub = sub;
        flashColor = color;
        flashTime = 2.2f;
    }

    void DrawFlash()
    {
        if (flashTime <= 0f) return;
        flashTime -= Game.LastFrameTime;
        int alpha = (int)(Math.Min(flashTime / 0.5f, 1f) * 255f);
        new TextElement(flashText, new PointF(640f, 170f), 0.9f, Color.FromArgb(alpha, flashColor), Font.Pricedown, Alignment.Center, true, true).Draw();
        if (!string.IsNullOrEmpty(flashSub))
            new TextElement(flashSub, new PointF(640f, 222f), 0.4f, Color.FromArgb(alpha, 255, 255, 255), Font.ChaletLondon, Alignment.Center, true, true).Draw();
    }

    // Skate-style readout of the running combo, plus score and the next challenge.
    void DrawTrickFeed()
    {
        string live = manual ? "MANUAL" : grinding ? grindName : powersliding ? "POWERSLIDE" : null;
        List<string> shown = new List<string>(comboTricks);
        if (live != null) shown.Add(live);
        if (shown.Count > 0)
        {
            int from = Math.Max(0, shown.Count - 4);
            string line = (from > 0 ? "... + " : "") + string.Join(" + ", shown.GetRange(from, shown.Count - from).ToArray());
            new TextElement(line, new PointF(640f, 70f), 0.55f, Color.FromArgb(255, 230, 80), Font.ChaletComprimeCologne, Alignment.Center, true, true).Draw();
            if (comboPoints > 0)
                new TextElement(comboPoints + " x " + Math.Max(comboMultiplier, 1), new PointF(640f, 105f), 0.5f, Color.White, Font.ChaletComprimeCologne, Alignment.Center, true, true).Draw();
        }

        new TextElement("SCORE " + totalScore + "   BEST COMBO " + bestCombo, new PointF(1260f, 20f), 0.4f, Color.White, Font.ChaletComprimeCologne, Alignment.Right, true, true).Draw();
        int next = Array.IndexOf(challengeDone, false);
        string goal = next < 0 ? "All challenges done! You're a legend." : "CHALLENGE " + (ChallengesDone() + 1) + "/" + ChallengeIds.Length + ": " + ChallengeTexts[next];
        new TextElement(goal, new PointF(20f, 20f), 0.38f, Color.FromArgb(150, 220, 255), Font.ChaletLondon, Alignment.Left, true, true).Draw();
    }

    static readonly string[] HelpLines =
    {
        "SKATE IN GTA - CONTROLS   (H = hide)",
        "K  get on / off        W push   S brake   A / D steer",
        "SPACE  hold + release = ollie (longer = higher)",
        "Q kickflip   E heelflip   R shove-it",
        "LEFT MOUSE hold + mouse down, then flick = Skate tricks",
        "   up = ollie, up-left = kickflip, up-right = heelflip",
        "   left / right = shove-its, left then up = 360 flip",
        "A / D in the air = spin 180 / 360",
        "F in the air = grab (+W nosegrab, +S tailgrab)",
        "Land on rails, ledges, curbs = grind (A / D balance)",
        "   hold W = nosegrind, S = 5-0, land turned 90 = boardslide",
        "CTRL manual (W / S balance)   C powerslide",
        "SHIFT rocket boost   SHIFT + ollie = rocket jump",
        "J save spot   U back to spot   L spawn ramp   DEL remove ramps",
        "V camera   B slow motion   X bail on purpose"
    };

    void DrawHelp()
    {
        Rect(0.2f, 0.5f, 0.38f, 0.04f + HelpLines.Length * 0.034f, 0, 0, 0, 170);
        float y = 360f - HelpLines.Length * 12f;
        for (int i = 0; i < HelpLines.Length; i++)
            new TextElement(HelpLines[i], new PointF(30f, y + i * 24f), i == 0 ? 0.42f : 0.36f,
                i == 0 ? Color.FromArgb(255, 230, 80) : Color.White, Font.ChaletLondon, Alignment.Left, true, true).Draw();
    }

    // ================================================================ challenges

    void LoadProgress()
    {
        progress = ScriptSettings.Load("scripts\\SkateInGTA_progress.ini");
        for (int i = 0; i < ChallengeIds.Length; i++) challengeDone[i] = progress.GetValue<bool>("Challenges", ChallengeIds[i], false);
        bestCombo = progress.GetValue<int>("Stats", "BestCombo", 0);
    }

    int ChallengesDone()
    {
        int n = 0;
        foreach (bool d in challengeDone) if (d) n++;
        return n;
    }

    void CompleteChallenge(string id)
    {
        int i = Array.IndexOf(ChallengeIds, id);
        if (i < 0 || challengeDone[i]) return;
        challengeDone[i] = true;
        progress.SetValue("Challenges", id, true);
        progress.Save();
        totalScore += 500;
        Notification.Show("~g~CHALLENGE COMPLETE~s~ (" + ChallengesDone() + "/" + ChallengeIds.Length + ")~n~" + ChallengeTexts[i] + "~n~+500");
        FrontendSound("MEDAL_GOLD", "HUD_AWARDS");
    }

    // Called when a combo is landed cleanly.
    void CheckComboChallenges(int earned)
    {
        if (comboTricks.Contains("OLLIE")) CompleteChallenge("ollie");
        if (comboTricks.Contains("KICKFLIP")) CompleteChallenge("kickflip");
        if (comboTricks.Contains("HEELFLIP")) CompleteChallenge("heelflip");
        if (comboTags.Contains("FLICK KICKFLIP")) CompleteChallenge("flick");
        if (comboTags.Contains("SPIN")) CompleteChallenge("spin");
        if (comboTags.Contains("MANUAL 3S")) CompleteChallenge("manual");
        if (comboTags.Contains("GRIND 3M")) CompleteChallenge("grind");
        if (comboTags.Contains("BOARDSLIDE")) CompleteChallenge("boardslide");
        if (comboTricks.Contains("360 FLIP")) CompleteChallenge("flip360");
        if (comboTags.Contains("ROCKET"))
            foreach (string f in FlipTricks)
                if (comboTricks.Contains(f)) CompleteChallenge("rocket");
        if (comboMultiplier >= 5) CompleteChallenge("combo5");
        if (earned >= 5000) CompleteChallenge("score5000");
    }

    // ================================================================ sound
    //
    // Board sounds (rolling, grinding, pop, land, flip) are small WAV files in scripts\SkateInGTA\,
    // played through Windows' MCI on a background thread so they can overlap and loop.
    // UI sounds (combo landed, challenge complete...) are GTA's own frontend sounds.

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    static extern int mciSendString(string command, System.Text.StringBuilder result, int resultLength, IntPtr callback);

    static readonly string[] SoundNames = { "roll", "grind", "pop", "land", "flip" };
    BlockingCollection<string> soundQueue;
    bool rollPlaying, grindPlaying;
    int rollVolume = -1, grindVolume = -1;

    void StartSound()
    {
        if (!soundEnabled) return;
        string dir = Path.GetFullPath("scripts\\SkateInGTA");
        foreach (string n in SoundNames)
            if (!File.Exists(Path.Combine(dir, n + ".wav"))) return; // shortcut: no sound files = silent board, the mod still works

        soundQueue = new BlockingCollection<string>();
        Thread t = new Thread(delegate ()
        {
            foreach (string cmd in soundQueue.GetConsumingEnumerable())
                try { mciSendString(cmd, null, 0, IntPtr.Zero); } catch (Exception) { }
        });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA); // MCI's media player wants a COM apartment
        t.Start();

        int vol = (int)(Math.Max(0f, Math.Min(soundVolume, 1f)) * 1000f);
        foreach (string n in SoundNames)
        {
            soundQueue.Add("open \"" + Path.Combine(dir, n + ".wav") + "\" type mpegvideo alias skate_" + n);
            soundQueue.Add("setaudio skate_" + n + " volume to " + vol);
        }
    }

    void StopSound()
    {
        if (soundQueue == null) return;
        foreach (string n in SoundNames) soundQueue.Add("close skate_" + n);
        soundQueue.CompleteAdding();
        soundQueue = null;
    }

    void PlaySound(string name)
    {
        if (soundQueue != null) soundQueue.Add("play skate_" + name + " from 0");
    }

    static void FrontendSound(string name, string set)
    {
        Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, name, set, true);
    }

    // Rolling gets louder with speed; grinding and powersliding scrape.
    void UpdateSoundLoops()
    {
        if (soundQueue == null) return;
        bool active = riding && !Game.IsPaused;
        float roll = active && !airborne && !grinding ? Math.Min(speed / 12f, 1f) : 0f;
        float scrape = active && (grinding || powersliding) ? 1f : 0f;
        SetLoop("roll", roll, ref rollPlaying, ref rollVolume);
        SetLoop("grind", scrape, ref grindPlaying, ref grindVolume);
    }

    void SetLoop(string name, float level, ref bool playing, ref int lastVolume)
    {
        int vol = (int)(level * Math.Max(0f, Math.Min(soundVolume, 1f)) * 1000f);
        if (vol <= 20)
        {
            if (playing) soundQueue.Add("stop skate_" + name);
            playing = false;
            return;
        }
        if (Math.Abs(vol - lastVolume) > 40)
        {
            soundQueue.Add("setaudio skate_" + name + " volume to " + vol);
            lastVolume = vol;
        }
        if (!playing) soundQueue.Add("play skate_" + name + " from 0 repeat");
        playing = true;
    }

    // ---------------------------------------------------------------- visuals

    void UpdateEntities(Ped ped)
    {
        Vector3 forward = ForwardFromHeading(heading);
        Vector3 right = new Vector3(forward.Y, -forward.X, 0f);

        // Tilt the board to match the ground (flat while flying).
        Vector3 n = airborne ? Vector3.WorldUp : groundNormal;
        float pitch = RadToDeg((float)Math.Atan2(-Vector3.Dot(n, forward), n.Z));
        float roll = RadToDeg((float)Math.Atan2(Vector3.Dot(n, right), n.Z));
        // Powerslide turns the board sideways; carving leans it into the turn.
        float dt = Game.LastFrameTime;
        slideYaw += ((powersliding ? slideDir * 80f : 0f) - slideYaw) * Math.Min(dt * 10f, 1f);
        float steer = airborne || grinding ? 0f : Game.GetDisabledControlValueNormalized(Control.MoveLeftRight);
        carve += (steer - carve) * Math.Min(dt * 6f, 1f);
        roll += carve * 8f * Math.Min(speed / 8f, 1f);
        float visualYaw = heading + airSpin + slideYaw + (fakie ? 180f : 0f);

        if (manual) pitch += 12f + manualBalance * 10f; // tail down
        if (grinding) roll += manualBalance * 8f;       // wobble with your balance
        if (grinding && grindName == "NOSEGRIND") pitch -= 14f;
        if (grinding && grindName == "5-0 GRIND") pitch += 14f;
        if (grabbing && grabName == "NOSEGRAB") pitch -= 12f;
        if (grabbing && grabName == "TAILGRAB") pitch += 12f;

        anchor.PositionNoOffset = pos;
        anchor.Rotation = new Vector3(pitch, roll, visualYaw);

        // Crouch while charging an ollie.
        float crouch = Math.Max(ollieCharge, flickState == FlickFromDown ? flickCharge : 0f) * 0.25f;
        if (grabbing) crouch = Math.Max(crouch, 0.4f);
        if (grinding || powersliding) crouch = Math.Max(crouch, 0.12f);
        ped.AttachTo(anchor, new Vector3(0f, 0f, pedHeight - crouch), new Vector3(0f, 0f, stanceYaw));

        if (board != null && board.Exists())
        {
            float flipRoll = 0f, flipYaw = 0f, lift = 0f;
            if (trickName != null)
            {
                float t = Smooth(trickTime);
                flipRoll = trickRoll * t;
                flipYaw = trickYaw * t;
                lift = (float)Math.Sin(trickTime * Math.PI) * 0.35f;
            }
            board.AttachTo(anchor, new Vector3(0f, 0f, boardHeight + lift), new Vector3(0f, flipRoll, boardYawOffset + flipYaw));
        }

        if (rideAnimDict.Length > 0 && !Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped, rideAnimDict, rideAnimClip, 3)) PlayRideAnimation(ped);
    }

    void UpdateCamera(float dt)
    {
        if (cam == null) return;

        // 0 = skate cam, 1 = filmer cam (low and close with a fisheye-wide view, like Skate's filmer),
        // 2 = far follow cam for big airs.
        float dist = camMode == 1 ? 2.2f : camMode == 2 ? camDistance * 2f : camDistance;
        float height = camMode == 1 ? 0.5f : camMode == 2 ? camHeight * 1.8f : camHeight;
        float baseFov = camMode == 1 ? 95f : 60f;

        Vector3 dir = ForwardFromHeading(heading);
        Vector3 desired = pos - dir * dist + Vector3.WorldUp * height;
        float follow = 1f - (float)Math.Exp(-6f * dt);
        camPos = camPos + (desired - camPos) * follow;

        cam.Position = camPos;
        cam.PointAt(pos + Vector3.WorldUp * (camMode == 1 ? 0.6f : 0.9f) + dir * 1.5f);

        // Wider view while boosting for a sense of speed.
        camFov += ((boosting ? baseFov + 15f : baseFov) - camFov) * Math.Min(dt * 4f, 1f);
        cam.FieldOfView = camFov;
    }

    // Asks the game to load the animation and plays it once it's ready. Called every frame until it works,
    // so we never freeze the game while waiting. Gives up after a few seconds if the name is wrong.
    void PlayRideAnimation(Ped ped)
    {
        if (string.IsNullOrEmpty(rideAnimDict)) return;

        if (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, rideAnimDict))
        {
            Function.Call(Hash.REQUEST_ANIM_DICT, rideAnimDict);
            if (DateTime.Now > animGiveUpTime) rideAnimDict = "";
            return;
        }
        ped.Task.PlayAnimation(rideAnimDict, rideAnimClip, 8f, -1, AnimationFlags.Loop);
    }

    // ---------------------------------------------------------------- helpers

    // We disable the on-foot controls every frame, so we have to read them with the "disabled" natives.
    static bool Pressed(Control c)
    {
        return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_PRESSED, 0, (int)c);
    }

    static bool JustPressed(Control c)
    {
        return Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)c);
    }

    // Returns a loaded model, or null if the name isn't a real game model.
    static Model LoadModel(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Model m = new Model(name);
        if (!m.IsInCdImage || !m.IsValid) return null;
        if (!m.Request(1000)) return null;
        return m;
    }

    bool HitWall(Vector3 from, Vector3 dir, float distance)
    {
        Vector3 start = from + Vector3.WorldUp * 0.5f;
        RaycastResult hit = World.Raycast(start, start + dir * distance, GroundFlags, anchor);
        // Only count steep surfaces. Gentle ones are ramps we can roll up.
        return hit.DidHit && Math.Abs(hit.SurfaceNormal.Z) < 0.5f;
    }

    // GTA headings: 0 = north (+Y), 90 = west (-X).
    static Vector3 ForwardFromHeading(float headingDeg)
    {
        float r = headingDeg * (float)Math.PI / 180f;
        return new Vector3(-(float)Math.Sin(r), (float)Math.Cos(r), 0f);
    }

    static float RadToDeg(float rad)
    {
        return rad * 180f / (float)Math.PI;
    }

    // Ease-in-out so flips start and end smoothly.
    static float Smooth(float t)
    {
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        return t * t * (3f - 2f * t);
    }
}
