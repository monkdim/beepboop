using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Bard.BardActions;

namespace OneTwoPunch.Core.Jobs.Bard;

/// <summary>
/// Bard, Dawntrail. Fully mobile, two damage-over-time effects that must never drop, and a
/// song cycle underneath everything. The songs rotate on their own cooldowns, so the rules
/// simply take whichever is available and let the cooldowns do the sequencing.
/// </summary>
public sealed class BardRotation : JobRotationBase
{
    public override uint JobId => 23;

    public override string Name => "Bard";

    public override ActionRef SingleTargetButton => A.HeavyShot;

    public override ActionRef AoeButton => A.QuickNock;

    public override float AoeRadius => 12f;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override StatusRef? BurstStatus => A.RagingStrikesBuff;

    public override ActionRef? BurstAction => A.RagingStrikes;

    /// <summary>
    /// The Balance's "Adjusted Standard Opener (2.48 GCD ideal)" for Bard level 100,
    /// Dawntrail patch 7.0. Heartbreak Shot is the pull, so the charge is not sitting
    /// overcapped while the two bleeds go up.
    /// </summary>
    private static readonly Opener Sequence = new(
        "The Balance adjusted standard", 100,
        A.HeartbreakShot,
        A.Stormbite, A.WanderersMinuet, A.EmpyrealArrow,
        A.CausticBite, A.BattleVoice,
        A.BurstShot, A.RadiantFinale, A.RagingStrikes,
        A.BurstShot, A.Barrage,
        A.RefulgentArrow, A.Sidewinder,
        A.RadiantEncore,
        A.ResonantArrow, A.EmpyrealArrow,
        A.RefulgentArrow,
        A.BurstShot,
        A.IronJaws,
        A.BurstShot, A.PitchPerfect)
    {
        // Drunk alongside Battle Voice, in Caustic Bite's weave window.
        PotionBeforeStep = 5,
    };

    public override Opener? Opener => Sequence;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // The two raid buffs, then the personal one they are meant to cover.
        p.OGcd(A.RadiantFinale)
            .When(c => !c.Downtime && c.Brd.CodaCount > 0)
            .Because("raid buff");

        p.OGcd(A.BattleVoice).When(c => !c.Downtime).Because("raid buff");
        p.OGcd(A.RagingStrikes).When(c => !c.Downtime).Because("burst window");

        // Songs, strongest first, and only at the hand-off point - see ReadyForTheNextSong.
        p.OGcd(A.WanderersMinuet).When(ReadyForTheNextSong).Because("next song");
        p.OGcd(A.MagesBallad).When(ReadyForTheNextSong).Because("next song");
        p.OGcd(A.ArmysPaeon).When(ReadyForTheNextSong).Because("next song");

        // Repertoire caps at three under Wanderer's Minuet and is lost on song change.
        p.OGcd(A.PitchPerfect)
            .When(c => c.Brd.Repertoire >= 3
                       || (c.Brd.Repertoire > 0 && c.Brd.SongTimeRemaining < 3f))
            .Because("Repertoire is about to be lost");

        p.OGcd(A.EmpyrealArrow).When(c => !c.Downtime);
        p.OGcd(A.Sidewinder).When(c => !c.Downtime);

        p.OGcd(A.Barrage)
            .When(c => !c.Downtime && !c.Buff(A.HawksEye))
            .Because("do not waste the proc we already have");

        // Bloodletter holds charges; spending down to one keeps a charge for the burst.
        p.OGcd(c => c.Has(A.HeartbreakShot) ? A.HeartbreakShot : A.Bloodletter)
            .When(c => !c.Downtime);

        // ---- GCDs --------------------------------------------------------
        // Iron Jaws refreshes both dots at once, so it beats reapplying either by hand.
        p.Gcd(A.IronJaws)
            .When(c => c.Has(A.IronJaws)
                       && !c.Downtime
                       && (c.DotExpiring(A.CausticBiteBuff, 4f) || c.DotExpiring(A.StormbiteBuff, 4f))
                       && c.Debuff(A.CausticBiteBuff)
                       && c.Debuff(A.StormbiteBuff))
            .Because("refresh both dots");

        p.Gcd(c => c.Has(A.CausticBite) ? A.CausticBite : A.VenomousBite)
            .When(c => !c.Downtime && !c.Debuff(A.CausticBiteBuff) && !c.Debuff(A.VenomousBiteBuff))
            .Because("apply the dot");

        p.Gcd(c => c.Has(A.Stormbite) ? A.Stormbite : A.Windbite)
            .When(c => !c.Downtime && !c.Debuff(A.StormbiteBuff) && !c.Debuff(A.WindbiteBuff))
            .Because("apply the dot");

        // Free follow-ups, which expire.
        p.Gcd(A.ResonantArrow).When(c => c.Buff(A.ResonantArrowReady));
        p.Gcd(A.RadiantEncore).When(c => c.Buff(A.RadiantEncoreReady));
        p.Gcd(A.BlastArrow).When(c => c.Buff(A.BlastArrowReady));

        // Apex Arrow wants a full gauge, and is held for the buff window when one is close.
        p.Gcd(A.ApexArrow)
            .When(c => c.Brd.SoulVoice >= 80 && !c.Downtime)
            .Because("Soul Voice is full");

