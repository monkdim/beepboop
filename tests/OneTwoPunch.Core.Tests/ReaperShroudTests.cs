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
}
