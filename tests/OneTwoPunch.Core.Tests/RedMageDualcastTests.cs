using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.RedMage.RedMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Red Mage's filler, which is a pair of globals rather than one - and which the list was
/// getting wrong in both halves at once. There were no Red Mage tests at all before these.
/// <para>
/// Cast times, from BossMod's annotations: Verthunder III and Veraero III are five seconds,
/// Jolt III, Verfire and Verstone are two, Grand Impact is instant. Dualcast makes the next
/// spell instant and is earned by finishing a spell that had a cast time - so the loop is
/// two seconds to earn it, then a five second spell that costs nothing.
/// </para>
/// <para>
/// The list paired nothing. "Build the lower colour" on Verthunder III had no condition at
/// all, so it matched every time the list reached it and the five second casts went out
/// hard, one after another; and because it always matched, the Jolt beneath it could never
/// be reached. Reported from a pull as "Jolt isn't being cast at all, it's hard casting my
/// 5 second casts".
/// </para>
/// </summary>
public sealed class RedMageDualcastTests
{
    /// <summary>
    /// The finishers answer <see cref="RotationContext.Ready"/>, which the fake grants to
    /// everything unless a test says otherwise - so Resolution and Scorch would win every
    /// one of these. None of them are about the melee combo.
    /// </summary>
    private static FakeActionState Filler() =>
        new FakeActionState()
            .Unusable(A.Resolution.Id)
            .Unusable(A.Scorch.Id);

    /// <summary>Mana kept well under the fifty the melee combo wants, and level and colour
    /// balance stated per test.</summary>
    private static SnapshotBuilder Casting(byte white = 20, byte black = 20) =>
        new SnapshotBuilder().Job(35).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Gauge(s =>
            {
                s.Gauges.RedMage.WhiteMana = white;
                s.Gauges.RedMage.BlackMana = black;
                s.Gauges.RedMage.ManaStacks = 0;
            });

    private static uint Suggest(SnapshotBuilder builder, FakeActionState? actions = null)
    {
        var state = actions ?? Filler();
        state.OnCooldown(A.Embolden.Id, 120f);
        state.OnCooldown(A.Manafication.Id, 110f);
        state.OnCooldown(A.Fleche.Id, 25f);
        state.OnCooldown(A.ContreSixte.Id, 35f);
        state.OnCooldown(A.Acceleration.Id, 55f);
        state.OnCooldown(A.Swiftcast.Id, 60f);

        return new RotationSession(JobRegistry.Create(35)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        }).Resolve(RotationMode.SingleTarget, builder.Build(), state).Action.Id;
    }

    // ---- The half that earns the Dualcast ---------------------------------

    /// <summary>
    /// The reported bug, in one line. No Dualcast, no proc: the answer is the two second
    /// cast, not the five second one. Jolt was unreachable before this.
    /// </summary>
    [Fact]
    public void WithNoDualcastTheFillerIsJoltRatherThanAFiveSecondCast()
    {
        Assert.Equal(A.JoltIII.Id, Suggest(Casting()));
    }

    /// <summary>A proc is a two second cast too, and it expires, so it goes before Jolt.</summary>
    [Fact]
    public void WithNoDualcastAProcComesBeforeJolt()
    {
        Assert.Equal(A.Verfire.Id, Suggest(Casting().Buff(A.VerfireReady.Id, 25f)));
    }

    /// <summary>Grand Impact is already instant, so this is the global it belongs on.</summary>
    [Fact]
    public void GrandImpactIsTakenWhenThereIsNoDualcastToWaste()
    {
        Assert.Equal(A.GrandImpact.Id, Suggest(Casting().Buff(A.GrandImpactReady.Id, 25f)));
    }

    // ---- The half that spends it ------------------------------------------

    /// <summary>Dualcast in hand goes on the spell that costs the most to hard cast.</summary>
    [Fact]
    public void DualcastIsSpentOnTheFiveSecondCast()
    {
        Assert.Equal(A.VeraeroIII.Id, Suggest(Casting().Buff(A.Dualcast.Id, 15f)));
    }

    /// <summary>And on whichever colour is behind - here black, so Verthunder.</summary>
    [Fact]
    public void DualcastGoesToTheColourThatIsBehind()
    {
        Assert.Equal(A.VerthunderIII.Id, Suggest(Casting(white: 40, black: 20).Buff(A.Dualcast.Id, 15f)));
    }

    /// <summary>Swiftcast makes the next spell instant the same way, so it counts the same.</summary>
    [Fact]
    public void SwiftcastCountsTheSameAsDualcast()
    {
        Assert.Equal(A.VeraeroIII.Id, Suggest(Casting().Buff(A.SwiftcastBuff.Id, 8f)));
    }

    /// <summary>
    /// A proc under Dualcast would spend a five second saving on a two second cast. The proc
    /// keeps until the next global, which is the one that has nothing better to do.
    /// </summary>
    [Fact]
    public void AProcIsNotWhatADualcastIsSpentOn()
    {
        var packet = Casting().Buff(A.Dualcast.Id, 15f).Buff(A.VerstoneReady.Id, 25f);

        Assert.Equal(A.VeraeroIII.Id, Suggest(packet));
    }

    /// <summary>The same for Grand Impact, which is instant with or without the Dualcast.</summary>
    [Fact]
    public void GrandImpactWaitsForAGlobalThatIsNotAlreadyFree()
    {
        var packet = Casting().Buff(A.Dualcast.Id, 15f).Buff(A.GrandImpactReady.Id, 25f);

        Assert.Equal(A.VeraeroIII.Id, Suggest(packet));
    }

    // ---- Levels ------------------------------------------------------------

    /// <summary>
    /// Both halves have to exist at every level the job is played at, or the pair has a rung
    /// that cannot be climbed - which is how this list has broken before. At 70 that is Jolt
    /// II and plain Veraero: Jolt III is 84 and Veraero III is 82.
    /// </summary>
    [Fact]
    public void TheLoopStillHasBothHalvesBelowTheUpgrades()
    {
        Assert.Equal(A.JoltII.Id, Suggest(Casting().Level(70)));
        Assert.Equal(A.Veraero.Id, Suggest(Casting().Level(70).Buff(A.Dualcast.Id, 15f)));
    }

    /// <summary>
    /// And at 50, where Red Mage starts, plain Jolt is the only one of the three. Jolt is
    /// level 2, so this rung exists wherever the job does and the filler can never run out
    /// of an answer.
    /// </summary>
    [Fact]
    public void PlainJoltIsTheFloor()
    {
        Assert.Equal(A.Jolt.Id, Suggest(Casting().Level(50)));
        Assert.Equal(A.Veraero.Id, Suggest(Casting().Level(50).Buff(A.Dualcast.Id, 15f)));
    }
}