        p.Gcd(A.RefulgentArrow)
            .When(c => c.Buff(A.HawksEye) || c.Buff(A.BarrageBuff))
            .Because("proc");

        p.Gcd(c => c.Has(A.BurstShot) ? A.BurstShot : A.HeavyShot);
    }

    /// <summary>
    /// The last repertoire proc chance is at three seconds, so that is where a song hands
    /// over. The Balance: "~43 seconds in Wanderer's Minuet, ~43 seconds in Mage's Ballad
    /// (leaving as the timer hits 3-2 in both cases)."
    /// <para>
    /// Army's Paeon is meant to be cut shorter still - "~34 seconds in Army's Paeon
    /// (leaving at 12-10 on the timer)" - because it is the weakest of the three. That one
    /// is not here yet: telling Paeon from the other two means knowing which song the
    /// gauge's SongId stands for, and the two readings available from here (Dalamud's enum
    /// member names, and the song status ids, of which the game's sheet carries several per
    /// song) are both guesses. DescribeGauge now prints the id, so the next recorded Bard
    /// pull settles it from real data instead.
    /// </para>
    /// </summary>
    private const float LeaveSongAt = 3f;

    /// <summary>
    /// Whether the song that is playing has reached the point where the next one takes
    /// over - and nothing else.
    /// <para>
    /// This used to be <c>!c.Downtime</c> on all three, on the reasoning that two minute
    /// cooldowns enforce the cycle by themselves. They do not. The three cooldowns run
    /// independently, so the moment two of them are up the list offers one, the player takes
    /// it, and the very next weave window it offers the other. A recorded pull is the whole
    /// case: Mage's Ballad at 00:20.3 and Army's Paeon at 00:20.9, six tenths of a second
    /// apart. Ballad was spent for one global, Paeon ran its course, and from 01:07 to 02:02
    /// the log shows no song at all - no Repertoire, no Soul Voice, no party buff, and no
    /// Pitch Perfect to spend - because all three were then turning at once.
    /// </para>
    /// <para>
    /// So the song that is up decides, not the cooldowns. Which one comes next needs no
    /// deciding: a song runs at most forty-five seconds against a two minute recast, so the
    /// one playing is always still on cooldown, and ordering the three strongest-first picks
    /// the right one out of what is left.
    /// </para>
    /// </summary>
    private static bool ReadyForTheNextSong(RotationContext c)
    {
        if (c.Downtime)
            return false;

        // Nothing playing: a song must always be running, so take whichever is offered.
        if (!c.Brd.AnySong)
            return true;

        return c.Brd.SongTimeRemaining <= LeaveSongAt;
    }

    /// <summary>
    /// What the recorded log prints for a Bard line. The song id is raw on purpose: which
    /// number is which song is the one thing the rules above still cannot tell, and a
    /// recorded pull naming it beside the song timer answers that without guessing.
    /// </summary>
    public override string? DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.Bard;

        var song = g.AnySong
            ? $"song {g.SongId} {g.SongTimeRemaining:0.#}s"
            : "no song";

        return $"{song} | repertoire {g.Repertoire} | soul {g.SoulVoice} | coda {g.CodaCount}";
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.RadiantFinale).When(c => !c.Downtime && c.Brd.CodaCount > 0);
        p.OGcd(A.BattleVoice).When(c => !c.Downtime);
        p.OGcd(A.RagingStrikes).When(c => !c.Downtime);

        p.OGcd(A.WanderersMinuet).When(ReadyForTheNextSong).Because("next song");
        p.OGcd(A.MagesBallad).When(ReadyForTheNextSong).Because("next song");
        p.OGcd(A.ArmysPaeon).When(ReadyForTheNextSong).Because("next song");

        p.OGcd(A.PitchPerfect)
            .When(c => c.Brd.Repertoire >= 3
                       || (c.Brd.Repertoire > 0 && c.Brd.SongTimeRemaining < 3f));

        p.OGcd(A.EmpyrealArrow).When(c => !c.Downtime);
        p.OGcd(A.Sidewinder).When(c => !c.Downtime);
        p.OGcd(A.RainOfDeath).When(c => !c.Downtime);
        p.OGcd(A.Barrage).When(c => !c.Downtime && !c.Buff(A.HawksEye));

        p.Gcd(A.ResonantArrow).When(c => c.Buff(A.ResonantArrowReady));
        p.Gcd(A.RadiantEncore).When(c => c.Buff(A.RadiantEncoreReady));
        p.Gcd(A.BlastArrow).When(c => c.Buff(A.BlastArrowReady));

        p.Gcd(A.ApexArrow).When(c => c.Brd.SoulVoice >= 80 && !c.Downtime);

        p.Gcd(A.Shadowbite)
            .When(c => c.Buff(A.HawksEye) || c.Buff(A.BarrageBuff))
            .Because("proc");

        p.Gcd(c => c.Has(A.WideVolley) ? A.WideVolley : A.QuickNock)
            .When(c => c.Buff(A.HawksEye))
            .Because("proc");

        p.Gcd(c => c.Has(A.Ladonsbite) ? A.Ladonsbite : A.QuickNock);
    }
}
