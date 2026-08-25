using TwoButton.Core.Engine;
using TwoButton.Core.Model;
using A = TwoButton.Core.Jobs.Dancer.DancerActions;

namespace TwoButton.Core.Jobs.Dancer;

/// <summary>
/// Dancer, Dawntrail. Fully mobile and almost entirely proc-driven, which makes it one of
/// the better fits for a two-key layout.
/// <para>
/// The dances need no extra button. A dance is four presses, the same shape of problem as
/// Ninja's mudras - but unlike mudras the gauge names the next step outright, so the main
/// buttons simply become it. Press the same key through the whole dance and it walks the
/// steps and then the finish.
/// </para>
/// </summary>
public sealed class DancerRotation : JobRotationBase
{
    public override uint JobId => 38;

    public override string Name => "Dancer";

    public override ActionRef SingleTargetButton => A.Cascade;

    public override ActionRef AoeButton => A.Windmill;

    public override float AoeRadius => 12f;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override StatusRef? BurstStatus => A.TechnicalFinishBuff;

    public override ActionRef? BurstAction => A.Devilment;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        p.OGcd(A.Devilment).When(c => !c.Downtime).Because("burst window");

        p.OGcd(A.Flourish)
            .When(c => !c.Downtime && !c.Buff(A.ThreefoldFanDance))
            .Because("do not overwrite the proc we have");

        p.OGcd(A.FanDanceIV).When(c => c.Buff(A.FourfoldFanDance));
        p.OGcd(A.FanDanceIII).When(c => c.Buff(A.ThreefoldFanDance));

        // Feathers cap at four; spend down only when close, so the burst still has them.
        p.OGcd(A.FanDance)
            .When(c => c.Dnc.Feathers >= 4 || (c.Dnc.Feathers > 0 && c.Buff(A.TechnicalFinishBuff)))
            .Because("feathers are close to capping");

        // ---- GCDs --------------------------------------------------------
        // A dance locks out everything else, so it comes first. The gauge names the next
        // step, so the button just becomes it.
        p.Gcd(c => StepAction(c))
            .When(c => c.Dnc.Dancing)
            .Because("dance step");

        // Steps done: the finish is what the button becomes next.
        p.Gcd(A.TechnicalFinish).When(c => c.Dnc.Dancing);
        p.Gcd(A.StandardFinish).When(c => c.Dnc.Dancing);

        // Free hits from the finishes, all of which expire.
        p.Gcd(A.Tillana).When(c => c.Buff(A.FlourishingFinish));
        p.Gcd(A.DanceOfTheDawn).When(c => c.Buff(A.DanceOfTheDawnReady));
        p.Gcd(A.StarfallDance).When(c => c.Buff(A.FlourishingStarfall));
        p.Gcd(A.LastDance).When(c => c.Buff(A.LastDanceReady));

        // Technical Step is the two-minute burst; Standard Step keeps its buff up between.
        p.Gcd(A.TechnicalStep).When(c => !c.Downtime);
        p.Gcd(A.FinishingMove).When(c => c.Buff(A.FinishingMoveReady) && !c.Downtime);
        p.Gcd(A.StandardStep).When(c => !c.Downtime);

        // Esprit caps at 100 and the burst generates a lot of it.
        p.Gcd(A.SaberDance)
            .When(c => c.Dnc.Esprit >= 50 && (c.Dnc.Esprit >= 85 || c.Buff(A.TechnicalFinishBuff)))
            .Because("Esprit is close to capping");

        // Procs before filler.
        p.Gcd(A.Fountainfall)
            .When(c => c.Buff(A.SilkenFlow) || c.Buff(A.FlourishingFlow))
            .Because("proc");

        p.Gcd(A.ReverseCascade)
            .When(c => c.Buff(A.SilkenSymmetry) || c.Buff(A.FlourishingSymmetry))
            .Because("proc");

        p.Gcd(A.Fountain).When(c => c.ComboIs(A.Cascade));
        p.Gcd(A.Cascade);
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.Devilment).When(c => !c.Downtime).Because("burst window");
        p.OGcd(A.Flourish).When(c => !c.Downtime && !c.Buff(A.ThreefoldFanDance));
        p.OGcd(A.FanDanceIV).When(c => c.Buff(A.FourfoldFanDance));
        p.OGcd(A.FanDanceIII).When(c => c.Buff(A.ThreefoldFanDance));

        p.OGcd(A.FanDanceII)
            .When(c => c.Dnc.Feathers >= 4 || (c.Dnc.Feathers > 0 && c.Buff(A.TechnicalFinishBuff)))
            .Because("feathers are close to capping");

        p.Gcd(c => StepAction(c)).When(c => c.Dnc.Dancing).Because("dance step");
        p.Gcd(A.TechnicalFinish).When(c => c.Dnc.Dancing);
        p.Gcd(A.StandardFinish).When(c => c.Dnc.Dancing);

        p.Gcd(A.Tillana).When(c => c.Buff(A.FlourishingFinish));
        p.Gcd(A.DanceOfTheDawn).When(c => c.Buff(A.DanceOfTheDawnReady));
        p.Gcd(A.StarfallDance).When(c => c.Buff(A.FlourishingStarfall));
        p.Gcd(A.LastDance).When(c => c.Buff(A.LastDanceReady));

        p.Gcd(A.TechnicalStep).When(c => !c.Downtime);
        p.Gcd(A.FinishingMove).When(c => c.Buff(A.FinishingMoveReady) && !c.Downtime);
        p.Gcd(A.StandardStep).When(c => !c.Downtime);

        p.Gcd(A.SaberDance)
            .When(c => c.Dnc.Esprit >= 50 && (c.Dnc.Esprit >= 85 || c.Buff(A.TechnicalFinishBuff)));

        p.Gcd(A.Bloodshower).When(c => c.Buff(A.SilkenFlow) || c.Buff(A.FlourishingFlow));
        p.Gcd(A.RisingWindmill).When(c => c.Buff(A.SilkenSymmetry) || c.Buff(A.FlourishingSymmetry));

        p.Gcd(A.Bladeshower).When(c => c.ComboIs(A.Windmill));
        p.Gcd(A.Windmill);
    }

    /// <summary>
    /// The step the dance currently wants. The gauge gives it as a raw action id, so this is
    /// a direct lookup rather than anything that could drift out of sync.
    /// </summary>
    private static ActionRef? StepAction(RotationContext c)
    {
        var next = c.Dnc.NextStep;

        if (next == A.Emboite.Id)
            return A.Emboite;

        if (next == A.Entrechat.Id)
            return A.Entrechat;

        if (next == A.Jete.Id)
            return A.Jete;

        if (next == A.Pirouette.Id)
            return A.Pirouette;

        return null;
    }
}
