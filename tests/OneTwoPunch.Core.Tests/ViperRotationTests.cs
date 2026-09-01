using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Reawaken, Vicewinder and Vicepit were gated on the combo being broken, which in a working
/// loop never happens - a finisher leaves the combo live rather than clearing it.
/// <para>
/// That one condition took most of the job with it. No Vicewinder means no Hunter's Coil or
/// Swiftskin's Coil, which are what Twinfang and Twinblood follow; no Reawaken means no
/// Generation chain, no Ouroboros and no Serpent's Tail. A recorded pull is seventy seconds
/// of Steel Fangs, a sting and a finisher on repeat, with Ready to Reawaken counting down
/// from twenty-nine to nothing untouched.
/// </para>
/// </summary>
public sealed class ViperRotationTests
{
    private static SnapshotBuilder Standing() =>
        new SnapshotBuilder().Job(41).Gcd(0f).Enemies(1);

    /// <summary>Mid-window, where an off-global is what the button should become.</summary>
    private static SnapshotBuilder InAWeaveWindow() =>
        new SnapshotBuilder().Job(41).Gcd(2f).Enemies(1).Combo(A.HindsbaneFang.Id);

    private static uint Suggest(SnapshotBuilder builder, RotationMode mode = RotationMode.SingleTarget)
    {
        // Serpent's Ire on cooldown rather than unusable: an Ire that is off cooldown reads
        // as "the burst is imminent", which holds Vicewinder and Vicepit for it - so these
        // tests, which are about the ordinary loop, keep it a minute away.
        var actions = new FakeActionState().OnCooldown(A.SerpentsIre.Id, 60f);

        // The Reawaken chain is offered by readiness rather than by a gauge check, so in a
        // test where everything is usable it would win every global.
        foreach (var id in new[]
                 {
                     A.FirstGeneration.Id, A.SecondGeneration.Id,
                     A.ThirdGeneration.Id, A.FourthGeneration.Id, A.Ouroboros.Id,
                 })
        {
            actions.Unusable(id);
        }

        return new RotationSession(JobRegistry.Create(41)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        }).Resolve(mode, builder.Build(), actions).Action.Id;
    }

    /// <summary>
    /// The state the loop actually sits in: a finisher just landed, so the combo is live and
    /// nothing mid-combo matches. This is where Reawaken belongs, and where it never went.
    /// </summary>
    [Fact]
    public void AFullSerpentOfferingReawakensAfterAFinisher()
    {
        var suggestion = Suggest(
            Standing()
                .Combo(A.HindsbaneFang.Id)
                .Gauge(s => s.Gauges.Viper.SerpentOffering = 50));

        Assert.Equal(A.Reawaken.Id, suggestion);
    }

    /// <summary>Ready to Reawaken is the other way in, and it expired unused in the log.</summary>
    [Fact]
    public void ReadyToReawakenIsSpent()
    {
        var suggestion = Suggest(Standing().Combo(A.HindsbaneFang.Id).Buff(A.ReawakenReady.Id, 20f));

        Assert.Equal(A.Reawaken.Id, suggestion);
    }

    /// <summary>And with neither, the same slot is Vicewinder's.</summary>
    [Fact]
    public void VicewinderTakesTheComboStarterGlobal()
    {
        var suggestion = Suggest(Standing().Combo(A.HindsbaneFang.Id));

        Assert.Equal(A.Vicewinder.Id, suggestion);
    }

    /// <summary>
    /// Which is what makes the coils reachable at all - but only once they are asked for
    /// where the answer lives. Vicewinder's chain is a gauge field, not the combo: it never
    /// touches the combo, which is both why the coils were never suggested and why the combo
    /// was never "broken" the way three other rules used to ask for.
    /// </summary>
    [Fact]
    public void TheCoilFollowsVicewinder()
    {
        var suggestion = Suggest(
            Standing()
                .Combo(A.HindsbaneFang.Id)
                .Gauge(s => s.Gauges.Viper.DreadCombo = DreadCombo.Vicewinder));

        Assert.Contains(suggestion, new[] { A.HuntersCoil.Id, A.SwiftskinsCoil.Id });
    }

    /// <summary>And the second coil follows the first.</summary>
    [Fact]
    public void TheOtherCoilFollowsTheFirst()
    {
        var suggestion = Suggest(
            Standing()
                .Combo(A.HindsbaneFang.Id)
                .Gauge(s => s.Gauges.Viper.DreadCombo = DreadCombo.HuntersCoil));

        Assert.Equal(A.SwiftskinsCoil.Id, suggestion);
    }

    /// <summary>
    /// Serpent's Tail is a hotbar placeholder the game draws and swaps but will not accept,
    /// so every rule written against its id asked for something unusable. The gauge names
    /// the real follow-up outright.
    /// </summary>
    [Theory]
    [InlineData(SerpentCombo.DeathRattle, 34634u)]
    [InlineData(SerpentCombo.FirstLegacy, 34640u)]
    [InlineData(SerpentCombo.SecondLegacy, 34641u)]
    [InlineData(SerpentCombo.ThirdLegacy, 34642u)]
    [InlineData(SerpentCombo.FourthLegacy, 34643u)]
    public void TheTailFollowUpIsNamedByTheGauge(SerpentCombo combo, uint expected)
    {
        var suggestion = Suggest(
            InAWeaveWindow().Gauge(s => s.Gauges.Viper.SerpentCombo = combo));

        Assert.Equal(expected, suggestion);
    }

    /// <summary>With nothing live, the tail rule must not fire at all.</summary>
    [Fact]
    public void TheTailIsSilentWhenNothingIsLive()
    {
        var suggestion = Suggest(InAWeaveWindow());

        Assert.NotEqual(A.DeathRattle.Id, suggestion);
        Assert.NotEqual(A.FirstLegacy.Id, suggestion);
    }

    /// <summary>
    /// The Twin pair chains off one coil: Hunter's Coil leaves Hunter's Venom for Twinfang
    /// Bite, which leaves Swiftskin's Venom for Twinblood Bite. A recorded pull had Poised
    /// for Twinfang sitting unspent on the player for a full minute.
    /// </summary>
    [Theory]
    [InlineData(3657u, 34636u)] // Hunter's Venom     -> Twinfang Bite
    [InlineData(3658u, 34637u)] // Swiftskin's Venom  -> Twinblood Bite
    [InlineData(3665u, 34644u)] // Poised for Twinfang -> Uncoiled Twinfang
    [InlineData(3666u, 34645u)] // Poised for Twinblood -> Uncoiled Twinblood
    public void EachVenomNamesItsFollowUp(uint status, uint expected)
    {
        var suggestion = Suggest(InAWeaveWindow().Buff(status, 30f));

        Assert.Equal(expected, suggestion);
    }

    /// <summary>The AoE list has its own pair, off the dens.</summary>
    [Theory]
    [InlineData(3659u, 34638u)] // Fellhunter's Venom -> Twinfang Thresh
    [InlineData(3660u, 34639u)] // Fellskin's Venom   -> Twinblood Thresh
    public void EachFellVenomNamesItsFollowUp(uint status, uint expected)
    {
        var suggestion = Suggest(
            InAWeaveWindow().Enemies(3).Buff(status, 30f),
            RotationMode.Aoe);

        Assert.Equal(expected, suggestion);
    }

    /// <summary>But none of them may interrupt a combo that is still running.</summary>
    [Fact]
    public void AFinisherStillComesFirst()
    {
        var suggestion = Suggest(
            Standing()
                .Combo(A.HuntersSting.Id)
                .Buff(A.FlankstungVenom.Id, 40f)
                .Gauge(s => s.Gauges.Viper.SerpentOffering = 100));

        Assert.Equal(A.FlankstingStrike.Id, suggestion);
    }

    /// <summary>Nor the second step of one.</summary>
    [Fact]
    public void AStingStillComesFirst()
    {
        var suggestion = Suggest(
            Standing()
                .Combo(A.SteelFangs.Id)
                .Gauge(s => s.Gauges.Viper.SerpentOffering = 100));

        Assert.Contains(suggestion, new[] { A.HuntersSting.Id, A.SwiftskinsSting.Id });
    }

    /// <summary>
    /// The button read "First Generation" for the whole Reawaken chain, because the game
    /// accepts any of the four and adjusts the press to the step you are on - so offering
    /// all four and letting readiness choose always chose the first. It cast correctly and
    /// looked broken, and looking broken is the whole product here.
    /// </summary>
    [Theory]
    [InlineData(5, 34627u)] // First Generation
    [InlineData(4, 34628u)] // Second Generation
    [InlineData(3, 34629u)] // Third Generation
    [InlineData(2, 34630u)] // Fourth Generation
    [InlineData(1, 34631u)] // Ouroboros
    public void TheGenerationChainNamesTheStepItIsOn(int tribute, uint expected)
    {
        var actions = new FakeActionState().Unusable(A.SerpentsIre.Id);

        var suggestion = new RotationSession(JobRegistry.Create(41)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        }).Resolve(
            RotationMode.SingleTarget,
            Standing().Gauge(s => s.Gauges.Viper.AnguineTribute = (byte)tribute).Build(),
            actions);

        Assert.Equal(expected, suggestion.Action.Id);
    }

    /// <summary>The AoE list had the same gate on Vicepit and Reawaken.</summary>
    [Fact]
    public void VicepitTakesTheAoeComboStarterGlobal()
    {
        var suggestion = Suggest(
            Standing().Enemies(3).Combo(A.BloodiedMaw.Id),
            RotationMode.Aoe);

        Assert.Equal(A.Vicepit.Id, suggestion);
    }
}
