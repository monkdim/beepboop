using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.BlackMage;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.BlackMage.BlackMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// A caster who has to move is the case this plugin exists for, and the answer is not to
/// suggest the instants.
/// <para>
/// Triplecast and Swiftcast were suggested for a while, and the mechanism worked. But a
/// button that sometimes becomes Triplecast competes with the key the player has bound to
/// Triplecast, and the loser of that race is a wasted charge. Reacting to movement is a
/// half-second decision, and a suggestion cannot beat a thumb that already knows - so they
/// went back on their own keys, by request.
/// </para>
/// <para>
/// What the rotation still owes them is to notice: once an instant is held the movement
/// rules stand down, so pressing one by hand keeps the list casting Fire IV rather than
/// spending it on Xenoglossy.
/// </para>
/// </summary>
public sealed class MovementTests
{
    private static RotationSession Session() =>
        new(JobRegistry.Create(25)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        });

    /// <summary>Moving, with the global up and nothing held that would make a cast instant.</summary>
    private static SnapshotBuilder Running() =>
        new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1).Moving();

    private static ActionRef Suggest(SnapshotBuilder builder) =>
        Session().Resolve(RotationMode.SingleTarget, builder.Build(), new FakeActionState()).Action;

    /// <summary>The player's own key, not ours.</summary>
    [Fact]
    public void TheInstantsAreNeverSuggested()
    {
        var suggestion = Suggest(Running());

        Assert.NotEqual(A.Triplecast.Id, suggestion.Id);
        Assert.NotEqual(A.Swiftcast.Id, suggestion.Id);
    }

    /// <summary>Not even with the global still rolling, where an off-global would normally fit.</summary>
    [Fact]
    public void NorInAWeaveWindowWhileMoving()
    {
        var suggestion = Suggest(Running().Gcd(2.2f));

        Assert.NotEqual(A.Triplecast.Id, suggestion.Id);
        Assert.NotEqual(A.Swiftcast.Id, suggestion.Id);
    }

    /// <summary>
    /// Once an instant is held the movement rules must stand down. Spending a Triplecast
    /// charge on Xenoglossy while Fire IV sits there is the opposite of the point.
    /// </summary>
    [Fact]
    public void WithTriplecastUpTheRotationCarriesOnInsteadOfDroppingToInstants()
    {
        var suggestion = Suggest(Running().Buff(A.TriplecastBuff.Id, 15f, 3));

        Assert.NotEqual(A.Triplecast.Id, suggestion.Id);
        Assert.NotEqual(A.Swiftcast.Id, suggestion.Id);
        Assert.NotEqual(A.Xenoglossy.Id, suggestion.Id);
    }

    [Fact]
    public void WithSwiftcastUpTheRotationCarriesOnToo()
    {
        var suggestion = Suggest(Running().Buff(A.SwiftcastBuff.Id, 8f));

        Assert.NotEqual(A.Triplecast.Id, suggestion.Id);
        Assert.NotEqual(A.Swiftcast.Id, suggestion.Id);
    }

    /// <summary>Standing still changes nothing either way.</summary>
    [Fact]
    public void StandingStillTheGlobalIsStillTheAnswer()
    {
        var suggestion = Suggest(new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1));

        Assert.NotEqual(A.Triplecast.Id, suggestion.Id);
        Assert.NotEqual(A.Swiftcast.Id, suggestion.Id);
    }

    /// <summary>
    /// Dragoon is entirely instant, so movement must change nothing at all there. This is
    /// the check that movement handling stayed a Black Mage concern.
    /// </summary>
    [Fact]
    public void AJobWithNoCastsIsUnaffectedByMoving()
    {
        var job = JobRegistry.Create(22)!;
        var settings = new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f };
        var actions = new FakeActionState();

        var still = new RotationSession(job, settings).Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().Job(22).Gcd(0f).NoCombo().Enemies(1).Build(),
            actions);

        var moving = new RotationSession(job, settings).Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().Job(22).Gcd(0f).NoCombo().Enemies(1).Moving().Build(),
            actions);

        Assert.Equal(still.Action.Id, moving.Action.Id);
    }
}
