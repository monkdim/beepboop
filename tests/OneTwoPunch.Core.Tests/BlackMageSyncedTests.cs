using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.BlackMage.BlackMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Black Mage below level 100, where every rule that asks about a spell the player has not
/// learned yet quietly inverts.
/// <para>
/// <see cref="RotationContext.Ready"/> answers false for an action the player does not have,
/// which is right - but it makes <c>!Ready(x)</c> trivially true below x's level, and the
/// fire phase is built out of exactly those tests: "no Fire IV left" and "no Despair left"
/// are how it knows the bar is spent. Recorded pulls at 50 and 68 show what that costs -
/// Manafont thrown on a near-full bar, an ice phase that could never end, and a fire phase
/// one global long that never cast Fire at all.
/// </para>
/// </summary>
public sealed class BlackMageSyncedTests
{
    private static RotationSession Session() =>
        new(JobRegistry.Create(25)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        });

    private static SnapshotBuilder At(byte level) =>
        new SnapshotBuilder().Job(25).Level(level).NoCombo().Enemies(1);

    /// <summary>Mid-global, so an off-global can be suggested.</summary>
    private static SnapshotBuilder Weaving(byte level) => At(level).Gcd(2.2f);

    /// <summary>On the global, so the answer is the next cast.</summary>
    private static SnapshotBuilder Casting(byte level) => At(level).Gcd(0f);

    /// <summary>
    /// Ley Lines and Amplifier sit above everything here in the off-global list, so both are
    /// put away. Manafont is left alone - half these tests are about it.
    /// </summary>
    private static uint Suggest(
        SnapshotBuilder builder,
        FakeActionState? actions = null,
        RotationMode mode = RotationMode.SingleTarget)
    {
        var state = actions ?? new FakeActionState();
        state.OnCooldown(A.LeyLines.Id, 60f);
        state.OnCooldown(A.Amplifier.Id, 60f);

        return Session().Resolve(mode, builder.Build(), state).Action.Id;
    }

    private static SnapshotBuilder AstralFireThree(SnapshotBuilder builder, uint mp) =>
        builder.Gauge(s =>
        {
            s.Gauges.BlackMage.AstralFire = 3;
            s.Gauges.BlackMage.ElementTimeRemaining = 15f;
            s.Mp = mp;
        });

    private static SnapshotBuilder UmbralIceThree(SnapshotBuilder builder, uint mp, byte hearts) =>
        builder.Gauge(s =>
        {
            s.Gauges.BlackMage.UmbralIce = 3;
            s.Gauges.BlackMage.ElementTimeRemaining = 15f;
            s.Gauges.BlackMage.UmbralHearts = hearts;
            s.Mp = mp;
        });

    // ---- Manafont ---------------------------------------------------------

    /// <summary>
    /// "Manafont ... mp 8400", labelled "the bar is spent", eight seconds into a level 50
    /// pull. Despair is level 72, so the test that was meant to mean "no mana left" meant
    /// nothing at all.
    /// </summary>
    [Fact]
    public void ManafontIsNotThrownOnANearFullBarBeforeDespairExists()
    {
        var suggestion = Suggest(AstralFireThree(Weaving(50), 8400));

        Assert.NotEqual(A.Manafont.Id, suggestion);
    }

    /// <summary>The same at 68, where Fire IV exists but Despair still does not.</summary>
    [Fact]
    public void ManafontIsNotThrownOnANearFullBarAtSixtyEight()
    {
        var suggestion = Suggest(AstralFireThree(Weaving(68), 8000));

        Assert.NotEqual(A.Manafont.Id, suggestion);
    }

    /// <summary>But it is still taken when the bar really is spent.</summary>
    [Fact]
    public void ManafontIsTakenOnceTheBarIsSpentBeforeDespairExists()
    {
        var suggestion = Suggest(
            AstralFireThree(Weaving(50), 800),
            new FakeActionState().Unusable(A.Fire1.Id));

        Assert.Equal(A.Manafont.Id, suggestion);
    }

    // ---- Astral Fire below Fire IV ----------------------------------------

    /// <summary>
    /// The whole fire phase at level 50 is Fire I, and there was no rule that could say so:
    /// Flare Star, Despair and Fire IV are all above 50, so the list fell past Astral Fire
    /// entirely and answered "into Umbral Ice" the global after arriving.
    /// </summary>
    [Fact]
    public void AstralFireCastsFireOneBeforeFireFourExists()
    {
        var suggestion = Suggest(AstralFireThree(Casting(50), 10000));

        Assert.Equal(A.Fire1.Id, suggestion);
    }

    /// <summary>And the phase is not abandoned the moment it begins.</summary>
    [Fact]
    public void TheFirePhaseIsNotLeftWhileTheBarCanStillPayForAFireOne()
    {
        var suggestion = Suggest(AstralFireThree(Weaving(50), 10000));

        Assert.NotEqual(A.Transpose.Id, suggestion);
        Assert.NotEqual(A.Blizzard3.Id, suggestion);
    }

    /// <summary>It is left when the bar cannot, and by Transpose rather than a hot Blizzard III.</summary>
    [Fact]
    public void FireIsLeftOnceTheBarCannotPayForAnotherFireOne()
    {
        var suggestion = Suggest(
            AstralFireThree(Weaving(50), 0),
            new FakeActionState().Unusable(A.Fire1.Id).OnCooldown(A.Manafont.Id, 60f));

        Assert.Equal(A.Transpose.Id, suggestion);
    }

    /// <summary>At full level Fire I is still never the answer in fire - Fire IV is.</summary>
    [Fact]
    public void FireOneIsNotSuggestedInAstralFireAtFullLevel()
    {
        var suggestion = Suggest(AstralFireThree(Casting(100), 10000));

        Assert.Equal(A.Fire4.Id, suggestion);
    }

    /// <summary>Nor when Fire IV will not cast - that is Despair's global, not Fire I's.</summary>
    [Fact]
    public void FireOneIsNotSuggestedInPlaceOfDespair()
    {
        var suggestion = Suggest(
            AstralFireThree(Casting(100), 2000),
            new FakeActionState().Unusable(A.Fire4.Id));

        Assert.Equal(A.Despair.Id, suggestion);
    }

    // ---- Umbral Ice below Blizzard IV -------------------------------------

    /// <summary>
    /// Hearts come from Blizzard IV, which is level 58, so at 50 they are permanently zero -
    /// and the ice phase, which required three of them to end, could never end. The recorded
    /// pull shows it fall out of ice sideways into a neutral Fire I instead.
    /// </summary>
    [Fact]
    public void IceEndsWithoutHeartsBeforeBlizzardFourExists()
    {
        var suggestion = Suggest(UmbralIceThree(Weaving(50), 10000, 0));

        Assert.Equal(A.Transpose.Id, suggestion);
    }

    /// <summary>The filler underneath it is an ice spell, not the Fire I it fell through to.</summary>
    [Fact]
    public void TheIceFillerIsBlizzardOneBeforeBlizzardFourExists()
    {
        var suggestion = Suggest(UmbralIceThree(Casting(50), 7000, 0));

        Assert.Equal(A.Blizzard1.Id, suggestion);
    }

    /// <summary>And ice is still not left on a half-empty bar at 50.</summary>
    [Fact]
    public void IceIsNotLeftOnAHalfEmptyBarBeforeBlizzardFourExists()
    {
        var suggestion = Suggest(UmbralIceThree(Weaving(50), 5000, 0));

        Assert.NotEqual(A.Transpose.Id, suggestion);
        Assert.NotEqual(A.Fire3.Id, suggestion);
    }

    /// <summary>Once Blizzard IV exists the hearts are owed again, and they come first.</summary>
    [Fact]
    public void HeartsAreStillFilledOnceBlizzardFourExists()
    {
        var suggestion = Suggest(UmbralIceThree(Casting(68), 10000, 0));

        Assert.Equal(A.Blizzard4.Id, suggestion);
    }

    // ---- The same shape on the AoE button ---------------------------------

    /// <summary>
    /// Flare empties the bar, and "no High Fire II left" is what marked it as the last cast
    /// of the phase. High Fire II is level 82, so below that Flare was the *first* cast of
    /// every fire phase instead.
    /// </summary>
    [Fact]
    public void FlareIsNotTheOpeningGlobalOfAnAoeFirePhase()
    {
        var suggestion = Suggest(
            AstralFireThree(Casting(50).Enemies(3), 10000),
            mode: RotationMode.Aoe);

        Assert.Equal(A.Fire2.Id, suggestion);
    }
}
