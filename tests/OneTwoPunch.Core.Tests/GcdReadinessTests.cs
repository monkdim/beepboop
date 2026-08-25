using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// A global cooldown's cooldown <em>is</em> the shared global. Asking whether one is off
/// cooldown right now is therefore asking whether the global is up - and while it rolls the
/// answer is no for every global in the job.
/// <para>
/// That left no rule able to match for most of every global, so the button fell back to the
/// base attack; and because the game queues a press made during the recast, the base attack
/// is what got queued. Reported as "does Fire IV, then only casts Fire".
/// </para>
/// <para>
/// No test caught it because the fake never modelled the recast at all. These do.
/// </para>
/// </summary>
public sealed class GcdReadinessTests
{
    private static readonly ActionRef Weaponskill = new(9001, "Test Weaponskill", ActionKind.Gcd, 1);

    private static RotationContext Context(float gcdRemaining, float actionCooldown)
    {
        var snapshot = new SnapshotBuilder().Gcd(gcdRemaining).Build();
        var actions = new FakeActionState().OnCooldown(Weaponskill.Id, actionCooldown);

        return new RotationContext(
            snapshot, actions, new RotationSettings(), RotationMode.SingleTarget, 0);
    }

    /// <summary>The bug, stated directly.</summary>
    [Fact]
    public void AGlobalIsChosenWhileTheGlobalIsStillRolling()
    {
        // Two seconds left on the global, and this weaponskill is on exactly that recast.
        var context = Context(gcdRemaining: 2.0f, actionCooldown: 2.0f);

        Assert.False(context.Ready(Weaponskill), "not usable this instant, which is correct");
        Assert.True(context.Ready(Weaponskill, byNextGcd: true), "but it is what to press next");
    }

    /// <summary>
    /// The safety half. Ignoring the recast entirely would suggest things that are genuinely
    /// unavailable, so the allowance is only ever as long as the wait already being served.
    /// </summary>
    [Fact]
    public void AnActionOnItsOwnLongCooldownIsStillNotChosen()
    {
        var context = Context(gcdRemaining: 2.0f, actionCooldown: 30f);

        Assert.False(context.Ready(Weaponskill));
        Assert.False(context.Ready(Weaponskill, byNextGcd: true));
    }

    [Fact]
    public void AReadyGlobalIsReadyEitherWay()
    {
        var context = Context(gcdRemaining: 0f, actionCooldown: 0f);

        Assert.True(context.Ready(Weaponskill));
        Assert.True(context.Ready(Weaponskill, byNextGcd: true));
    }
}
