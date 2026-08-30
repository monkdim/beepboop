using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.BlackMage.BlackMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The AoE loop from The Balance's multi-target chart, which the AoE list had never once been
/// checked against.
/// <para>
/// Freeze, a filler, Transpose, Flare, Flare, Flare Star, a filler, Transpose. The chart says
/// what it is doing in its own first line - "Transpose leveraged to skip both High Fire II
/// and High Blizzard II" - and the list was changing phase with exactly those two spells,
/// with Flare written as the last cast of the fire phase rather than the whole of it.
/// </para>
/// </summary>
public sealed class BlackMageAoeLoopTests
{
    private static SnapshotBuilder Pack() =>
        new SnapshotBuilder().Job(25).Level(100).Gcd(0f).Enemies(4).Buff(A.Thunderhead.Id, 30f);

    /// <summary>Mid-window, where the Transpose that changes phase would be woven.</summary>
    private static SnapshotBuilder PackWeaving() =>
        new SnapshotBuilder().Job(25).Level(100).Gcd(2f).Enemies(4).Buff(A.Thunderhead.Id, 30f);

    /// <summary>
    /// The three off-globals that sit above Transpose and would otherwise take every weave
    /// slot in the tests. None of these tests are about them.
    /// </summary>
    private static FakeActionState Weaves() =>
        new FakeActionState()
            .Unusable(A.LeyLines.Id)
            .Unusable(A.Amplifier.Id)
            .Unusable(A.Manafont.Id);

