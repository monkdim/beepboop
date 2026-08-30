using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Which Blitz a Perfect Balance window is building towards, which the list never asked.
/// <para>
/// It banked Opo-opo three times, every window, for ever. Three matching chakra is an Elixir
/// Burst, which lights the Lunar Nadi - and lighting a Nadi that is already lit does nothing.
/// Phantom Rush needs both, and only a Blitz of three different chakra lights Solar, so the
/// rotation got exactly one Phantom Rush per fight: the one its scripted opener set up.
/// </para>
/// <para>
/// A recorded pull shows it exactly. Rising Phoenix and Elixir Burst in the opener, Phantom
/// Rush at 00:49, then Perfect Balance at 01:23 and again at 01:59 - both spent on Leaping
/// Opo, Dragon Kick, Leaping Opo, both ending in an Elixir Burst, both with Lunar already lit.
/// Two windows spent lighting a lamp that was on. NadiFlags was in the gauge and read nowhere.
/// </para>
/// </summary>
public sealed class MonkNadiTests
{
    private const byte Lunar = 1;
    private const byte Solar = 2;

    private static uint Suggest(SnapshotBuilder builder)
    {
        var state = new FakeActionState()
            .OnCooldown(A.Brotherhood.Id, 120f)
            .OnCooldown(A.RiddleOfFire.Id, 60f)
            .OnCooldown(A.RiddleOfWind.Id, 60f)
            .OnCooldown(A.PerfectBalance.Id, 60f)
            .OnCooldown(A.ForbiddenChakra.Id, 60f)
            .OnCooldown(A.SecondWind.Id, 120f);

        return new RotationSession(JobRotationBase.Create<MonkRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f })
            .Resolve(RotationMode.SingleTarget, builder.Build(), state).Action.Id;
    }

    /// <summary>Inside a Perfect Balance window, with the Nadi and open chakra stated.</summary>
    private static SnapshotBuilder InPerfectBalance(
        byte nadi, bool opo = false, bool raptor = false, bool coeurl = false) =>
        new SnapshotBuilder()
            .Job(20).Level(100).Gcd(0f).NoCombo().Enemies(1)
            .Buff(A.PerfectBalanceBuff.Id, 18f)
            .Gauge(s =>
            {
                // Set through the snapshot rather than handed a MonkGauge: it is a struct, so
                // anything taking one by value would mutate a copy and change nothing.
                s.Gauges.Monk.NadiFlags = nadi;
                s.Gauges.Monk.HasOpoChakra = opo;
                s.Gauges.Monk.HasRaptorChakra = raptor;
                s.Gauges.Monk.HasCoeurlChakra = coeurl;
            });

    /// <summary>
    /// The defect. Lunar is lit, Solar is not, so a matching Blitz is worth nothing - the
    /// window has to build one of each instead, and Opo-opo is where that starts.
    /// </summary>
    [Fact]
    public void WithLunarLitTheWindowBuildsTowardsSolar()
    {
        Assert.Equal(A.DragonKick.Id, Suggest(InPerfectBalance(Lunar)));
    }

    /// <summary>Then the Raptor chakra, because Opo-opo is already banked.</summary>
    [Fact]
    public void TheSolarWindowTakesEachChakraInTurn()
    {
        Assert.Equal(
            A.TwinSnakes.Id,
            Suggest(InPerfectBalance(Lunar, opo: true)));

        Assert.Equal(
            A.Demolish.Id,
            Suggest(InPerfectBalance(Lunar, opo: true, raptor: true)));
    }

    /// <summary>
    /// Asked of the gauge rather than counted, so a global pressed by hand mid-window does not
    /// put the rest of it out of step - here Raptor was opened first and Opo-opo is still due.
    /// </summary>
    [Fact]
    public void AWindowOutOfOrderStillFillsWhatIsMissing()
    {
        Assert.Equal(
            A.DragonKick.Id,
            Suggest(InPerfectBalance(Lunar, raptor: true)));
    }

    /// <summary>
    /// With neither Nadi lit there is nothing to pair with, so Opo-opo - the hardest hitting
    /// build - is right, and it lights the Lunar half.
    /// </summary>
    [Fact]
    public void WithNoNadiLitTheWindowBuildsOpoOpo()
    {
        Assert.Equal(A.DragonKick.Id, Suggest(InPerfectBalance(0)));
    }

    /// <summary>And with both lit the next Blitz is Phantom Rush regardless, so Opo-opo again.</summary>
    [Fact]
    public void WithBothNadiLitTheWindowBuildsOpoOpo()
    {
        Assert.Equal(
            A.DragonKick.Id,
            Suggest(InPerfectBalance((byte)(Lunar | Solar), raptor: true)));
    }

    /// <summary>
    /// The Opo-opo fury is still spent when it is there rather than rebuilt, in either kind of
    /// window - that part was right and stays.
    /// </summary>
    [Fact]
    public void TheOpoOpoFuryIsStillSpentWhenItIsUp()
    {
        var packet = InPerfectBalance(0).Gauge(s => s.Gauges.Monk.OpoOpoFury = 1);

        Assert.Equal(A.LeapingOpo.Id, Suggest(packet));
    }

    /// <summary>
    /// Below Rising Phoenix's level there are no Nadi to pair and the flags read zero for
    /// ever, so the window builds Opo-opo - the rotation those levels actually have. A rung
    /// that cannot be reached is how this engine has broken before.
    /// </summary>
    [Fact]
    public void BelowRisingPhoenixTheWindowIsAlwaysOpoOpo()
    {
        Assert.Equal(A.DragonKick.Id, Suggest(InPerfectBalance(Lunar).Level(60)));
    }
}
