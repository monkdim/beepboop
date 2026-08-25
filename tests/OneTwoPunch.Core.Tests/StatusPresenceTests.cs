using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.BlackMage;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// A buff you are holding is a buff you are holding, whatever the game says about how long
/// is left on it.
/// <para>
/// <c>Buff</c> used to be <c>BuffTime(status) &gt; 0</c>, which is the same question right
/// up until the game reports a status with no time on it - and it does. A recorded Black
/// Mage pull had Thunderhead on the player for seventy seconds straight, every single frame
/// reporting zero seconds remaining, while Ley Lines and Circle of Power beside it counted
/// down perfectly normally. Both Thunder rules are gated on <c>Buff(Thunderhead)</c>, so
/// Thunder was never suggested once in the entire fight. Reported as "why do we never use
/// thunder? thats a huge chunk of damage".
/// </para>
/// </summary>
public sealed class StatusPresenceTests
{
    private static readonly StatusRef Held = new(9101, "Test Buff");
    private static readonly StatusRef OnTarget = new(9102, "Test Debuff");

    private static RotationContext Context(CombatSnapshot snapshot) =>
        new(snapshot, new FakeActionState(), new RotationSettings(), RotationMode.SingleTarget, 0);

    /// <summary>The bug, stated directly.</summary>
    [Fact]
    public void ABuffTheGameReportsWithNoTimeLeftIsStillHeld()
    {
        var context = Context(new SnapshotBuilder().Buff(Held.Id, remaining: 0f).Build());

        Assert.True(context.Buff(Held), "a status in the list is on you, whatever its timer says");
    }

    [Fact]
    public void ABuffThatIsNotThereIsNotHeld()
    {
        Assert.False(Context(new SnapshotBuilder().Build()).Buff(Held));
    }

    [Fact]
    public void ADebuffTheGameReportsWithNoTimeLeftIsStillOnTheTarget()
    {
        var context = Context(new SnapshotBuilder().Debuff(OnTarget.Id, remaining: 0f).Build());

        Assert.True(context.Debuff(OnTarget));
    }

    [Fact]
    public void ADebuffThatIsNotThereIsNotOnTheTarget()
    {
        Assert.False(Context(new SnapshotBuilder().Build()).Debuff(OnTarget));
    }

    /// <summary>
    /// Time left is a separate question and still answered by the clock, so the rules that
    /// genuinely need one - refreshing Fugetsu, keeping Power Surge up - are unaffected.
    /// </summary>
    [Fact]
    public void TimeLeftIsStillTimeLeft()
    {
        var context = Context(new SnapshotBuilder().Buff(Held.Id, remaining: 0f).Build());

        Assert.Equal(0f, context.BuffTime(Held));
    }

    /// <summary>
    /// The reported symptom, end to end: Thunderhead up with no time on it, the dot not on
    /// the target, and Thunder is what the button should be.
    /// </summary>
    [Fact]
    public void BlackMageCastsThunderOnAProcTheGameReportsWithNoTimeLeft()
    {
        var job = JobRegistry.Create(25)!;

        var snapshot = new SnapshotBuilder()
            .Job(25)
            .Gcd(0f)
            .NoCombo()
            .Enemies(1)
            .Buff(BlackMageActions.Thunderhead.Id, remaining: 0f)
            .Build();

        var session = new RotationSession(job, new RotationSettings { UseOpener = false });
        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(BlackMageActions.HighThunder.Id, suggestion.Action.Id);
    }
}
