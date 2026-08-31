using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Dragoon;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Dragoon.DragoonActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// An opener chart assumes every cooldown is up. Real pulls are not like that - a striking
/// dummy least of all, where the last attempt was thirty seconds ago and half the buttons
/// are still turning.
/// <para>
/// Two recorded pulls died on exactly that. Dragoon lost the whole opener at step three
/// because Lance Charge was still down from the previous attempt; Black Mage the same at
/// step four over Amplifier. Twenty-one scripted steps discarded for one missing weave, and
/// in both logs everything after that point came from the priority list.
/// </para>
/// </summary>
public sealed class OpenerResilienceTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<DragoonRotation>(),
            new RotationSettings { SuggestionHoldSeconds = 0f });

    private static CombatSnapshot AtPull(float gcdRemaining = 0.1f) =>
        new SnapshotBuilder()
            .Gcd(gcdRemaining)
            .NoCombo()
            .Gauge(s => s.CombatDuration = 0.5f)
            .Build();

    /// <summary>Walks the first two globals, leaving the opener on its Lance Charge step.</summary>
    private static void WalkToTheLanceChargeStep(RotationSession session, IActionState actions)
    {
        session.Resolve(RotationMode.SingleTarget, AtPull(), actions);
        session.NotifyActionUsed(A.TrueThrust.Id);
        session.Resolve(RotationMode.SingleTarget, AtPull(), actions);
        session.NotifyActionUsed(A.SpiralBlow.Id);
    }

    /// <summary>Walks the first four steps, leaving the opener on its Battle Litany step.</summary>
    private static void WalkToTheBattleLitanyStep(RotationSession session, IActionState actions)
    {
        foreach (var step in new[]
                 { A.TrueThrust, A.SpiralBlow, A.LanceCharge, A.ChaoticSpring })
        {
            session.Resolve(RotationMode.SingleTarget, AtPull(2.4f), actions);
            session.NotifyActionUsed(step.Id);
        }
    }

    /// <summary>
    /// The bug, stated directly.
    /// <para>
    /// Battle Litany rather than Lance Charge, which this used to use: Lance Charge is the
    /// job's burst, and an opener whose burst is down no longer starts at all - see
    /// <see cref="OpenerBurstReadinessTests"/>. That is a different rule about a different
    /// moment, and this one is still exactly what it was: a weave partway through the chart,
    /// left turning by the last pull, must not throw the rest of the chart away.
    /// </para>
    /// </summary>
    [Fact]
    public void AWeaveLeftOnCooldownFromTheLastPullIsSteppedOverRatherThanEndingTheOpener()
    {
        var session = Session();

        // Battle Litany is two minutes long and was pressed thirty seconds ago.
        var actions = new FakeActionState().OnCooldown(A.BattleLitany.Id, 30f);

        WalkToTheBattleLitanyStep(session, actions);

        // Wide open window, so nothing here is about the weave slot.
        var suggestion = session.Resolve(RotationMode.SingleTarget, AtPull(2.4f), actions);

        Assert.True(session.OpenerActive, "the opener gave up over one unavailable weave");
        Assert.Equal(A.Geirskogul.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The opposite case, which must still wait: an off-global that is off cooldown but not
    /// usable this instant is a proc that has not landed or a window that has not opened,
    /// and it is about to be available.
    /// </summary>
    [Fact]
    public void AWeaveThatIsOnlyMomentarilyUnavailableIsStillWaitedFor()
    {
        var session = Session();

        // Well inside a global: this is a moment, not a cooldown.
        var actions = new FakeActionState().OnCooldown(A.LanceCharge.Id, 1.0f);

        WalkToTheLanceChargeStep(session, actions);
        session.Resolve(RotationMode.SingleTarget, AtPull(), actions);

        Assert.True(session.OpenerActive);
        Assert.Equal(2, session.OpenerStep); // still pointing at Lance Charge
    }

    /// <summary>
    /// Which of two weaves in one window goes first does not matter, and pressing the next
    /// global while a weave is still waiting for a slot is not going off script either.
    /// </summary>
    [Fact]
    public void PressingTheNextGlobalWhileAWeaveIsPendingKeepsTheOpener()
    {
        var session = Session();
        var actions = new FakeActionState();

        WalkToTheLanceChargeStep(session, actions);

        // Lance Charge never happened; the player took the global instead.
        session.NotifyActionUsed(A.ChaoticSpring.Id);

        Assert.True(session.OpenerActive, "a skipped weave ended the opener");
        Assert.Equal(4, session.OpenerStep); // past Chaotic Spring, onto Battle Litany
    }

    [Fact]
    public void WeavesTakenInTheOtherOrderKeepTheOpener()
    {
        var session = Session();
        var actions = new FakeActionState();

        WalkToTheLanceChargeStep(session, actions);
        session.NotifyActionUsed(A.LanceCharge.Id);
        session.Resolve(RotationMode.SingleTarget, AtPull(), actions);
        session.NotifyActionUsed(A.ChaoticSpring.Id);

        // The chart draws Battle Litany then Geirskogul. Taking them the other way round is
        // the same window and the same two abilities.
        session.NotifyActionUsed(A.Geirskogul.Id);

        Assert.True(session.OpenerActive);
        Assert.Equal(A.WheelingThrust.Id, JobRegistry.Create(22)!.Opener!.Steps[session.OpenerStep].Id);
    }

    /// <summary>
    /// The forgiveness stops at the next global. A different global is a different rotation,
    /// and the opener has no business driving it.
    /// </summary>
    [Fact]
    public void AGlobalThatIsNotTheNextOneStillEndsTheOpener()
    {
        var session = Session();
        var actions = new FakeActionState();

        WalkToTheLanceChargeStep(session, actions);
        session.NotifyActionUsed(A.PiercingTalon.Id);

        Assert.False(session.OpenerActive);
    }

    // ---- Aborting over the plugin's own suggestion ------------------------

    /// <summary>
    /// The Red Mage failure, reproduced on Dragoon's machinery because it is the machinery
    /// that was wrong rather than the job.
    /// <para>
    /// A recorded pull ends "step 3 of 36, gave up: step 3 wanted Swiftcast, but Veraero III
    /// was used" - and Veraero III is what the button was showing at that moment, with the
    /// priority list's own reason printed beside it in the same log. The opener had stood
    /// down (an off-global step with no weave slot), the priority list answered instead, the
    /// player pressed the button, and the opener threw away thirty-three remaining steps over
    /// its own plugin's suggestion.
    /// </para>
    /// </summary>
    [Fact]
    public void PressingWhatTheButtonWasShowingDoesNotEndTheOpener()
    {
        var session = Session();
        var actions = new FakeActionState();

        WalkToTheLanceChargeStep(session, actions);

        // Whatever the button is offering right now - opener step or priority list, it does
        // not matter which, because the player can only press what they are shown.
        var shown = session.Resolve(RotationMode.SingleTarget, AtPull(), actions);
        session.NotifyActionUsed(shown.Action.Id);

        Assert.True(
            session.OpenerActive,
            $"the opener gave up over its own suggestion: {session.OpenerOutcome}");
    }

    /// <summary>
    /// And the guard is not a blanket amnesty. An action that was never suggested is a real
    /// divergence and still ends the opener - otherwise the sequence would drive on through
    /// a rotation it has nothing to do with.
    /// </summary>
    [Fact]
    public void AnActionThatWasNeverSuggestedStillEndsTheOpener()
    {
        var session = Session();
        var actions = new FakeActionState();

        WalkToTheLanceChargeStep(session, actions);
        session.Resolve(RotationMode.SingleTarget, AtPull(), actions);

        // Never offered by either button in this window.
        session.NotifyActionUsed(A.PiercingTalon.Id);

        Assert.False(session.OpenerActive, "a real divergence was allowed to carry on");
        Assert.NotNull(session.OpenerOutcome);
    }

    /// <summary>
    /// Not ending the opener is not the same as leaving it where it was, and for a long time
    /// it was not: the press was excused and then the step was sent back to one anyway.
    /// <para>
    /// Mid-pull that is worse than aborting, because the guard that refuses to start an
    /// opener late fires on the very next frame and the log blames the wrong thing entirely.
    /// A recorded Monk pull ran five steps cleanly, took one priority-list global while the
    /// opener stood down waiting for a weave slot, and reported "step 1 of 20, gave up: the
    /// fight was already 4.9s old at the first step" - fifteen scripted steps thrown away
    /// and the reason given for it never happened.
    /// </para>
    /// </summary>
    [Fact]
    public void PressingWhatTheButtonWasShowingDoesNotRewindTheOpenerToStepOne()
    {
        var session = Session();
        var actions = new FakeActionState();

        WalkToTheLanceChargeStep(session, actions);

        var shown = session.Resolve(RotationMode.SingleTarget, AtPull(), actions);
        session.NotifyActionUsed(shown.Action.Id);

        Assert.StartsWith(
            "step 3 of ",
            session.OpenerReport ?? string.Empty,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// And it picks up from there. The whole point of holding is that the weave slot opens a
    /// moment later and the scripted step is still the one on offer.
    /// </summary>
    [Fact]
    public void AfterHoldingTheOpenerCarriesOnFromTheStepItHeld()
    {
        var session = Session();
        var actions = new FakeActionState();

        WalkToTheLanceChargeStep(session, actions);

        // No weave slot, so the opener stands down and the priority list answers.
        var shown = session.Resolve(RotationMode.SingleTarget, AtPull(), actions);
        session.NotifyActionUsed(shown.Action.Id);

        // Slot open. The step it was holding, not the one it started at.
        var next = session.Resolve(RotationMode.SingleTarget, AtPull(2.4f), actions);

        Assert.Equal(A.LanceCharge.Id, next.Action.Id);
    }

    /// <summary>
    /// Before the pull the rewind is still right: somebody fidgeting on a dummy has not gone
    /// off script, they have moved the standing start, and the pre-pull walk begins again.
    /// </summary>
    [Fact]
    public void AStrayPressBeforeThePullStartsThePreOpenerWalkAgain()
    {
        var session = Session();
        var actions = new FakeActionState();

        CombatSnapshot BeforeThePull() =>
            new SnapshotBuilder().Gcd(0f).NoCombo().OutOfCombat().Build();

        // Two scripted globals, all of it before the pull.
        session.Resolve(RotationMode.SingleTarget, BeforeThePull(), actions);
        session.NotifyActionUsed(A.TrueThrust.Id);
        session.Resolve(RotationMode.SingleTarget, BeforeThePull(), actions);
        session.NotifyActionUsed(A.SpiralBlow.Id);

        session.NotifyActionUsed(A.PiercingTalon.Id);

        Assert.True(session.OpenerActive, "a pre-pull fidget ended the opener");
        Assert.Equal(
            A.TrueThrust.Id,
            session.Resolve(RotationMode.SingleTarget, BeforeThePull(), actions).Action.Id);
    }

    /// <summary>
    /// The set is what was on offer leading up to this press, not everything ever suggested -
    /// so a press that follows a different suggestion is still judged on its own window.
    /// </summary>
    [Fact]
    public void TheAmnestyIsClearedOnceAnActionHasGoneOff()
    {
        var session = Session();
        var actions = new FakeActionState();

        WalkToTheLanceChargeStep(session, actions);

        var shown = session.Resolve(RotationMode.SingleTarget, AtPull(), actions);
        session.NotifyActionUsed(shown.Action.Id);

        // No Resolve between the two presses, so nothing has been offered since - this one
        // has to be judged as the divergence it is.
        session.NotifyActionUsed(A.PiercingTalon.Id);

        Assert.False(session.OpenerActive, "a press with nothing on offer was excused");
    }
}
