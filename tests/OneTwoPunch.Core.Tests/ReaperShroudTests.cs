using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Reaper.ReaperActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Arcane Circle leaves Ideal Host behind, and Ideal Host is a free Enshroud: it does not
/// care what the Shroud gauge says.
/// <para>
/// The rule asked for fifty Shroud, so it missed the free one every single time. A recorded
/// pull has Ideal Host counting down from 29s to 3s completely untouched while the list
/// built Soul instead - a whole Enshroud of damage dropped once every two minutes. Reported
/// as "not using enshroud twice during the boost, using it once then holding and letting the
/// second fall off", which is exactly right.
/// </para>
/// </summary>
public sealed class ReaperShroudTests
{
    private static RotationSession Session() =>
        new(JobRegistry.Create(39)!, new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = 0f,
        });

    /// <summary>A weave window, since Enshroud is an off-global.</summary>
    private static SnapshotBuilder InAWeaveWindow() =>
        new SnapshotBuilder().Job(39).Gcd(2.2f).NoCombo().Enemies(1);

    /// <summary>
    /// Arcane Circle is the first off-global in the list and would win every one of these,
    /// so it is put on cooldown - these tests are about what happens after it, in the window
    /// it leaves behind.
    /// </summary>
    private static uint Suggest(SnapshotBuilder builder) =>
        Session()
            .Resolve(
                RotationMode.SingleTarget,
                builder.Build(),
                new FakeActionState().OnCooldown(A.ArcaneCircle.Id, 60f))
            .Action.Id;

    /// <summary>The bug, stated directly.</summary>
    [Fact]
    public void AFreeEnshroudIsTakenEvenWithAnEmptyGauge()
    {
        var suggestion = Suggest(
            InAWeaveWindow()
                .Buff(A.IdealHost.Id, 25f)
                .Gauge(s => s.Gauges.Reaper.Shroud = 0));

        Assert.Equal(A.Enshroud.Id, suggestion);
    }

    [Fact]
    public void AFullShroudGaugeStillEnshroudsWithoutTheFreeOne()
    {
        var suggestion = Suggest(InAWeaveWindow().Gauge(s => s.Gauges.Reaper.Shroud = 50));

        Assert.Equal(A.Enshroud.Id, suggestion);
    }

    /// <summary>
    /// And neither case fires while already shrouded, or holding a Soul Reaver that has to
    /// be spent first.
    /// </summary>
    [Fact]
    public void AlreadyShroudedIsNotAReasonToEnshroudAgain()
    {
        var suggestion = Suggest(
            InAWeaveWindow()
                .Buff(A.IdealHost.Id, 25f)
                .Gauge(s =>
                {
                    s.Gauges.Reaper.Shroud = 100;

                    // Enshrouded is derived from the timer, so this is how you are in it.
                    s.Gauges.Reaper.EnshroudTimeRemaining = 25f;
                }));

        Assert.NotEqual(A.Enshroud.Id, suggestion);
    }

    [Fact]
    public void AHeldSoulReaverIsSpentBeforeEnshrouding()
    {
        var suggestion = Suggest(
            InAWeaveWindow()
                .Buff(A.IdealHost.Id, 25f)
                .Buff(A.SoulReaver.Id, 20f)
                .Gauge(s => s.Gauges.Reaper.Shroud = 100));

        Assert.NotEqual(A.Enshroud.Id, suggestion);
    }

    /// <summary>
    /// The two-minute window wants two Enshrouds: the free one Arcane Circle leaves behind
    /// and a paid one out of banked Shroud. Spending at fifty the moment it is reached means
    /// the gauge is never near a hundred when the burst arrives - a recorded pull has
    /// Enshroud at 00:50 and 01:43, both well outside a buff window, and then only the free
    /// one inside each burst.
    /// </summary>
    [Fact]
    public void ShroudIsSavedWhenTheBurstIsClose()
    {
        var suggestion = Session()
            .Resolve(
                RotationMode.SingleTarget,
                InAWeaveWindow().Gauge(s => s.Gauges.Reaper.Shroud = 50).Build(),
                new FakeActionState().OnCooldown(A.ArcaneCircle.Id, 10f))
            .Action.Id;

        Assert.NotEqual(A.Enshroud.Id, suggestion);
    }

    /// <summary>With the burst a long way off there is nothing to save it for.</summary>
    [Fact]
    public void ShroudIsSpentWhenTheBurstIsNotClose()
    {
        var suggestion = Session()
            .Resolve(
                RotationMode.SingleTarget,
                InAWeaveWindow().Gauge(s => s.Gauges.Reaper.Shroud = 50).Build(),
                new FakeActionState().OnCooldown(A.ArcaneCircle.Id, 90f))
            .Action.Id;

        Assert.Equal(A.Enshroud.Id, suggestion);
    }

    /// <summary>
    /// And never saved past the cap. At a hundred Shroud every further Reaver spend is
    /// thrown away, which costs more than a badly timed Enshroud.
    /// </summary>
    [Fact]
    public void AFullGaugeIsSpentEvenWithTheBurstClose()
    {
        var suggestion = Session()
            .Resolve(
                RotationMode.SingleTarget,
                InAWeaveWindow().Gauge(s => s.Gauges.Reaper.Shroud = 100).Build(),
                new FakeActionState().OnCooldown(A.ArcaneCircle.Id, 10f))
            .Action.Id;

        Assert.Equal(A.Enshroud.Id, suggestion);
    }

    /// <summary>
    /// The hole the cap release leaves, and the reason for it: Plentiful Harvest's own fifty
    /// Shroud is what pushes the gauge to a hundred, so the release above would fire a paid
    /// Enshroud on exactly the global the free one was meant for. Enshroud's fifteen second
    /// cooldown then swallows Ideal Host for most of the buff window.
    /// </summary>
    [Fact]
    public void AFullGaugeIsStillHeldWhilePlentifulHarvestIsWaiting()
    {
        var suggestion = Session()
            .Resolve(
                RotationMode.SingleTarget,
                InAWeaveWindow()
                    .Buff(A.ArcaneCircleBuff.Id, 14f)
                    .Buff(A.ImmortalSacrifice.Id, 26f)
                    .Gauge(s => s.Gauges.Reaper.Shroud = 100)
                    .Build(),
                new FakeActionState().OnCooldown(A.ArcaneCircle.Id, 112f))
            .Action.Id;

        Assert.NotEqual(A.Enshroud.Id, suggestion);
    }

    /// <summary>
    /// And the hold lets go the moment the free one is actually in hand, rather than waiting
    /// for the stacks to time out.
    /// </summary>
    [Fact]
    public void TheHoldEndsOnceIdealHostIsUp()
    {
        var suggestion = Session()
            .Resolve(
                RotationMode.SingleTarget,
                InAWeaveWindow()
                    .Buff(A.ArcaneCircleBuff.Id, 12f)
                    .Buff(A.ImmortalSacrifice.Id, 24f)
                    .Buff(A.IdealHost.Id, 29f)
                    .Gauge(s => s.Gauges.Reaper.Shroud = 100)
                    .Build(),
                new FakeActionState().OnCooldown(A.ArcaneCircle.Id, 112f))
            .Action.Id;

        Assert.Equal(A.Enshroud.Id, suggestion);
    }

    /// <summary>Inside the buff, saving is over - that is what it was saved for.</summary>
    [Fact]
    public void ShroudIsSpentInsideTheBurst()
    {
        var suggestion = Session()
            .Resolve(
                RotationMode.SingleTarget,
                InAWeaveWindow()
                    .Buff(A.ArcaneCircleBuff.Id, 8f)
                    .Gauge(s => s.Gauges.Reaper.Shroud = 50)
                    .Build(),
                new FakeActionState().OnCooldown(A.ArcaneCircle.Id, 112f))
            .Action.Id;

        Assert.Equal(A.Enshroud.Id, suggestion);
    }
}
