using TwoButton.Core.Engine;
using TwoButton.Core.Model;
using A = TwoButton.Core.Jobs.Bard.BardActions;

namespace TwoButton.Core.Jobs.Bard;

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

        // Songs. Their cooldowns already enforce the cycle, so taking whichever is up is
        // enough - and a song must always be running, since Repertoire only ticks under one.
        p.OGcd(A.WanderersMinuet).When(c => !c.Downtime);
        p.OGcd(A.MagesBallad).When(c => !c.Downtime);
        p.OGcd(A.ArmysPaeon).When(c => !c.Downtime);

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

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.RadiantFinale).When(c => !c.Downtime && c.Brd.CodaCount > 0);
        p.OGcd(A.BattleVoice).When(c => !c.Downtime);
        p.OGcd(A.RagingStrikes).When(c => !c.Downtime);

        p.OGcd(A.WanderersMinuet).When(c => !c.Downtime);
        p.OGcd(A.MagesBallad).When(c => !c.Downtime);
        p.OGcd(A.ArmysPaeon).When(c => !c.Downtime);

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
