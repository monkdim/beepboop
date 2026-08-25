using TwoButton.Core.Model;

namespace TwoButton.Core.Jobs.Dragoon;

/// <summary>
/// Dragoon action and status table, current as of Dawntrail (7.x).
/// <para>
/// The numeric ids are seeds only. <c>ActionTableVerifier</c> checks every one against the
/// game's own Action and Status sheets at startup and rebinds by name on a mismatch, so a
/// patch that shuffles ids - or a typo here - is corrected rather than mis-cast.
/// </para>
/// </summary>
public static class DragoonActions
{
    // ---- Single target combo --------------------------------------------
    public static readonly ActionRef TrueThrust = new(75, "True Thrust", ActionKind.Gcd, 1);
    public static readonly ActionRef RaidenThrust = new(16479, "Raiden Thrust", ActionKind.Gcd, 76);
    public static readonly ActionRef LanceBarrage = new(36954, "Lance Barrage", ActionKind.Gcd, 4);
    public static readonly ActionRef SpiralBlow = new(36955, "Spiral Blow", ActionKind.Gcd, 18);
    public static readonly ActionRef FullThrust = new(84, "Full Thrust", ActionKind.Gcd, 26);
    public static readonly ActionRef HeavensThrust = new(25771, "Heavens' Thrust", ActionKind.Gcd, 86);
    public static readonly ActionRef ChaosThrust = new(88, "Chaos Thrust", ActionKind.Gcd, 50);
    public static readonly ActionRef ChaoticSpring = new(25772, "Chaotic Spring", ActionKind.Gcd, 86);
    public static readonly ActionRef FangAndClaw = new(3554, "Fang and Claw", ActionKind.Gcd, 56);
    public static readonly ActionRef WheelingThrust = new(3556, "Wheeling Thrust", ActionKind.Gcd, 58);
    public static readonly ActionRef Drakesbane = new(36952, "Drakesbane", ActionKind.Gcd, 100);

    // ---- AoE combo -------------------------------------------------------
    public static readonly ActionRef DoomSpike = new(86, "Doom Spike", ActionKind.Gcd, 40);
    public static readonly ActionRef DraconianFury = new(25770, "Draconian Fury", ActionKind.Gcd, 82);
    public static readonly ActionRef SonicThrust = new(7397, "Sonic Thrust", ActionKind.Gcd, 62);
    public static readonly ActionRef CoerthanTorment = new(16477, "Coerthan Torment", ActionKind.Gcd, 72);

    // ---- Ranged filler ---------------------------------------------------
    public static readonly ActionRef PiercingTalon = new(90, "Piercing Talon", ActionKind.Gcd, 15);

    // ---- Off-globals -----------------------------------------------------
    public static readonly ActionRef LifeSurge = new(83, "Life Surge", ActionKind.OGcd, 6);
    public static readonly ActionRef LanceCharge = new(85, "Lance Charge", ActionKind.OGcd, 30);
    public static readonly ActionRef BattleLitany = new(3557, "Battle Litany", ActionKind.OGcd, 52);
    public static readonly ActionRef Jump = new(92, "Jump", ActionKind.OGcd, 30);
    public static readonly ActionRef HighJump = new(16478, "High Jump", ActionKind.OGcd, 74);
    public static readonly ActionRef MirageDive = new(7399, "Mirage Dive", ActionKind.OGcd, 68);
    public static readonly ActionRef DragonfireDive = new(96, "Dragonfire Dive", ActionKind.OGcd, 50);
    public static readonly ActionRef RiseOfTheDragon = new(36953, "Rise of the Dragon", ActionKind.OGcd, 92);
    public static readonly ActionRef Geirskogul = new(3555, "Geirskogul", ActionKind.OGcd, 60);
    public static readonly ActionRef Nastrond = new(7400, "Nastrond", ActionKind.OGcd, 70);
    public static readonly ActionRef Stardiver = new(16480, "Stardiver", ActionKind.OGcd, 80);
    public static readonly ActionRef Starcross = new(36956, "Starcross", ActionKind.OGcd, 100);
    public static readonly ActionRef WyrmwindThrust = new(25773, "Wyrmwind Thrust", ActionKind.OGcd, 90);
    public static readonly ActionRef TrueNorth = new(7546, "True North", ActionKind.OGcd, 50);

    // ---- Statuses --------------------------------------------------------
    public static readonly StatusRef DraconianFire = new(1863, "Draconian Fire");
    public static readonly StatusRef PowerSurge = new(2720, "Power Surge");
    public static readonly StatusRef LanceChargeBuff = new(1864, "Lance Charge");
    public static readonly StatusRef BattleLitanyBuff = new(786, "Battle Litany");
    public static readonly StatusRef LifeSurgeBuff = new(116, "Life Surge");
    public static readonly StatusRef DiveReady = new(1243, "Dive Ready");
    public static readonly StatusRef NastrondReady = new(3844, "Nastrond Ready");
    public static readonly StatusRef StarcrossReady = new(4302, "Starcross Ready");
    public static readonly StatusRef DragonsFlight = new(4303, "Dragon's Flight");
    public static readonly StatusRef ChaoticSpringDot = new(2719, "Chaotic Spring");
    public static readonly StatusRef ChaosThrustDot = new(118, "Chaos Thrust");
    public static readonly StatusRef TrueNorthBuff = new(1250, "True North");

    public static readonly IReadOnlyList<ActionRef> All =
    [
        TrueThrust, RaidenThrust, LanceBarrage, SpiralBlow, FullThrust, HeavensThrust,
        ChaosThrust, ChaoticSpring, FangAndClaw, WheelingThrust, Drakesbane,
        DoomSpike, DraconianFury, SonicThrust, CoerthanTorment, PiercingTalon,
        LifeSurge, LanceCharge, BattleLitany, Jump, HighJump, MirageDive,
        DragonfireDive, RiseOfTheDragon, Geirskogul, Nastrond, Stardiver, Starcross,
        WyrmwindThrust, TrueNorth,
    ];

    public static readonly IReadOnlyList<StatusRef> AllStatuses =
    [
        DraconianFire, PowerSurge, LanceChargeBuff, BattleLitanyBuff, LifeSurgeBuff,
        DiveReady, NastrondReady, StarcrossReady, DragonsFlight,
        ChaoticSpringDot, ChaosThrustDot, TrueNorthBuff,
    ];
}
