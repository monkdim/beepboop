using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Ninja;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Ninja.NinjaActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Two buttons cover almost every job, but not all. An extra button drives its own
/// sequence, so the global-cooldown rules that govern the main two must not apply to it.
/// </summary>
public sealed class ExtraButtonTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<NinjaRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f });

    [Fact]
    public void NinjaDeclaresAMudraButton()
    {
        var job = JobRotationBase.Create<NinjaRotation>();

        Assert.Single(job.ExtraButtons);
        Assert.Equal("Mudra", job.ExtraButtons[0].Name);
        Assert.NotEmpty(job.ExtraButtons[0].Purpose);
    }

    [Fact]
    public void TheMudraButtonFiresTheNinjutsuOnceItIsCharged()
    {
        var session = Session();
        var snapshot = new SnapshotBuilder().Gcd(0.1f).Build();

        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, new FakeActionState());

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheMudraButtonStartsASequenceWhenNoNinjutsuIsCharged()
    {
        var session = Session();

        // The game refuses Ninjutsu until enough mudras are charged, which is exactly the
        // signal the button reads.
        var actions = new FakeActionState().Unusable(A.Ninjutsu.Id);
        var snapshot = new SnapshotBuilder().Gcd(0.1f).Build();

        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, actions);

        Assert.Equal(A.Ten1.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheMudraButtonTakesTheSecondMudraOnceTheFirstIsSpent()
    {
        var session = Session();

        // Mid-sequence: the game no longer accepts the opening mudra ids.
        var actions = new FakeActionState()
            .Unusable(A.Ninjutsu.Id)
            .Unusable(A.Ten1.Id)
            .Unusable(A.Chi1.Id);

        var snapshot = new SnapshotBuilder().Gcd(0.1f).Build();
        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, actions);

        Assert.Equal(A.Chi2.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheMudraButtonTakesTheKassatsuBranch()
    {
        var session = Session();
        var actions = new FakeActionState()
            .Unusable(A.Ninjutsu.Id)
            .Unusable(A.Ten1.Id)
            .Unusable(A.Chi1.Id);

        var snapshot = new SnapshotBuilder().Gcd(0.1f).Buff(A.KassatsuBuff, 10f).Build();
        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, actions);

        Assert.Equal(A.Jin2.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The whole point of the flat resolution path: mudras are pressed back to back and do
    /// not roll the global cooldown, so a closed weave window must not silence the button.
    /// </summary>
    [Fact]
    public void TheMudraButtonIgnoresTheWeaveWindow()
    {
        var session = Session();
        var actions = new FakeActionState().Unusable(A.Ninjutsu.Id);

        // No room at all to weave.
        var snapshot = new SnapshotBuilder().Gcd(0.2f).AnimationLock(0.4f).Build();
        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, actions);

        Assert.Equal(A.Ten1.Id, suggestion.Action.Id);
    }

    [Fact]
    public void JobsWithoutExtraButtonsFallBackHarmlessly()
    {
        var job = JobRotationBase.Create<OneTwoPunch.Core.Jobs.Dragoon.DragoonRotation>();
        var session = new RotationSession(job, new RotationSettings { UseOpener = false });

        var suggestion = session.Resolve(
            RotationMode.Extra1, new SnapshotBuilder().Gcd(0.1f).NoCombo().Build(), new FakeActionState());

        // Falls through to the single-target button rather than throwing or going silent.
        Assert.NotNull(suggestion);
        Assert.True(suggestion.Action.Id > 0);
    }
}
