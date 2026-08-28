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
}
