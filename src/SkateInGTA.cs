// Skate in GTA - Skate-style skateboarding for GTA V Story Mode.
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
using System.Globalization;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using Control = GTA.Control;
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

    // tricks
    string trickName;       // null when no trick is running
    float trickTime;        // 0..1 progress of the current trick
    float trickRoll;        // total board roll for the trick (kickflip = 360)
    float trickYaw;         // total board yaw for the trick (shove-it = 180)
    int trickPoints;

    // score
    int comboPoints;
    int comboMultiplier;
    int totalScore;

    const float ChargeTime = 0.6f;          // seconds to fully charge an ollie
    const IntersectFlags GroundFlags = IntersectFlags.Map | IntersectFlags.Vehicles | IntersectFlags.Objects;

    public SkateInGTA()
    {
        LoadSettings();
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;
        Interval = 0;
    }

    void LoadSettings()
    {
        ScriptSettings s = ScriptSettings.Load("scripts\\SkateInGTA.ini");

        toggleKey = s.GetValue<Keys>("Controls", "ToggleKey", Keys.F5);

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
        if (e.KeyCode != toggleKey) return;

        if (riding) StopRiding();
        else StartRiding();
    }

    void StartRiding()
    {
        Ped ped = rider;
        if (ped == null || !ped.Exists() || ped != Game.Player.Character || ped.IsDead || ped.IsInVehicle() || ped.IsRagdoll || Game.IsCutsceneActive)
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
        rider = ped;
        animGiveUpTime = DateTime.Now.AddSeconds(3);
        PlayRideAnimation(ped);

        speed = 0f;
        groundVz = 0f;
        fakie = false;
        airborne = false;
        airSpin = 0f;
        ollieCharge = 0f;
        trickName = null;
        comboPoints = 0;
        comboMultiplier = 0;
        riding = true;

        UpdateEntities(ped);

        if (useSkateCam)
        {
            camPos = pos - ForwardFromHeading(heading) * camDistance + Vector3.WorldUp * camHeight;
            cam = World.CreateCamera(camPos, Vector3.Zero, 60f);
            World.RenderingCamera = cam;
        }

        Notification.Show("~g~Skate in GTA~s~~n~W push, S brake, A/D steer~n~Hold SPACE + release = ollie~n~In air: Q kickflip, E heelflip, R shove-it, A/D spin");
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
            if (!ped.IsDead) ped.Position = pos;
        }

        DeleteProps();
        DestroyCamera();
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

        comboPoints = 0;
        comboMultiplier = 0;
        Screen.ShowSubtitle("~r~BAIL!~s~ " + reason + " (press " + toggleKey + " to ride again)", 3000);
    }

    void OnAborted(object sender, EventArgs e)
    {
        if (riding) StopRiding();
        else
        {
            DeleteProps();
            DestroyCamera();
        }
    }

    void DeleteProps()
    {
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
        if (!riding) return;

        Ped ped = Game.Player.Character;
        if (ped.IsDead || ped.IsInVehicle() || Game.IsCutsceneActive || anchor == null || !anchor.Exists())
        {
            StopRiding();
            return;
        }
        if (Game.IsPaused) return;

        float dt = Game.LastFrameTime;
        if (dt <= 0f) return;
        if (dt > 0.05f) dt = 0.05f; // avoid huge jumps after a lag spike

        DisableOnFootControls();

        if (ped.IsInWater)
        {
            Bail("Skateboards don't float.");
            return;
        }

        if (airborne) UpdateAir(dt);
        else UpdateGround(dt);

        if (!riding) return; // we may have bailed above

        UpdateEntities(ped);
        UpdateCamera(dt);
    }

    // Stop GTA from using our buttons for running, jumping, punching, etc.
    void DisableOnFootControls()
    {
        Control[] controls = new Control[]
        {
            Control.MoveLeftRight, Control.MoveUpDown, Control.MoveUpOnly, Control.MoveDownOnly,
            Control.MoveLeftOnly, Control.MoveRightOnly, Control.Jump, Control.Sprint, Control.Duck,
            Control.Cover, Control.Context, Control.Reload, Control.Enter, Control.Attack, Control.Aim,
            Control.MeleeAttackLight, Control.MeleeAttackHeavy, Control.MeleeAttackAlternate
        };
        foreach (Control c in controls) Game.DisableControlThisFrame(c);
    }

    // ---------------------------------------------------------------- ground physics

    void UpdateGround(float dt)
    {
        float steer = Game.GetDisabledControlValueNormalized(Control.MoveLeftRight);  // -1 left .. 1 right
        float throttle = -Game.GetDisabledControlValueNormalized(Control.MoveUpDown); // 1 = W / stick up

        // Push and brake.
        if (throttle > 0.1f && speed < pushMaxSpeed) speed += pushAccel * throttle * dt;
        if (throttle < -0.1f) speed -= brakeDecel * -throttle * dt;

        // Rolling friction.
        speed -= speed * friction * dt;

        // Hills: gravity pulls you down the slope.
        Vector3 forward = ForwardFromHeading(heading);
        float uphill = -Vector3.Dot(groundNormal, forward); // > 0 when the ground rises in front of us
        speed -= gravity * uphill * slopeFactor * dt;

        if (speed < 0f) speed = 0f;
        if (speed > maxSpeed) speed = maxSpeed;

        // Steering. Turning is a bit sharper at low speed, like a real board.
        float turnScale = 1.0f - 0.4f * Math.Min(speed / maxSpeed, 1f);
        heading -= steer * turnRate * turnScale * dt;

        // Ollie: hold to crouch, release to pop.
        if (Pressed(Control.Jump))
        {
            ollieCharge = Math.Min(ollieCharge + dt / ChargeTime, 1f);
        }
        else if (ollieCharge > 0f)
        {
            float pop = ollieMin + (ollieMax - ollieMin) * ollieCharge;
            ollieCharge = 0f;
            Launch(forward * speed + Vector3.WorldUp * (pop + Math.Max(groundVz, 0f)));
            AddTrickText("OLLIE", 0);
            return;
        }

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

    void Launch(Vector3 velocity)
    {
        airborne = true;
        airVelocity = velocity;
        airSpin = 0f;
        pos += Vector3.WorldUp * 0.05f;
    }

    // ---------------------------------------------------------------- air physics and tricks

    void UpdateAir(float dt)
    {
        float steer = Game.GetDisabledControlValueNormalized(Control.MoveLeftRight);
        airSpin -= steer * airSpinRate * dt;

        // Start a flip trick if none is running.
        if (trickName == null)
        {
            if (JustPressed(Control.Cover)) StartTrick("KICKFLIP", 360f, 0f, 100);
            else if (JustPressed(Control.Context)) StartTrick("HEELFLIP", -360f, 0f, 100);
            else if (JustPressed(Control.Reload)) StartTrick("POP SHOVE-IT", 0f, 180f, 80);
        }
        else
        {
            trickTime += dt / trickDuration;
            if (trickTime >= 1f)
            {
                AddTrickText(trickName, trickPoints);
                trickName = null;
                trickTime = 0f;
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
            if (ground.DidHit)
            {
                Land(ground, flatSpeed);
                return;
            }
        }

        pos = next;
    }

    void StartTrick(string name, float roll, float yaw, int points)
    {
        trickName = name;
        trickTime = 0f;
        trickRoll = roll;
        trickYaw = yaw;
        trickPoints = points;
    }

    void Land(RaycastResult ground, float flatSpeed)
    {
        pos = ground.HitPosition;
        groundNormal = ground.SurfaceNormal;

        if (trickName != null)
        {
            Bail("Didn't finish the " + trickName.ToLower() + ".");
            return;
        }
        if (-airVelocity.Z > maxLandingSpeed)
        {
            Bail("That drop was too big.");
            return;
        }
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
        int halfTurns = (int)Math.Round(Math.Abs(airSpin) / 180f);
        if (halfTurns > 0) AddTrickText((halfTurns * 180) + (offBackward < offStraight ? " (FAKIE)" : ""), halfTurns * 75);
        if (offBackward < offStraight) fakie = !fakie;

        airborne = false;
        airSpin = 0f;
        speed = Math.Min(flatSpeed * 0.95f, maxSpeed);
        groundVz = 0f;

        if (comboPoints > 0)
        {
            int earned = comboPoints * Math.Max(comboMultiplier, 1);
            totalScore += earned;
            Screen.ShowSubtitle("~g~LANDED!~s~ +" + earned + "   Score: " + totalScore, 2500);
        }
        comboPoints = 0;
        comboMultiplier = 0;
    }

    void AddTrickText(string name, int points)
    {
        if (points > 0)
        {
            comboPoints += points;
            comboMultiplier++;
        }
        string text = "~y~" + name + "~s~";
        if (comboPoints > 0) text += "   " + comboPoints + " x" + Math.Max(comboMultiplier, 1);
        Screen.ShowSubtitle(text, 1500);
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
        float visualYaw = heading + airSpin + (fakie ? 180f : 0f);

        anchor.PositionNoOffset = pos;
        anchor.Rotation = new Vector3(pitch, roll, visualYaw);

        // Crouch while charging an ollie.
        float crouch = ollieCharge * 0.25f;
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

        Vector3 dir = ForwardFromHeading(heading);
        Vector3 desired = pos - dir * camDistance + Vector3.WorldUp * camHeight;
        float follow = 1f - (float)Math.Exp(-6f * dt);
        camPos = camPos + (desired - camPos) * follow;

        cam.Position = camPos;
        cam.PointAt(pos + Vector3.WorldUp * 0.9f + dir * 1.5f);
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
