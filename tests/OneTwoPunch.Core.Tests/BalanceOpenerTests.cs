using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Bard;
using OneTwoPunch.Core.Jobs.Dragoon;
using OneTwoPunch.Core.Jobs.Machinist;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Jobs.Pictomancer;
using OneTwoPunch.Core.Jobs.RedMage;
using OneTwoPunch.Core.Jobs.Reaper;
using OneTwoPunch.Core.Jobs.Samurai;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The openers are transcriptions of The Balance's opener charts, not something the engine
/// derives, so the only thing worth testing is that each one still says what its chart
/// says.
/// <para>
/// Each job is pinned twice over: the numbered globals in the chart's order, and the
/// off-globals in the order they are woven. Both are read off the chart independently of
/// the rotation file, so a transcription slip has to be made twice in the same way to
/// survive. Black Mage has its own file with some extra structural checks.
/// </para>
/// </summary>
public sealed class BalanceOpenerTests
{
    private static Opener OpenerFor(uint jobId)
    {
        var job = JobRegistry.Create(jobId)
            ?? throw new InvalidOperationException($"no rotation for job {jobId}");

        return job.Opener
            ?? throw new InvalidOperationException($"{job.Name} has no opener");
    }

    private static void AssertMatches(uint jobId, ActionKind kind, ActionRef[] chart)
    {
        var steps = OpenerFor(jobId).Steps.Where(s => s.Kind == kind).ToArray();

        Assert.True(
            chart.Length == steps.Length,
            $"chart has {chart.Length} {kind} steps, opener has {steps.Length}");

        for (var i = 0; i < chart.Length; i++)
        {
            Assert.True(
                chart[i].Id == steps[i].Id,
                $"{kind} {i + 1}: chart says {chart[i].Name}, opener says {steps[i].Name}");
        }
    }

    // ---- Dragoon: "Standard Opener - No PT", 7.1 --------------------------

    [Fact]
    public void DragoonGlobalsMatchTheChart() => AssertMatches(22, ActionKind.Gcd,
    [
        DragoonActions.TrueThrust,
        DragoonActions.SpiralBlow,
        DragoonActions.ChaoticSpring,
        DragoonActions.WheelingThrust,
        DragoonActions.Drakesbane,
        DragoonActions.RaidenThrust,
        DragoonActions.LanceBarrage,
        DragoonActions.HeavensThrust,
        DragoonActions.FangAndClaw,
        DragoonActions.Drakesbane,
        DragoonActions.RaidenThrust,
    ]);

    [Fact]
    public void DragoonWeavesMatchTheChart() => AssertMatches(22, ActionKind.OGcd,
    [
        DragoonActions.LanceCharge,
        DragoonActions.BattleLitany,
        DragoonActions.Geirskogul,
        DragoonActions.HighJump,
        DragoonActions.LifeSurge,
        DragoonActions.DragonfireDive,
        DragoonActions.Nastrond,
        DragoonActions.Stardiver,
        DragoonActions.Starcross,
        DragoonActions.LifeSurge,
        DragoonActions.RiseOfTheDragon,
        DragoonActions.MirageDive,
        DragoonActions.WyrmwindThrust,
    ]);

    // ---- Monk: "Solar Lunar - DK Opener (5s Buffs)", 7.0 ------------------

    [Fact]
    public void MonkGlobalsMatchTheChart() => AssertMatches(20, ActionKind.Gcd,
    [
        MonkActions.DragonKick,
        MonkActions.TwinSnakes,
        MonkActions.Demolish,
        MonkActions.LeapingOpo,
        MonkActions.RisingPhoenix,
        MonkActions.DragonKick,
        MonkActions.WindsReply,
        MonkActions.FiresReply,
        MonkActions.LeapingOpo,
        MonkActions.DragonKick,
        MonkActions.LeapingOpo,
        MonkActions.DragonKick,
        MonkActions.ElixirBurst,
        MonkActions.LeapingOpo,
    ]);

    [Fact]
    public void MonkWeavesMatchTheChart() => AssertMatches(20, ActionKind.OGcd,
    [
        MonkActions.PerfectBalance,
        MonkActions.Brotherhood,
        MonkActions.RiddleOfFire,
        MonkActions.ForbiddenChakra,
        MonkActions.RiddleOfWind,
        MonkActions.PerfectBalance,
    ]);

