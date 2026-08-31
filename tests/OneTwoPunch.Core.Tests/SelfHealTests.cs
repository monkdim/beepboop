using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The second rung of the self-heal, which the melee jobs did not have.
/// <para>
/// Second Wind is two minutes and a dungeon is a great deal longer than that, so once it had
/// gone the button had nothing left to offer. Two recorded Monk runs have the player reaching
/// past the plugin for Bloodbath seventeen times between them - ten in one twenty-seven
/// minute dungeon - which is exactly the attention this is meant to not need.
/// </para>
/// </summary>
public sealed class SelfHealTests
{
    private static uint Suggest(FakeActionState actions, float hp)
    {
        var snapshot = new SnapshotBuilder()
            .Job(20).Level(100).Gcd(1.3f).NoCombo().Enemies(1)
            .Gauge(s => s.PlayerHpFraction = hp)
            .Build();

        return new RotationSession(JobRotationBase.Create<MonkRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f })
            .Resolve(RotationMode.SingleTarget, snapshot, actions).Action.Id;
    }

    /// <summary>Nothing else is competing for the slot, so each test is only about the heal.</summary>
    private static FakeActionState Quiet() =>
        new FakeActionState()
            .OnCooldown(A.Brotherhood.Id, 90f)
            .OnCooldown(A.RiddleOfFire.Id, 45f)
            .OnCooldown(A.RiddleOfWind.Id, 45f)
            .OnCooldown(A.PerfectBalance.Id, 30f);

    /// <summary>Second Wind still comes first: it is the bigger heal and it is free.</summary>
    [Fact]
    public void SecondWindIsStillTheFirstAnswer()
    {
        Assert.Equal(A.SecondWind.Id, Suggest(Quiet(), hp: 0.4f));
    }

    /// <summary>The defect: with Second Wind spent, the button had nothing.</summary>
    [Fact]
    public void BloodbathAnswersOnceSecondWindIsSpent()
    {
        Assert.Equal(
            A.Bloodbath.Id,
            Suggest(Quiet().OnCooldown(A.SecondWind.Id, 90f), hp: 0.4f));
    }

    /// <summary>And neither is offered while you are fine.</summary>
    [Fact]
    public void NeitherIsOfferedAtFullHealth()
    {
        var suggestion = Suggest(Quiet(), hp: 1f);

        Assert.NotEqual(A.SecondWind.Id, suggestion);
        Assert.NotEqual(A.Bloodbath.Id, suggestion);
    }
}
