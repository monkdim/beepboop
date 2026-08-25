using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.BlackMage;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.BlackMage.BlackMageActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// A caster who has to move is the case this plugin exists for. It was also the case it
/// handled worst.
/// <para>
/// Off-globals are only offered inside a weave window, which is right for damage - an
/// ability squeezed in beside a global costs nothing. But Swiftcast and Triplecast are not
/// there to add damage, they are there to unblock the global that follows, and the moment
/// they are wanted is the moment the global comes up and the next thing is a hard cast.
/// Behind the weave window they were unreachable at exactly that moment.
/// </para>
/// <para>
/// A recorded pull shows what that looked like: twelve Blizzard IIIs attempted in four
/// seconds while the player ran, every one of them interrupted, and Triplecast never once
/// suggested. Reported as "my abilities dont change to things that are better to cast while
/// moving".
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

    /// <summary>The global is up, which is the whole point: there is no weave window here.</summary>
    private static SnapshotBuilder Running() =>
        new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1).Moving();

    private static ActionRef Suggest(SnapshotBuilder builder) =>
        Session().Resolve(RotationMode.SingleTarget, builder.Build(), new FakeActionState()).Action;

    /// <summary>The bug, stated directly.</summary>
    [Fact]
    public void MovingWithNothingInstantAsksForTriplecastEvenWithTheGlobalUp()
    {
        Assert.Equal(A.Triplecast.Id, Suggest(Running()).Id);
    }

    /// <summary>Three instants beat one, so Swiftcast waits until Triplecast cannot cover it.</summary>
    [Fact]
    public void SwiftcastIsOnlyAskedForWhenTriplecastIsDown()
    {
        var session = Session();

        var suggestion = session.Resolve(
            RotationMode.SingleTarget,
            Running().Build(),
            new FakeActionState().OnCooldown(A.Triplecast.Id, 30f));

        Assert.Equal(A.Swiftcast.Id, suggestion.Action.Id);
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

    /// <summary>
    /// Standing still, nothing changes: an off-global that beats the global is a concession
    /// to movement, not a new way for off-globals to clip.
    /// </summary>
    [Fact]
    public void StandingStillTheGlobalIsStillTheAnswer()
    {
        var suggestion = Suggest(new SnapshotBuilder().Job(25).Gcd(0f).NoCombo().Enemies(1));

        Assert.NotEqual(A.Triplecast.Id, suggestion.Id);
        Assert.NotEqual(A.Swiftcast.Id, suggestion.Id);
    }

    /// <summary>
    /// And no other job gains the behaviour by accident - only rules that ask for it get it.
    /// Dragoon is entirely instant, so movement must change nothing at all there.
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
