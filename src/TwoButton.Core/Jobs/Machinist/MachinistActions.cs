using TwoButton.Core.Model;

namespace TwoButton.Core.Jobs.Machinist;

/// <summary>
/// Machinist action and status table, current as of Dawntrail (7.x). Ids are seeds; see
/// the note on <see cref="Dragoon.DragoonActions"/>.
/// </summary>
public static class MachinistActions
{
    // ---- Single target combo --------------------------------------------
    public static readonly ActionRef SplitShot = new(2866, "Split Shot", ActionKind.Gcd, 1);
    public static readonly ActionRef HeatedSplitShot = new(7411, "Heated Split Shot", ActionKind.Gcd, 54);
    public static readonly ActionRef SlugShot = new(2868, "Slug Shot", ActionKind.Gcd, 2);
    public static readonly ActionRef HeatedSlugShot = new(7412, "Heated Slug Shot", ActionKind.Gcd, 60);
    public static readonly ActionRef CleanShot = new(2873, "Clean Shot", ActionKind.Gcd, 26);
    public static readonly ActionRef HeatedCleanShot = new(7413, "Heated Clean Shot", ActionKind.Gcd, 64);

    // ---- Tools -----------------------------------------------------------
    public static readonly ActionRef Drill = new(16498, "Drill", ActionKind.Gcd, 58);
    public static readonly ActionRef AirAnchor = new(16500, "Air Anchor", ActionKind.Gcd, 76);
    public static readonly ActionRef ChainSaw = new(25788, "Chain Saw", ActionKind.Gcd, 90);
    public static readonly ActionRef Excavator = new(36981, "Excavator", ActionKind.Gcd, 96);
    public static readonly ActionRef FullMetalField = new(36982, "Full Metal Field", ActionKind.Gcd, 100);

    // ---- Overheat --------------------------------------------------------
    public static readonly ActionRef HeatBlast = new(7410, "Heat Blast", ActionKind.Gcd, 35);
    public static readonly ActionRef BlazingShot = new(36978, "Blazing Shot", ActionKind.Gcd, 86);
    public static readonly ActionRef AutoCrossbow = new(16497, "Auto Crossbow", ActionKind.Gcd, 52);

    // ---- AoE -------------------------------------------------------------
    public static readonly ActionRef SpreadShot = new(2870, "Spread Shot", ActionKind.Gcd, 18);
    public static readonly ActionRef Scattergun = new(25786, "Scattergun", ActionKind.Gcd, 82);
    public static readonly ActionRef Bioblaster = new(16499, "Bioblaster", ActionKind.Gcd, 72);
    public static readonly ActionRef Flamethrower = new(7418, "Flamethrower", ActionKind.Gcd, 70);

    // ---- Off-globals -----------------------------------------------------
    public static readonly ActionRef GaussRound = new(2874, "Gauss Round", ActionKind.OGcd, 15);
    public static readonly ActionRef DoubleCheck = new(36979, "Double Check", ActionKind.OGcd, 92);
    public static readonly ActionRef Ricochet = new(2890, "Ricochet", ActionKind.OGcd, 50);
    public static readonly ActionRef Checkmate = new(36980, "Checkmate", ActionKind.OGcd, 92);
    public static readonly ActionRef Reassemble = new(2876, "Reassemble", ActionKind.OGcd, 10);
    public static readonly ActionRef Hypercharge = new(17209, "Hypercharge", ActionKind.OGcd, 30);
    public static readonly ActionRef Wildfire = new(2878, "Wildfire", ActionKind.OGcd, 45);
    public static readonly ActionRef BarrelStabilizer = new(7414, "Barrel Stabilizer", ActionKind.OGcd, 66);
    public static readonly ActionRef AutomatonQueen = new(16501, "Automaton Queen", ActionKind.OGcd, 80);
    public static readonly ActionRef QueenOverdrive = new(16502, "Queen Overdrive", ActionKind.OGcd, 80);

    // ---- Statuses --------------------------------------------------------
    public static readonly StatusRef Reassembled = new(851, "Reassembled");
    public static readonly StatusRef Overheated = new(2688, "Overheated");
    public static readonly StatusRef WildfireBuff = new(1946, "Wildfire");
    public static readonly StatusRef Hypercharged = new(3864, "Hypercharged");
    public static readonly StatusRef ExcavatorReady = new(3865, "Excavator Ready");
    public static readonly StatusRef FullMetalMachinist = new(4404, "Full Metal Machinist");
    public static readonly StatusRef BioblasterDot = new(1866, "Bioblaster");

    public static readonly IReadOnlyList<ActionRef> All =
    [
        SplitShot, HeatedSplitShot, SlugShot, HeatedSlugShot, CleanShot, HeatedCleanShot,
        Drill, AirAnchor, ChainSaw, Excavator, FullMetalField,
        HeatBlast, BlazingShot, AutoCrossbow,
        SpreadShot, Scattergun, Bioblaster, Flamethrower,
        GaussRound, DoubleCheck, Ricochet, Checkmate, Reassemble, Hypercharge,
        Wildfire, BarrelStabilizer, AutomatonQueen, QueenOverdrive,
    ];

    public static readonly IReadOnlyList<StatusRef> AllStatuses =
    [
        Reassembled, Overheated, WildfireBuff, Hypercharged, ExcavatorReady,
        FullMetalMachinist, BioblasterDot,
    ];
}
