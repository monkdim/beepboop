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
        BuildRotation(SingleTarget, false);
        BuildRotation(Aoe, true);
    }

    private void BuildRotation(RotationPlan plan, bool aoe)
    {
        var filler = aoe ? A.Outburst : A.Ruin1;
        plan.OGcd(A.SearingLight).When(c => c.HasTarget && !c.Downtime);
        plan.OGcd(A.SearingFlash).When(c => c.HasTarget && c.Buff(A.RubysGlimmer));
        plan.OGcd(A.Sunflare).When(c => c.HasTarget && Current(c, A.AstralFlow) == A.Sunflare);
        plan.OGcd(A.Deathflare).When(c => c.HasTarget && Current(c, A.AstralFlow) == A.Deathflare);
        plan.OGcd(A.MountainBuster).When(c => c.HasTarget && c.Buff(A.TitansFavor));
        plan.OGcd(c => Current(c, A.EnkindleBahamut))
            .When(c => c.HasTarget && c.Smn.SummonTimeRemaining > 0
                && !c.Smn.PrimalActive && c.Has(A.EnkindleBahamut));
        plan.OGcd(c => aoe && c.Has(A.Painflare) ? A.Painflare : c.Has(A.Necrotize) ? A.Necrotize : A.Fester)
            .When(c => c.HasTarget && c.Smn.AetherflowStacks > 0);
        plan.OGcd(c => aoe && c.Has(A.EnergySiphon) ? A.EnergySiphon : A.EnergyDrain)
            .When(c => c.HasTarget && c.Smn.AetherflowStacks == 0);

        plan.Gcd(A.SummonCarbuncle).When(c => !c.Smn.HasPet && c.Smn.SummonTimeRemaining <= 0 && !c.Smn.PrimalActive);
        plan.Gcd(A.CrimsonStrike).When(c => c.HasTarget && c.Buff(A.CrimsonStrikeReady));
        plan.Gcd(A.CrimsonCyclone).When(c => c.HasTarget && c.Buff(A.IfritsFavor));
        plan.Gcd(A.Slipstream).When(c => c.HasTarget && c.Buff(A.GarudasFavor));
        plan.Gcd(c => Current(c, aoe && c.Has(A.PreciousBrilliance) ? A.PreciousBrilliance : A.Gemshine))
            .When(c => c.HasTarget && c.Smn.PrimalActive);
        plan.Gcd(c => Current(c, filler))
            .When(c => c.HasTarget && c.Smn.SummonTimeRemaining > 0 && !c.Smn.PrimalActive);
        plan.Gcd(c => Current(c, A.Aethercharge))
            .When(c => c.HasTarget && c.Smn.SummonTimeRemaining <= 0);
        plan.Gcd(c => Current(c, A.SummonTopaz)).When(c => c.HasTarget && c.Smn.TitanReady && !c.Smn.PrimalActive);
        plan.Gcd(c => Current(c, A.SummonEmerald)).When(c => c.HasTarget && c.Smn.GarudaReady && !c.Smn.PrimalActive);
        plan.Gcd(c => Current(c, A.SummonRuby)).When(c => c.HasTarget && c.Smn.IfritReady && !c.Smn.PrimalActive);
        plan.Gcd(A.Ruin4).When(c => c.HasTarget && c.Buff(A.FurtherRuin));
        plan.Gcd(c => Current(c, c.Has(filler) ? filler : A.Ruin1));
    }
}