    /// <summary>
    /// Each Perfect Balance is worth three globals and nothing else, and the blitz that
    /// follows is the whole point of the opener. Getting the count wrong turns Rising
    /// Phoenix into a dud press, so it is worth stating separately from the sequence.
    /// </summary>
    [Fact]
    public void EachMonkPerfectBalanceIsFollowedByThreeGlobalsThenABlitz()
    {
        var steps = OpenerFor(20).Steps;

        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Id != MonkActions.PerfectBalance.Id)
                continue;

            var globals = steps.Skip(i + 1).Where(s => s.Kind == ActionKind.Gcd).Take(4).ToArray();

            Assert.Equal(4, globals.Length);
            Assert.True(
                globals[3].Id == MonkActions.RisingPhoenix.Id
                || globals[3].Id == MonkActions.ElixirBurst.Id,
                $"fourth global after Perfect Balance is {globals[3].Name}, expected a blitz");
        }
    }

    // ---- Reaper: "Opener - 2nd GCD AC", 7.0 ------------------------------

    [Fact]
    public void ReaperGlobalsMatchTheChart() => AssertMatches(39, ActionKind.Gcd,
    [
        ReaperActions.Soulsow, // pre-pull, loads Harvest Moon
        ReaperActions.Harpe, // pre-pull
        ReaperActions.ShadowofDeath,
        ReaperActions.SoulSlice,
        ReaperActions.ExecutionersGibbet,
        ReaperActions.ExecutionersGallows,
        ReaperActions.SoulSlice,
        ReaperActions.PlentifulHarvest,
        ReaperActions.VoidReaping,
        ReaperActions.CrossReaping,
        ReaperActions.VoidReaping,
        ReaperActions.CrossReaping,
        ReaperActions.Communio,
        ReaperActions.Perfectio,
        ReaperActions.Gibbet,
        ReaperActions.ShadowofDeath,
        ReaperActions.Slice,
    ]);

    [Fact]
    public void ReaperWeavesMatchTheChart() => AssertMatches(39, ActionKind.OGcd,
    [
        ReaperActions.ArcaneCircle,
        ReaperActions.Gluttony,
        ReaperActions.Enshroud,
        ReaperActions.Sacrificium,
        ReaperActions.LemuresSlice,
        ReaperActions.LemuresSlice,
        ReaperActions.UnveiledGibbet,
    ]);

    /// <summary>
    /// The chart is named for this: Arcane Circle goes into the second global's weave
    /// window, not the first, so the buff covers the Executioner pair.
    /// <para>
    /// Named rather than counted. This was a count, and adding pre-pull Soulsow moved it
    /// from three to four - a failure that says nothing about whether the buff still lands
    /// where the chart wants it. Listing the globals says which two are pre-pull and which
    /// two are the ones Arcane Circle is being weaved after.
    /// </para>
    /// </summary>
    [Fact]
    public void ReaperArcaneCircleIsOnTheSecondGlobal()
    {
        var steps = OpenerFor(39).Steps;
        var circle = steps.ToList().FindIndex(s => s.Id == ReaperActions.ArcaneCircle.Id);

        Assert.True(circle > 0, "Arcane Circle is not in the opener");

        var globalsBefore = steps.Take(circle)
            .Where(s => s.Kind == ActionKind.Gcd)
            .Select(s => s.Id)
            .ToArray();

        Assert.Equal(
            new[]
            {
                ReaperActions.Soulsow.Id,       // pre-pull
                ReaperActions.Harpe.Id,         // pre-pull
                ReaperActions.ShadowofDeath.Id, // first combat global
                ReaperActions.SoulSlice.Id,     // second, and the one it weaves off
            },
            globalsBefore);
    }

    // ---- Samurai: "Standard Opener", 7.05 --------------------------------

    [Fact]
    public void SamuraiGlobalsMatchTheChart() => AssertMatches(34, ActionKind.Gcd,
    [
        SamuraiActions.Gekko,
        SamuraiActions.Kasha,
        SamuraiActions.Yukikaze,
        SamuraiActions.TendoSetsugekka,
        SamuraiActions.TendoKaeshiSetsugekka,
        SamuraiActions.Gekko,
        SamuraiActions.Higanbana,
        SamuraiActions.OgiNamikiri,
        SamuraiActions.KaeshiNamikiri,
        SamuraiActions.Kasha,
        SamuraiActions.Gekko,
        SamuraiActions.Gyofu,
        SamuraiActions.Yukikaze,
        SamuraiActions.TendoSetsugekka,
        SamuraiActions.TendoKaeshiSetsugekka,
    ]);

    [Fact]
    public void SamuraiWeavesMatchTheChart() => AssertMatches(34, ActionKind.OGcd,
    [
        SamuraiActions.MeikyoShisui, // -14s
        SamuraiActions.TrueNorth,    // -5s
        SamuraiActions.Ikishoten,
        SamuraiActions.HissatsuSenei,
        SamuraiActions.MeikyoShisui,
        SamuraiActions.Zanshin,
        SamuraiActions.Shoha,
        SamuraiActions.HissatsuShinten,
        SamuraiActions.HissatsuGyoten,
        SamuraiActions.HissatsuShinten,
    ]);

    // ---- Viper: "Standard Opener", 7.5 -----------------------------------

    [Fact]
    public void ViperGlobalsMatchTheChart() => AssertMatches(41, ActionKind.Gcd,
    [
        ViperActions.SteelFangs,
        ViperActions.SwiftskinsSting,
        ViperActions.Vicewinder,
        ViperActions.HuntersCoil,
        ViperActions.SwiftskinsCoil,
        ViperActions.Reawaken,
        ViperActions.FirstGeneration,
        ViperActions.SecondGeneration,
        ViperActions.ThirdGeneration,
        ViperActions.FourthGeneration,
        ViperActions.Ouroboros,
        ViperActions.UncoiledFury,
        ViperActions.UncoiledFury,
    ]);

    [Fact]
    public void ViperWeavesMatchTheChart() => AssertMatches(41, ActionKind.OGcd,
    [
        ViperActions.Slither,
        ViperActions.SerpentsIre,
        ViperActions.TwinfangBite,
        ViperActions.TwinbloodBite,
        ViperActions.TwinbloodBite,
        ViperActions.TwinfangBite,
        ViperActions.FirstLegacy,
        ViperActions.SecondLegacy,
        ViperActions.ThirdLegacy,
        ViperActions.FourthLegacy,
        ViperActions.UncoiledTwinfang,
        ViperActions.UncoiledTwinblood,
        ViperActions.UncoiledTwinfang,
        ViperActions.UncoiledTwinblood,
    ]);

    /// <summary>
    /// Each Generation is followed immediately by its own Legacy. Pairing them wrongly is
    /// the easiest slip to make here and the hardest to see in a list of near-identical
    /// names.
    /// </summary>
    [Fact]
    public void EachViperGenerationIsFollowedByItsOwnLegacy()
    {
        var steps = OpenerFor(41).Steps;

        (ActionRef Generation, ActionRef Legacy)[] pairs =
        [
            (ViperActions.FirstGeneration, ViperActions.FirstLegacy),
            (ViperActions.SecondGeneration, ViperActions.SecondLegacy),
            (ViperActions.ThirdGeneration, ViperActions.ThirdLegacy),
            (ViperActions.FourthGeneration, ViperActions.FourthLegacy),
        ];

        foreach (var (generation, legacy) in pairs)
        {
            var at = steps.ToList().FindIndex(s => s.Id == generation.Id);
            Assert.True(at >= 0, $"{generation.Name} is not in the opener");
            Assert.True(at + 1 < steps.Count, $"{generation.Name} is the last step");
            Assert.True(
                steps[at + 1].Id == legacy.Id,
                $"{generation.Name} is followed by {steps[at + 1].Name}, expected {legacy.Name}");
        }
    }

    // ---- Bard: "Adjusted Standard Opener (2.48 GCD ideal)", 7.0 ----------

    [Fact]
    public void BardGlobalsMatchTheChart() => AssertMatches(23, ActionKind.Gcd,
    [
        BardActions.Stormbite,
        BardActions.CausticBite,
        BardActions.BurstShot,
        BardActions.BurstShot,
        BardActions.RefulgentArrow,
        BardActions.RadiantEncore,
        BardActions.ResonantArrow,
        BardActions.RefulgentArrow,
        BardActions.BurstShot,
        BardActions.IronJaws,
        BardActions.BurstShot,
    ]);

    [Fact]
    public void BardWeavesMatchTheChart() => AssertMatches(23, ActionKind.OGcd,
    [
        BardActions.HeartbreakShot, // the pull
        BardActions.WanderersMinuet,
        BardActions.EmpyrealArrow,
        BardActions.BattleVoice,
        BardActions.RadiantFinale,
        BardActions.RagingStrikes,
        BardActions.Barrage,
        BardActions.Sidewinder,
        BardActions.EmpyrealArrow,
        BardActions.PitchPerfect,
    ]);

    // ---- Machinist: "Standard Opener (AA)", 7.0 --------------------------

    [Fact]
    public void MachinistGlobalsMatchTheChart() => AssertMatches(31, ActionKind.Gcd,
    [
        MachinistActions.AirAnchor,
        MachinistActions.Drill,
        MachinistActions.ChainSaw,
        MachinistActions.Excavator,
        MachinistActions.Drill,
        MachinistActions.FullMetalField,
        MachinistActions.BlazingShot,
        MachinistActions.BlazingShot,
        MachinistActions.BlazingShot,
        MachinistActions.BlazingShot,
        MachinistActions.BlazingShot,
        MachinistActions.Drill,
        MachinistActions.HeatedSplitShot,
        MachinistActions.HeatedSlugShot,
        MachinistActions.HeatedCleanShot,
    ]);

    [Fact]
    public void MachinistWeavesMatchTheChart() => AssertMatches(31, ActionKind.OGcd,
    [
        MachinistActions.Reassemble, // -5s
        MachinistActions.Checkmate,
        MachinistActions.DoubleCheck,
        MachinistActions.BarrelStabilizer,
        MachinistActions.AutomatonQueen,
        MachinistActions.Reassemble,
        MachinistActions.Checkmate,
        MachinistActions.Wildfire,
        MachinistActions.DoubleCheck,
        MachinistActions.Hypercharge,
        MachinistActions.Checkmate,
        MachinistActions.DoubleCheck,
        MachinistActions.Checkmate,
        MachinistActions.DoubleCheck,
        MachinistActions.Checkmate,
        MachinistActions.DoubleCheck,
        MachinistActions.Checkmate,
        MachinistActions.DoubleCheck,
    ]);

    /// <summary>
    /// Hypercharge is what makes the five Blazing Shots possible, so it has to land on the
    /// global immediately before them. Wildfire has to be a global earlier still, so its
    /// window covers all five.
    /// </summary>
    [Fact]
    public void MachinistHyperchargeOpensTheBlazingShotRun()
    {
        var steps = OpenerFor(31).Steps.ToList();
        var hypercharge = steps.FindIndex(s => s.Id == MachinistActions.Hypercharge.Id);
        var wildfire = steps.FindIndex(s => s.Id == MachinistActions.Wildfire.Id);

        Assert.True(hypercharge > 0, "Hypercharge is not in the opener");
        Assert.True(wildfire >= 0 && wildfire < hypercharge, "Wildfire must come before Hypercharge");

        var after = steps.Skip(hypercharge + 1).Where(s => s.Kind == ActionKind.Gcd).Take(5);
        Assert.All(after, s => Assert.Equal(MachinistActions.BlazingShot.Id, s.Id));
    }

    // ---- Pictomancer: "2nd GCD Starry Opener", 7.2 -----------------------

    [Fact]
    public void PictomancerGlobalsMatchTheChart() => AssertMatches(42, ActionKind.Gcd,
    [
        PictomancerActions.RainbowDrip, // pre-pull
        PictomancerActions.WingMotif,
        PictomancerActions.HammerStamp,
        PictomancerActions.BlizzardInCyan,
        PictomancerActions.StoneInYellow,
        PictomancerActions.ThunderInMagenta,
        PictomancerActions.CometInBlack,
        PictomancerActions.StarPrism,
        PictomancerActions.HammerBrush,
        PictomancerActions.PolishingHammer,
        PictomancerActions.RainbowDrip,
        PictomancerActions.FireInRed,
        PictomancerActions.AeroInGreen,
    ]);

    [Fact]
    public void PictomancerWeavesMatchTheChart() => AssertMatches(42, ActionKind.OGcd,
    [
        PictomancerActions.PomMuse,
        PictomancerActions.StrikingMuse,
        PictomancerActions.StarryMuse,
        PictomancerActions.SubtractivePalette,
        PictomancerActions.WingedMuse,
        PictomancerActions.MogOfTheAges,
        PictomancerActions.Swiftcast,
    ]);

    /// <summary>
    /// The chart's name is a claim about timing: Starry Muse goes up between the first and
    /// second global, so the buff covers from Hammer Stamp on rather than from Wing Motif.
    /// </summary>
    [Fact]
    public void PictomancerStarryMuseLandsBeforeTheSecondGlobal()
    {
        var steps = OpenerFor(42).Steps;
        var muse = steps.ToList().FindIndex(s => s.Id == PictomancerActions.StarryMuse.Id);

        Assert.True(muse > 0, "Starry Muse is not in the opener");

        var globalsBefore = steps.Take(muse).Count(s => s.Kind == ActionKind.Gcd);
        Assert.Equal(2, globalsBefore); // pre-pull Rainbow Drip, then Wing Motif
        Assert.Equal(PictomancerActions.HammerStamp.Id, steps[muse + 1].Id);
    }

    // ---- Red Mage: "Standard Opener", 7.0 --------------------------------

    [Fact]
    public void RedMageGlobalsMatchTheChart() => AssertMatches(35, ActionKind.Gcd,
    [
        RedMageActions.VeraeroIII, // pre-pull
        RedMageActions.VerthunderIII,
        RedMageActions.VerthunderIII,
        RedMageActions.VerthunderIII,
        RedMageActions.EnchantedRiposte,
        RedMageActions.EnchantedZwerchhau,
        RedMageActions.EnchantedRedoublement,
        RedMageActions.Verholy,
        RedMageActions.Scorch,
        RedMageActions.Resolution,
        RedMageActions.GrandImpact,
        RedMageActions.Verfire,
        RedMageActions.GrandImpact,
        RedMageActions.VerthunderIII,
        RedMageActions.VeraeroIII,
        RedMageActions.Verfire,
        RedMageActions.VerthunderIII,
        RedMageActions.Verstone,
        RedMageActions.VeraeroIII,
        RedMageActions.VeraeroIII,
    ]);

    [Fact]
    public void RedMageWeavesMatchTheChart() => AssertMatches(35, ActionKind.OGcd,
    [
        RedMageActions.Swiftcast,
        RedMageActions.Fleche,
        RedMageActions.Acceleration,
        RedMageActions.Embolden,
        RedMageActions.Manafication,
        RedMageActions.ContreSixte,
        RedMageActions.Engagement,
        RedMageActions.CorpsACorps,
        RedMageActions.ViceOfThorns,
        RedMageActions.Engagement,
        RedMageActions.CorpsACorps,
        RedMageActions.Prefulgence,
        RedMageActions.Acceleration,
        RedMageActions.Fleche,
        RedMageActions.Swiftcast,
        RedMageActions.ContreSixte,
    ]);

    // ---- Structural checks that hold for every opener ---------------------

    public static TheoryData<uint, string> JobsWithAnOpener()
    {
        var data = new TheoryData<uint, string>();
        foreach (var job in JobRegistry.CreateAll())
        {
            if (job.Opener is not null)
                data.Add(job.JobId, job.Name);
        }

        return data;
    }

    /// <summary>
    /// Weaving three off-globals into one window clips the next global. No Balance chart
    /// asks for it, so a run of three here is a transcription slip: an off-global has been
    /// dropped into the wrong gap.
    /// </summary>
    [Theory]
    [MemberData(nameof(JobsWithAnOpener))]
    public void NoWindowInAnyOpenerAsksForThreeWeaves(uint jobId, string name)
    {
        var steps = OpenerFor(jobId).Steps;

        var run = 0;
        for (var i = 0; i < steps.Count; i++)
        {
            run = steps[i].Kind == ActionKind.OGcd ? run + 1 : 0;
            Assert.True(run <= 2, $"{name}: three off-globals in a row ending at step {i + 1}");
        }
    }

    /// <summary>
    /// An opener declared for a level the player cannot have every step at would abandon
    /// itself on the first missing action, which is worse than not having one.
    /// </summary>
    [Theory]
    [MemberData(nameof(JobsWithAnOpener))]
    public void EveryStepIsUnlockedAtTheOpenersOwnLevel(uint jobId, string name)
    {
        var opener = OpenerFor(jobId);

        foreach (var step in opener.Steps)
        {
            Assert.True(
                step.Level <= opener.MinimumLevel,
                $"{name}: {step.Name} unlocks at {step.Level}, opener claims {opener.MinimumLevel}");
        }
    }

    /// <summary>The potion point has to name a step that exists.</summary>
    [Theory]
    [MemberData(nameof(JobsWithAnOpener))]
    public void ThePotionPointIsAStepInTheOpener(uint jobId, string name)
    {
        var opener = OpenerFor(jobId);

        Assert.True(opener.PotionBeforeStep >= 0, $"{name}: no potion point");
        Assert.True(
            opener.PotionBeforeStep < opener.Steps.Count,
            $"{name}: potion point {opener.PotionBeforeStep} is past the end of the opener");
    }
}
