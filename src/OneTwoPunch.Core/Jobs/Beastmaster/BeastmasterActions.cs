using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs.Beastmaster;

/// <summary>
/// Beastmaster action and status ids.
/// <para>
/// Unlike every other job here, these are <em>not</em> from <c>tools/generate_action_tables.py</c>:
/// BossMod has no Beastmaster definitions yet. They were read directly from the game's own
/// 7.56 Action, ActionTransient and Status sheets, and cross-checked against the official
/// job guide. Ids are still verified by name at startup and repaired on a mismatch.
/// </para>
/// <para>
/// <b>Two deliberate omissions.</b> The level 50 upgrades (Brutal Rage, Hawkish Talons,
/// Risen Fall, Calamity) and the eight Kinship actions that Beast Mode morphs into are not
/// listed. Both groups sit behind the game's own ActionIndirection table rather than being
/// pressed directly - <c>GetAdjustedActionId</c> hands them back in place of their base
/// action - and no rule here suggests one yet. They go in when the instinctual track does.
/// </para>
/// </summary>
public static class BeastmasterActions
{
    // ---- Weaponskill combo: the ordinary 2.5s line, cooldown group 58 ----

    public static readonly ActionRef SmashAxe = new(44879, "Smash Axe", ActionKind.Gcd, 1);
    public static readonly ActionRef AxebladeBite = new(44883, "Axeblade Bite", ActionKind.Gcd, 2);
    public static readonly ActionRef Shieldsplitter = new(44885, "Shieldsplitter", ActionKind.Gcd, 12);

    /// <summary>
    /// Capture is filed as an Ability by the sheet but sits on cooldown group 58, so it
    /// rolls the global like a weaponskill. Kind follows the cooldown group, not the label.
    /// </summary>
    public static readonly ActionRef Capture = new(44880, "Capture", ActionKind.Gcd, 1);

    /// <summary>Beast Mode is likewise group 58 - it rolls the global.</summary>
    public static readonly ActionRef BeastMode = new(44886, "Beast Mode", ActionKind.Gcd, 22);

    // ---- Instinctual skills: the third clock ----------------------------

    /// <remarks>
    /// These four are the reason this job needs an engine decision before it gets rules.
    /// They are weaponskills that sit on their own 5.0s cooldown group 16, carrying neither
    /// group 58 (the global) nor group 71 (the shared ability lock) - the only four non-PvP
    /// weaponskills in the game that do. Their tooltip says it outright: "Instinctual skills
    /// do not share a recast timer with any other actions."
    /// <para>
    /// <see cref="ActionKind.OGcd"/> is the closer of the two available answers because they
    /// do not roll the global, but it is not accurate: the engine would weave them into a
    /// gap, and they need no gap. Nothing suggests them yet, so the mislabel is inert. Fix
    /// the model before writing the rules, not after.
    /// </para>
    /// </remarks>
    public static readonly ActionRef AvalancheAxe = new(44884, "Avalanche Axe", ActionKind.OGcd, 4);
    public static readonly ActionRef MistralAxe = new(44887, "Mistral Axe", ActionKind.OGcd, 8);
    public static readonly ActionRef SpinningAxe = new(44888, "Spinning Axe", ActionKind.OGcd, 14);
    public static readonly ActionRef GaleAxe = new(44889, "Gale Axe", ActionKind.OGcd, 16);

    // ---- Familiar --------------------------------------------------------

    public static readonly ActionRef FirstBattlehorn = new(44881, "First Battlehorn", ActionKind.OGcd, 1);
    public static readonly ActionRef SecondBattlehorn = new(44892, "Second Battlehorn", ActionKind.OGcd, 10);
    public static readonly ActionRef ThirdBattlehorn = new(44894, "Third Battlehorn", ActionKind.OGcd, 20);
    public static readonly ActionRef PartingBlow = new(44891, "Parting Blow", ActionKind.OGcd, 6);
    public static readonly ActionRef Trick = new(47093, "Trick", ActionKind.OGcd, 8);
    public static readonly ActionRef TemperedRelease = new(44890, "Tempered Release", ActionKind.OGcd, 18);
    public static readonly ActionRef Borrow = new(44895, "Borrow", ActionKind.OGcd, 22);

    // ---- Cooldowns and utility ------------------------------------------

    public static readonly ActionRef ShieldCharge = new(44893, "Shield Charge", ActionKind.OGcd, 24);
    public static readonly ActionRef Rally = new(44905, "Rally", ActionKind.OGcd, 28);
    public static readonly ActionRef RallyingCheer = new(44904, "Rallying Cheer", ActionKind.OGcd, 40);
    public static readonly ActionRef Gauge = new(44882, "Gauge", ActionKind.OGcd, 1);

    // ---- Statuses --------------------------------------------------------

    /// <summary>
    /// The four Hearts. Each names the affinity that combos off it, so the Heart the player
    /// is holding <em>is</em> their position in the ring: Volant, then Rampant, then Durant,
    /// then Eldritch, then Volant again.
    /// </summary>
    public static readonly StatusRef VolantHeart = new(4595, "Volant Heart");
    public static readonly StatusRef RampantHeart = new(4596, "Rampant Heart");
    public static readonly StatusRef DurantHeart = new(4597, "Durant Heart");
    public static readonly StatusRef EldritchHeart = new(4598, "Eldritch Heart");

    /// <summary>The two halves of the Inner Compass, lit by completed intentional combos.</summary>
    public static readonly StatusRef Sunstrider = new(4599, "Sunstrider");
    public static readonly StatusRef Moonstalker = new(4600, "Moonstalker");

    public static readonly StatusRef OneWithNature = new(4601, "One with Nature");
    public static readonly StatusRef LingeringVantage = new(4614, "Lingering Vantage");

    /// <summary>Familiar combos are locked out entirely while this is up.</summary>
    public static readonly StatusRef WaveringHeart = new(4643, "Wavering Heart");

    public static readonly StatusRef BeastKinship = new(4602, "Beast Kinship");
    public static readonly StatusRef VileKinship = new(4603, "Vile Kinship");
    public static readonly StatusRef CloudKinship = new(4604, "Cloud Kinship");
    public static readonly StatusRef SeedKinship = new(4605, "Seed Kinship");
    public static readonly StatusRef WaveKinship = new(4606, "Wave Kinship");
    public static readonly StatusRef ScaleKinship = new(4607, "Scale Kinship");
    public static readonly StatusRef SoulKinship = new(4608, "Soul Kinship");
    public static readonly StatusRef AshKinship = new(4609, "Ash Kinship");

    public static readonly IReadOnlyList<ActionRef> All =
    [
        SmashAxe, AxebladeBite, Shieldsplitter, Capture,
        BeastMode, AvalancheAxe, MistralAxe, SpinningAxe,
        GaleAxe, FirstBattlehorn, SecondBattlehorn, ThirdBattlehorn,
        PartingBlow, Trick, TemperedRelease, Borrow,
        ShieldCharge, Rally, RallyingCheer, Gauge,
    ];

    public static readonly IReadOnlyList<StatusRef> AllStatuses =
    [
        VolantHeart, RampantHeart, DurantHeart, EldritchHeart,
        Sunstrider, Moonstalker, OneWithNature, LingeringVantage,
        WaveringHeart, BeastKinship, VileKinship, CloudKinship,
        SeedKinship, WaveKinship, ScaleKinship, SoulKinship,
        AshKinship,
    ];
}
