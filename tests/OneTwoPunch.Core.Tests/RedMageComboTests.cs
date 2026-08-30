using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.RedMage.RedMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Red Mage's melee combo, which a recorded pull shows never getting past its first step.
/// <para>
/// Three Enchanted Ripostes back to back at 03:02, 03:04 and 03:05, then Verholy - which
/// needs three mana stacks and had them, because three Ripostes is three stacks. Every one
/// of those globals should have been the next step of the chain, and the spells that took
/// the globals afterwards did so only because the chain had been abandoned rather than
/// finished. Reported as "we keep interrupting our melee burst to cast spells".
/// </para>
/// <para>
/// Enchanted Riposte and Riposte are different action ids, 7527 and 7504, and so are the two
/// Zwerchhaus and the two Redoublements. The list asked only about the enchanted ones. Which
/// the game writes into its combo tracker cannot be settled from here and does not need to
/// be: the pair are one position in one chain, so both are accepted.
/// </para>
/// </summary>
public sealed class RedMageComboTests
{
    private static FakeActionState Melee() =>
        new FakeActionState()
            .Unusable(A.Resolution.Id)
            .Unusable(A.Scorch.Id);

    /// <summary>In melee range with both colours full, which is where the combo is spent.</summary>
    private static SnapshotBuilder InTheCombo(byte stacks = 1) =>
        new SnapshotBuilder().Job(35).Level(100).Gcd(0f).Enemies(1)
            .Gauge(s =>
            {
                s.Gauges.RedMage.WhiteMana = 100;
                s.Gauges.RedMage.BlackMana = 100;
                s.Gauges.RedMage.ManaStacks = stacks;
            });

    private static uint Suggest(SnapshotBuilder builder)
    {
        var state = Melee();
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

    /// <summary>
    /// The reported bug. Both colours are full, so the rule that starts the combo would match
    /// too - and did, over and over. The live chain has to win.
    /// </summary>
    [Fact]
    public void AStartedComboIsAdvancedRatherThanStartedAgain()
    {
        var packet = InTheCombo().Combo(A.EnchantedRiposte.Id);

        Assert.Equal(A.EnchantedZwerchhau.Id, Suggest(packet));
    }

    /// <summary>And from the unenchanted spelling of the same position.</summary>
    [Fact]
    public void ThePlainRiposteIsTheSameStepOfTheSameChain()
    {
        var packet = InTheCombo().Combo(A.Riposte.Id);

        Assert.Equal(A.EnchantedZwerchhau.Id, Suggest(packet));
    }

    /// <summary>The last step, from either spelling of the second.</summary>
    [Fact]
    public void TheComboFinishesFromEitherZwerchhau()
    {
        Assert.Equal(
            A.EnchantedRedoublement.Id,
            Suggest(InTheCombo(stacks: 2).Combo(A.EnchantedZwerchhau.Id)));

        Assert.Equal(
            A.EnchantedRedoublement.Id,
            Suggest(InTheCombo(stacks: 2).Combo(A.Zwerchhau.Id)));
    }

    /// <summary>
    /// With no combo live and both colours full, starting one is right - this is the rule that
    /// was firing every global, and it still has to fire on the global it belongs to.
    /// </summary>
    [Fact]
    public void WithNoComboLiveTheChainStarts()
    {
        Assert.Equal(A.EnchantedRiposte.Id, Suggest(InTheCombo(stacks: 0).NoCombo()));
    }

    /// <summary>
    /// A combo that has timed out is not a combo. Nothing should advance off a dead chain.
    /// </summary>
    [Fact]
    public void AnExpiredComboStartsFromTheTop()
    {
        var packet = InTheCombo(stacks: 0).Combo(A.EnchantedRiposte.Id, timeLeft: 0f);

        Assert.Equal(A.EnchantedRiposte.Id, Suggest(packet));
    }

    /// <summary>
    /// Three mana stacks is the finisher, and it only arrives once the chain has actually run
    /// - which is the point. Three Enchanted Ripostes reached it too, and that is the shape of
    /// the bug rather than the rotation.
    /// </summary>
    [Fact]
    public void ThreeStacksIsTheFinisherRatherThanAFourthMeleeGlobal()
    {
        var packet = InTheCombo(stacks: 3).Combo(A.EnchantedRedoublement.Id);

        Assert.Equal(A.Verholy.Id, Suggest(packet));
    }

    /// <summary>
    /// Below fifty in the lower colour the chain must not be started - it cannot be finished,
    /// and a dropped combo is what this whole file is about.
    /// </summary>
    [Fact]
    public void TheChainIsNotStartedOnAGaugeThatCannotFinishIt()
    {
        var packet = new SnapshotBuilder().Job(35).Level(100).Gcd(0f).Enemies(1).NoCombo()
            .Gauge(s =>
            {
                s.Gauges.RedMage.WhiteMana = 40;
                s.Gauges.RedMage.BlackMana = 40;
            });

        Assert.NotEqual(A.EnchantedRiposte.Id, Suggest(packet));
    }

    // ---- Magicked Swordplay, which pays for the whole chain ---------------

    /// <summary>
    /// The state a recorded pull sat in for thirty seconds. Manafication had just been pressed
    /// at white 14 black 8 - which is exactly when its free combo is worth most - and the
    /// gauge test refused the combo until the buff expired unused. Three free globals gone.
    /// </summary>
    [Fact]
    public void MagickedSwordplayStartsTheChainOnAnEmptyGauge()
    {
        var packet = new SnapshotBuilder().Job(35).Level(100).Gcd(0f).Enemies(1).NoCombo()
            .Buff(A.MagickedSwordplay.Id, 29f)
            .Gauge(s =>
            {
                s.Gauges.RedMage.WhiteMana = 14;
                s.Gauges.RedMage.BlackMana = 8;
            });

        Assert.Equal(A.EnchantedRiposte.Id, Suggest(packet));
    }

    /// <summary>
    /// And the chain still runs to its end on a gauge that could never have paid for it. The
    /// buff covers all three globals, which is why it comes in threes.
    /// </summary>
    [Fact]
    public void TheFreeChainRunsToItsEnd()
    {
        var mid = new SnapshotBuilder().Job(35).Level(100).Gcd(0f).Enemies(1)
            .Buff(A.MagickedSwordplay.Id, 26f)
            .Combo(A.EnchantedRiposte.Id)
            .Gauge(s =>
            {
                s.Gauges.RedMage.WhiteMana = 14;
                s.Gauges.RedMage.BlackMana = 8;
                s.Gauges.RedMage.ManaStacks = 1;
            });

        Assert.Equal(A.EnchantedZwerchhau.Id, Suggest(mid));
    }

    /// <summary>
    /// Without the buff an empty gauge still cannot start the chain - the gauge test is not
    /// being removed, only given the second way of being satisfied that it always needed.
    /// </summary>
    [Fact]
    public void AnEmptyGaugeWithoutTheBuffStillWaits()
    {
        var packet = new SnapshotBuilder().Job(35).Level(100).Gcd(0f).Enemies(1).NoCombo()
            .Gauge(s =>
            {
                s.Gauges.RedMage.WhiteMana = 14;
                s.Gauges.RedMage.BlackMana = 8;
            });

        Assert.NotEqual(A.EnchantedRiposte.Id, Suggest(packet));
    }
}