    private static Suggestion Suggest(SnapshotBuilder builder, FakeActionState? actions = null) =>
        new RotationSession(JobRegistry.Create(25)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        }).Resolve(RotationMode.Aoe, builder.Build(), actions ?? Weaves());

    /// <summary>The ice phase is there to buy three Umbral Hearts, and Freeze is what buys them.</summary>
    [Fact]
    public void TheIcePhaseBuysHeartsWithFreeze()
    {
        var suggestion = Suggest(
            Pack().Mp(4000).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 1;
                    s.Gauges.BlackMage.UmbralHearts = 0;
                }));

        Assert.Equal(A.Freeze.Id, suggestion.Action.Id);
    }

    /// <summary>On two targets the chart says Blizzard IV instead. Same three hearts.</summary>
    [Fact]
    public void TwoTargetsBuyTheSameHeartsWithBlizzardFour()
    {
        var suggestion = Suggest(
            Pack().Enemies(2).Mp(4000).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 1;
                    s.Gauges.BlackMage.UmbralHearts = 0;
                }));

        Assert.Equal(A.Blizzard4.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// With the hearts bought and the mana back, Transpose takes the loop into Astral Fire -
    /// not High Fire II, which is the spell the chart exists to skip.
    /// </summary>
    [Fact]
    public void TransposeTakesTheLoopIntoAstralFire()
    {
        var suggestion = Suggest(
            PackWeaving().Mp(9000).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 1;
                    s.Gauges.BlackMage.UmbralHearts = 3;
                }));

        Assert.Equal(A.Transpose.Id, suggestion.Action.Id);
    }

    /// <summary>But not while a filler still wants the global it would be woven after.</summary>
    [Fact]
    public void TheFillerGetsItsGlobalBeforeTranspose()
    {
        var suggestion = Suggest(
            PackWeaving().Mp(9000).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 1;
                    s.Gauges.BlackMage.UmbralHearts = 3;
                    s.Gauges.BlackMage.PolyglotStacks = 2;
                }));

        Assert.NotEqual(A.Transpose.Id, suggestion.Action.Id);
        Assert.Equal(A.Foul.Id, suggestion.NextGcd?.Id);
    }

    /// <summary>The fire phase is Flare, not High Fire II with Flare on the end of it.</summary>
    [Fact]
    public void TheFirePhaseIsFlare()
    {
        var suggestion = Suggest(
            Pack().Mp(9000).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.AstralFire = 3;
                    s.Gauges.BlackMage.UmbralHearts = 3;
                }));

        Assert.Equal(A.Flare.Id, suggestion.Action.Id);
    }

    /// <summary>Two Flares is six Astral Soul, and six Astral Soul is Flare Star.</summary>
    [Fact]
    public void SixAstralSoulIsFlareStar()
    {
        var suggestion = Suggest(
            Pack().Mp(0).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.AstralFire = 3;
                    s.Gauges.BlackMage.AstralSoulStacks = 6;
                }));

        Assert.Equal(A.FlareStar.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// And with the bar empty and the Flare Star spent, Transpose goes back to ice. Flare
    /// needs eight hundred mana, so an empty bar is the whole of "the fire phase is over".
    /// </summary>
    [Fact]
    public void TransposeTakesTheLoopBackToIce()
    {
        var actions = Weaves().Unusable(A.Flare.Id);

        var suggestion = Suggest(
            PackWeaving().Mp(0).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.AstralFire = 3;
                    s.Gauges.BlackMage.AstralSoulStacks = 0;
                }),
            actions);

        Assert.Equal(A.Transpose.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// It must not hand the phase back while there is still a Flare to cast. Transposing out
    /// of a fire phase that has mana left is the whole loop thrown away.
    /// </summary>
    [Fact]
    public void TransposeDoesNotLeaveAFirePhaseThatStillHasMana()
    {
        var suggestion = Suggest(
            PackWeaving().Mp(9000).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.AstralFire = 3;
                    s.Gauges.BlackMage.UmbralHearts = 3;
                }));

        Assert.NotEqual(A.Transpose.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Nor into an ice phase that has not bought its hearts yet - one Flare with no hearts
    /// eats the whole bar and there is no second.
    /// </summary>
    [Fact]
    public void TransposeDoesNotLeaveIceWithoutItsHearts()
    {
        var suggestion = Suggest(
            PackWeaving().Mp(10000).Debuff(A.HighThunderII.Id, 20f)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 1;
                    s.Gauges.BlackMage.UmbralHearts = 0;
                }));

        Assert.NotEqual(A.Transpose.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Below Flare Star's level there is no Transpose loop to run. Astral Soul arrives with
    /// Flare Star at 100, and without it two Flares build towards nothing - so those levels
    /// keep the phase change by spell, which is the rotation they actually have.
    /// </summary>
    [Fact]
    public void TheTransposeLoopIsOnlyForLevelsThatHaveFlareStar()
    {
        var suggestion = Suggest(
            PackWeaving().Level(90).Mp(10000)
                .Gauge(s =>
                {
                    s.Gauges.BlackMage.UmbralIce = 1;
                    s.Gauges.BlackMage.UmbralHearts = 3;
                }));

        Assert.NotEqual(A.Transpose.Id, suggestion.Action.Id);
    }

    // ---- The global the crossing weaves off -------------------------------

    /// <summary>Astral Fire with the AoE bar spent: Flare will not cast and Astral Soul is
    /// short of Flare Star.</summary>
    private static SnapshotBuilder FireIsSpentOnThePack(byte polyglot) =>
        Pack().Mp(0).Debuff(A.HighThunderII.Id, 25f)
            .Gauge(s =>
            {
                s.Gauges.BlackMage.AstralFire = 3;
                s.Gauges.BlackMage.AstralSoulStacks = 3;
                s.Gauges.BlackMage.UmbralHearts = 0;
                s.Gauges.BlackMage.PolyglotStacks = polyglot;
            });

    /// <summary>
    /// The bar spent, as the game reports it: neither Flare nor High Fire II will cast. High
    /// Fire II matters as much as Flare - the rule above this one offers it in Astral Fire
    /// with no mana test of its own, and the fake grants every action unless a test says
    /// otherwise, so leaving it out would answer High Fire II and test nothing.
    /// </summary>
    private static FakeActionState BarIsSpent() =>
        Weaves().Unusable(A.Flare.Id).Unusable(A.HighFire2.Id);

    /// <summary>
    /// The defect, from a recorded duty: the AoE loop crossed back to ice by hard-casting
    /// High Blizzard II nine times out of eleven, every one at "gcd 0.0s". Transpose is an
    /// off-global and the phase ended with nothing left to cast, so no weave window ever
    /// opened. Foul is the chart's filler and costs nothing here - the phase is over.
    /// </summary>
    [Fact]
    public void TheCrossingOutOfFireGetsAGlobalToWeaveOff()
    {
        var suggestion = Suggest(FireIsSpentOnThePack(polyglot: 1), BarIsSpent());

        Assert.Equal(A.Foul.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// And with no Polyglot banked there is no filler to be had, so the penalised cast into
    /// ice stays reachable. Withholding it would leave the list with nothing to say, which is
    /// the failure this rule exists to avoid rather than cause.
    /// </summary>
    [Fact]
    public void WithNoPolyglotTheCrossingStillHasAnAnswer()
    {
        var suggestion = Suggest(FireIsSpentOnThePack(polyglot: 0), BarIsSpent());

        Assert.Equal(A.HighBlizzard2.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// In the weave window that the filler above opens, Transpose is what crosses - free and
    /// off the global, rather than a High Blizzard II cast in Astral Fire.
    /// </summary>
    [Fact]
    public void TheWeaveThatFollowsIsTheTranspose()
    {
        var packet = PackWeaving().Mp(0).Debuff(A.HighThunderII.Id, 25f)
            .Gauge(s =>
            {
                s.Gauges.BlackMage.AstralFire = 3;
                s.Gauges.BlackMage.AstralSoulStacks = 3;
                s.Gauges.BlackMage.UmbralHearts = 0;
                s.Gauges.BlackMage.PolyglotStacks = 0;
            });

        Assert.Equal(A.Transpose.Id, Suggest(packet, BarIsSpent()).Action.Id);
    }
}
