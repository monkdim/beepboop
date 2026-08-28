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

    private static uint Suggest(SnapshotBuilder builder, RotationMode mode = RotationMode.SingleTarget)
    {
        var actions = new FakeActionState();

        // The Reawaken chain and the tail follow-ups are offered unconditionally and picked
        // by readiness, so in a test where everything is usable they would win every global.
        foreach (var id in new[]
                 {
                     A.SerpentsTail.Id, A.Twinfang.Id, A.Twinblood.Id, A.SerpentsIre.Id,
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

    /// <summary>Which is what makes the coils reachable at all.</summary>
    [Fact]
    public void TheCoilFollowsVicewinder()
    {
        var suggestion = Suggest(Standing().Combo(A.Vicewinder.Id));

        Assert.Contains(suggestion, new[] { A.HuntersCoil.Id, A.SwiftskinsCoil.Id });
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
