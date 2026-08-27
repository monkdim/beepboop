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
            s.Mp = mp;
        });

    private static SnapshotBuilder UmbralIceThree(SnapshotBuilder builder, uint mp, byte hearts) =>
        builder.Gauge(s =>
        {
            s.Gauges.BlackMage.UmbralIce = 3;
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

    // ---- Below level 35, where the III-tier spells do not exist yet --------
    //
    // A levelling roulette syncs to 15 or 20 as readily as to 50, and down there the list
    // was naming Fire III and Blizzard III in every rung that matters. Both are level 35.

    /// <summary>
    /// The worst of them. In Umbral Ice with no rung that could match, the list fell all the
    /// way to its Fire I fallback - and Fire I cast in Umbral Ice *removes Umbral Ice*, so
    /// the phase the rotation had just built collapsed on the next global, every time.
    /// </summary>
    [Fact]
    public void TheIceFillerIsAnIceSpellBeforeBlizzardThreeExists()
    {
        var suggestion = Suggest(UmbralIceThree(Casting(20), 7000, 0).Debuff(A.Thunder.Id, 25f));

        Assert.Equal(A.Blizzard1.Id, suggestion);
    }

    /// <summary>Climbing the rungs is Blizzard I's job too, one at a time.</summary>
    [Fact]
    public void UmbralIceClimbsOnBlizzardOneBeforeBlizzardThreeExists()
    {
        var suggestion = Suggest(
            Casting(20).Debuff(A.Thunder.Id, 25f).Gauge(s =>
            {
                s.Gauges.BlackMage.UmbralIce = 1;
                s.Mp = 5000;
            }));

        Assert.Equal(A.Blizzard1.Id, suggestion);
    }

    /// <summary>And leaving ice is Fire I, since there is no Fire III to leave on.</summary>
    [Fact]
    public void IceIsLeftOnFireOneBeforeFireThreeExists()
    {
        var suggestion = Suggest(UmbralIceThree(Casting(20), 10000, 0).Debuff(A.Thunder.Id, 25f));

        Assert.Equal(A.Fire1.Id, suggestion);
    }

    /// <summary>The same on the way in: "into Umbral Ice" named a spell that was not there.</summary>
    [Fact]
    public void FireIsLeftOnBlizzardOneBeforeBlizzardThreeExists()
    {
        var suggestion = Suggest(
            AstralFireThree(Casting(20), 0).Debuff(A.Thunder.Id, 25f),
            new FakeActionState().Unusable(A.Fire1.Id));

        Assert.Equal(A.Blizzard1.Id, suggestion);
    }

    /// <summary>And a full bar from neither phase opens in fire, on the fire spell that exists.</summary>
    [Fact]
    public void AFullBarOpensOnFireOneBeforeFireThreeExists()
    {
        var suggestion = Suggest(Casting(20).Debuff(A.Thunder.Id, 25f));

        Assert.Equal(A.Fire1.Id, suggestion);
    }

    // ---- The dot ----------------------------------------------------------

    /// <summary>
    /// Every thunder rule asked for a Thunderhead proc, and every one named Thunder III or
    /// High Thunder. Thunder I is level 6 and predates the proc entirely, so a synced-down
    /// Black Mage cast no thunder at all - the whole dot, missing, for a whole dungeon.
    /// </summary>
    [Fact]
    public void ThunderOneIsCastBeforeTheProcExists()
    {
        var suggestion = Suggest(AstralFireThree(Casting(20), 10000));

        Assert.Equal(A.Thunder1.Id, suggestion);
    }

    /// <summary>But not over a dot that is still ticking.</summary>
    [Fact]
    public void AHealthyLowLevelDotIsNotClipped()
    {
        var suggestion = Suggest(
            AstralFireThree(Casting(20), 10000).Debuff(A.Thunder.Id, 25f));

        Assert.NotEqual(A.Thunder1.Id, suggestion);
    }

    /// <summary>
    /// And where the proc does exist the rotation still waits for it. Hard-casting High
    /// Thunder is not the level 100 rotation, and the game refuses it besides.
    /// </summary>
    [Fact]
    public void ThunderStillWaitsForTheProcAtFullLevel()
    {
        var suggestion = Suggest(AstralFireThree(Casting(100), 10000));

        Assert.Equal(A.Fire4.Id, suggestion);
    }

    // ---- The AoE button, which is most of what a roulette presses ---------

    /// <summary>
    /// Fire II is level 18, and the AoE list named it in every fire rung including the
    /// fallback - so under 18 the button had nothing it could answer with at all.
    /// </summary>
    [Fact]
    public void TheAoeButtonStillAnswersBeforeFireTwoExists()
    {
        var suggestion = Suggest(
            AstralFireThree(Casting(15).Enemies(3), 10000),
            mode: RotationMode.Aoe);

        Assert.Equal(A.Fire1.Id, suggestion);
    }

    /// <summary>And the ice side is Blizzard II rather than a Fire II that undoes the phase.</summary>
    [Fact]
    public void TheAoeIceFillerIsAnIceSpell()
    {
        var suggestion = Suggest(
            UmbralIceThree(Casting(30).Enemies(3), 7000, 0).Debuff(A.ThunderII.Id, 25f),
            mode: RotationMode.Aoe);

        Assert.Equal(A.Blizzard2.Id, suggestion);
    }

    /// <summary>The AoE dot, which the list had no rule for at any level.</summary>
    [Fact]
    public void TheAoeListKeepsTheDotUp()
    {
        var suggestion = Suggest(
            AstralFireThree(Casting(30).Enemies(3), 10000),
            mode: RotationMode.Aoe);

        Assert.Equal(A.Thunder2.Id, suggestion);
    }

    /// <summary>On its proc and in its highest form, at full level.</summary>
    [Fact]
    public void TheAoeDotIsHighThunderTwoAtFullLevel()
    {
        var suggestion = Suggest(
            AstralFireThree(Casting(100).Enemies(3), 10000).Buff(A.Thunderhead.Id, 25f),
            mode: RotationMode.Aoe);

        Assert.Equal(A.HighThunder2.Id, suggestion);
    }

    // ---- The ceiling, below level 20 --------------------------------------
    //
    // Astral Fire and Umbral Ice cap at a single stack until Aspect Mastery raises the
    // ceiling. A recorded Sastasha run reads "ice 1" forty-four globals running and never
    // anything else - so "climb to three" is a condition that cannot come true, and it sat
    // directly above the rule that leaves the phase.

    private static SnapshotBuilder UmbralIceOne(SnapshotBuilder builder, uint mp) =>
        builder.Gauge(s =>
        {
            s.Gauges.BlackMage.UmbralIce = 1;
            s.Mp = mp;
        });

    /// <summary>The reported symptom: stuck in ice on a full bar, for ever.</summary>
    [Fact]
    public void AFullBarLeavesIceEvenWhereTheThirdRungCannotBeReached()
    {
        var suggestion = Suggest(UmbralIceOne(Casting(18), 10000).Debuff(A.Thunder.Id, 25f));

        Assert.Equal(A.Fire1.Id, suggestion);
    }

    /// <summary>The AoE button was stuck in exactly the same place.</summary>
    [Fact]
    public void TheAoeButtonLeavesIceTooOnAFullBar()
    {
        var suggestion = Suggest(
            UmbralIceOne(Casting(18).Enemies(3), 10000).Debuff(A.ThunderII.Id, 25f),
            mode: RotationMode.Aoe);

        Assert.Equal(A.Fire2.Id, suggestion);
    }

    /// <summary>With the bar still empty it stays in ice, which is what ice is for.</summary>
    [Fact]
    public void AnEmptyBarStaysInIceAtTheCeiling()
    {
        var suggestion = Suggest(UmbralIceOne(Casting(18), 3000).Debuff(A.Thunder.Id, 25f));

        Assert.Equal(A.Blizzard1.Id, suggestion);
    }

    /// <summary>
    /// And at full level the climb still happens first: the bar is spent on the way in, so
    /// the third rung is always reached long before the mana that would end the phase.
    /// </summary>
    [Fact]
    public void TheClimbStillComesFirstAtFullLevel()
    {
        var suggestion = Suggest(UmbralIceOne(Casting(100), 3300).Debuff(A.HighThunderBuff.Id, 25f));

        Assert.Equal(A.Blizzard3.Id, suggestion);
    }

    /// <summary>Nor does a full bar cross back while the hearts are still owed.</summary>
    [Fact]
    public void AFullBarDoesNotLeaveIceWithTheHeartsStillOwed()
    {
        var suggestion = Suggest(UmbralIceOne(Casting(100), 10000).Debuff(A.HighThunderBuff.Id, 25f));

        Assert.NotEqual(A.Fire3.Id, suggestion);
    }
}
