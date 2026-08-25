using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Summoner.SummonerActions;

namespace OneTwoPunch.Core.Jobs.Summoner;

/// <summary>
/// Summoner, Dawntrail. A fixed loop of phases - the great wyrm, then the three primals -
/// where each phase replaces what the buttons do while it runs.
/// <para>
/// Most of that replacement is the game's own. Gemshine, Precious Brilliance, Astral Flow
/// and the Enkindle line are single actions that the game turns into the right elemental
/// version for whatever is currently summoned, so the rules name the generic action and let
/// it resolve. That is one less table here to fall out of step with a patch.
/// </para>
/// <para>
/// Summoner is almost entirely instant, which makes it one of the friendliest jobs in the
/// game to play from two keys.
/// </para>
/// </summary>
public sealed class SummonerRotation : JobRotationBase
{
    public override uint JobId => 27;

    public override string Name => "Summoner";

    public override ActionRef SingleTargetButton => A.Ruin1;

    public override ActionRef AoeButton => A.Outburst;

    public override float AoeRadius => 5f;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override StatusRef? BurstStatus => A.SearingLightBuff;

    public override ActionRef? BurstAction => A.SearingLight;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        p.OGcd(A.SearingLight).When(c => !c.Downtime).Because("raid buff");
        p.OGcd(A.SearingFlash).When(c => c.Buff(A.RubysGlimmer));

        // Whichever pet is out; the game only accepts the matching one.
        p.OGcd(A.EnkindleSolarBahamut).When(c => !c.Downtime);
        p.OGcd(A.EnkindleBahamut).When(c => !c.Downtime);
        p.OGcd(A.EnkindlePhoenix).When(c => !c.Downtime);

        // Astral Flow is Mountain Buster, Slipstream or Crimson Cyclone depending on the
        // primal - again resolved by the game rather than duplicated here.
        p.OGcd(A.AstralFlow)
            .When(c => c.Buff(A.TitansFavor) || c.Buff(A.GarudasFavor) || c.Buff(A.IfritsFavor));

        p.OGcd(A.MountainBuster).When(c => c.Buff(A.TitansFavor));
        p.OGcd(A.CrimsonStrike).When(c => c.Buff(A.CrimsonStrikeReady));

        // Aetherflow: Fester is the single-target spend and caps at two stacks.
        p.OGcd(c => c.Has(A.Necrotize) ? A.Necrotize : A.Fester)
            .When(c => c.Smn.AetherflowStacks > 0)
            .Because("spend Aetherflow");

        p.OGcd(A.EnergyDrain)
            .When(c => c.Smn.AetherflowStacks == 0 && !c.Downtime)
            .Because("refill Aetherflow");

        // ---- GCDs --------------------------------------------------------
        // While a primal is attuned the whole bar is its element.
        p.Gcd(A.Gemshine)
            .When(c => c.Smn.Attunement > 0)
            .Because("primal attunement");

        p.Gcd(A.Slipstream).When(c => c.Buff(A.SlipstreamBuff));

        // Great wyrm phase.
        p.Gcd(A.UmbralImpulse).When(c => !c.Downtime);
        p.Gcd(A.AstralImpulse).When(c => !c.Downtime);
        p.Gcd(A.FountainOfFire).When(c => !c.Downtime);

        p.Gcd(A.Sunflare).When(c => !c.Downtime);
        p.Gcd(A.Deathflare).When(c => !c.Downtime);

        // Summons, in the order the loop wants them. Each is only accepted at its point in
        // the cycle, so Ready() keeps the sequence without a phase counter here.
        p.Gcd(A.SummonSolarBahamut).When(c => !c.Downtime);
        p.Gcd(A.SummonBahamut).When(c => !c.Downtime);
        p.Gcd(A.SummonPhoenix).When(c => !c.Downtime);

        p.Gcd(c => c.Has(A.SummonIfrit2) ? A.SummonIfrit2 : A.SummonIfrit1).When(c => !c.Downtime);
        p.Gcd(c => c.Has(A.SummonTitan2) ? A.SummonTitan2 : A.SummonTitan1).When(c => !c.Downtime);
        p.Gcd(c => c.Has(A.SummonGaruda2) ? A.SummonGaruda2 : A.SummonGaruda1).When(c => !c.Downtime);

        p.Gcd(A.LuxSolaris).When(c => c.Buff(A.RefulgentLux));

        p.Gcd(A.Ruin4)
            .When(c => c.Buff(A.FurtherRuin))
            .Because("free and instant");

        p.Gcd(c => c.Has(A.Ruin3) ? A.Ruin3 : A.Ruin1);
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.SearingLight).When(c => !c.Downtime).Because("raid buff");
        p.OGcd(A.SearingFlash).When(c => c.Buff(A.RubysGlimmer));

        p.OGcd(A.EnkindleSolarBahamut).When(c => !c.Downtime);
        p.OGcd(A.EnkindleBahamut).When(c => !c.Downtime);
        p.OGcd(A.EnkindlePhoenix).When(c => !c.Downtime);

        p.OGcd(A.AstralFlow)
            .When(c => c.Buff(A.TitansFavor) || c.Buff(A.GarudasFavor) || c.Buff(A.IfritsFavor));

        p.OGcd(A.MountainBuster).When(c => c.Buff(A.TitansFavor));
        p.OGcd(A.CrimsonStrike).When(c => c.Buff(A.CrimsonStrikeReady));

        p.OGcd(A.Painflare)
            .When(c => c.Smn.AetherflowStacks > 0)
            .Because("spend Aetherflow");

        p.OGcd(A.EnergySiphon)
            .When(c => c.Smn.AetherflowStacks == 0 && !c.Downtime)
            .Because("refill Aetherflow");

        p.Gcd(A.PreciousBrilliance)
            .When(c => c.Smn.Attunement > 0)
            .Because("primal attunement");

        p.Gcd(A.Slipstream).When(c => c.Buff(A.SlipstreamBuff));

        p.Gcd(A.UmbralFlare).When(c => !c.Downtime);
        p.Gcd(A.AstralFlare).When(c => !c.Downtime);
        p.Gcd(A.BrandOfPurgatory).When(c => !c.Downtime);

        p.Gcd(A.Sunflare).When(c => !c.Downtime);
        p.Gcd(A.Deathflare).When(c => !c.Downtime);

        p.Gcd(A.SummonSolarBahamut).When(c => !c.Downtime);
        p.Gcd(A.SummonBahamut).When(c => !c.Downtime);
        p.Gcd(A.SummonPhoenix).When(c => !c.Downtime);

        p.Gcd(c => c.Has(A.SummonIfrit2) ? A.SummonIfrit2 : A.SummonIfrit1).When(c => !c.Downtime);
        p.Gcd(c => c.Has(A.SummonTitan2) ? A.SummonTitan2 : A.SummonTitan1).When(c => !c.Downtime);
        p.Gcd(c => c.Has(A.SummonGaruda2) ? A.SummonGaruda2 : A.SummonGaruda1).When(c => !c.Downtime);

        p.Gcd(A.LuxSolaris).When(c => c.Buff(A.RefulgentLux));
        p.Gcd(A.Ruin4).When(c => c.Buff(A.FurtherRuin)).Because("free and instant");

        p.Gcd(A.Outburst);
    }
}
