using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Beastmaster.BeastmasterActions;

namespace OneTwoPunch.Core.Jobs.Beastmaster;

/// <summary>
/// Beastmaster, Dawntrail 7.56. <b>Deliberately incomplete.</b>
/// <para>
/// This drives the ordinary weaponskill combo and nothing else. It exists so the job is
/// registered, because the plugin returns early on an unsupported job before the recorder
/// is ever started - so until Beastmaster is in the registry there is no way to capture a
/// log of anyone playing it, and no way to check any of the reasoning below against what
/// the game actually does.
/// </para>
/// <para>
/// <b>What is missing, and why.</b> The damage lives in the four instinctual skills, which
/// form a closed ring: Gale Axe (Volant) into Avalanche Axe (Rampant) into Mistral Axe
/// (Durant) into Spinning Axe (Eldritch) and back to Gale. Each grants the Heart that makes
/// the next one combo, and the official guide's "Inner Compass" lays out exactly that order,
/// clockwise. Walking the ring is the whole rotation.
/// </para>
/// <para>
/// Those four are not driven here because they do not fit the engine's model yet. They are
/// weaponskills on their own 5.0s cooldown group carrying neither the global's group 58 nor
/// the ability lock's group 71 - a third clock that is neither <c>Gcd</c> nor <c>OGcd</c>.
/// Guessing at it would put a wrong shape in a job people are meant to rely on. The engine
/// decision comes first, and a real log makes that decision on evidence.
/// </para>
/// <para>
/// Two consequences of the job's own rules are worth recording here for whoever writes the
/// rest. Beastmaster cannot execute role actions at all, so there is no Second Wind to hang
/// a self-heal rule on the way every other melee list here does. And it is played solo or in
/// pre-made parties, so the hold-for-party-buffs reasoning that shaped Viper has nothing to
/// line up with - spend on cooldown.
/// </para>
/// </summary>
public sealed class BeastmasterRotation : JobRotationBase
{
    public override uint JobId => 43;

    public override string Name => "Beastmaster";

    public override ActionRef SingleTargetButton => A.SmashAxe;

    /// <summary>
    /// Shield Charge rather than an AoE combo starter, because Beastmaster has no AoE
    /// weaponskill line: below 50 there is simply nothing, and at 50 the AoE arrives as the
    /// upgraded instinctuals, which this file does not drive yet. Shield Charge is the only
    /// unambiguously multi-target damage the job presses directly, so it is the honest host
    /// for the second key. The two buttons must be different actions in any case - the
    /// plugin keys its hotbar forms by action id, and a shared id would collapse them.
    /// </summary>
    public override ActionRef AoeButton => A.ShieldCharge;

    public override float AoeRadius => 6f;

    public override int AoeMinimumEnemies => 3;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    // No PositionalRescue: True North is a role action, and this job has none.
    // No BurstAction or BurstStatus: there is no raid buff to align to.
    // No Opener: nobody has published one, and the guide is explicit that guessing is worse
    // than having none.

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // Finishers first - first match wins, so the deepest live combo step has to be
        // tested before the step that feeds it.
        p.Gcd(A.Shieldsplitter)
            .When(c => c.ComboIs(A.AxebladeBite))
            .Because("600 potency and 15 TP on the combo");

        p.Gcd(A.AxebladeBite)
            .When(c => c.ComboIs(A.SmashAxe))
            .Because("500 potency and 13 TP on the combo");

        p.Gcd(A.SmashAxe);
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.ShieldCharge)
            .When(c => !c.Downtime && c.Enemies >= AoeMinimumEnemies)
            .Because("300 potency to the group, and it holds three charges");

        // Beastmaster has no AoE combo, so the single-target line is genuinely the right
        // answer at any number of targets. This is not a placeholder.
        p.Gcd(A.Shieldsplitter).When(c => c.ComboIs(A.AxebladeBite));
        p.Gcd(A.AxebladeBite).When(c => c.ComboIs(A.SmashAxe));
        p.Gcd(A.SmashAxe);
    }

    /// <summary>
    /// There is no Beastmaster job gauge - not in Dalamud's typed gauges, and not in the
    /// JobGaugeManager union in ClientStructs - so TP cannot be read the way Viper's coils
    /// can. The ring's position can be, though: the Heart the player is holding names the
    /// affinity that combos off it, which is the same thing as saying where they are on the
    /// Inner Compass. That plus the two compass halves is what a log needs to be readable.
    /// </summary>
    public override string? DescribeGauge(CombatSnapshot snapshot)
    {
        var heart = Holding(snapshot, A.VolantHeart) ? "volant"
            : Holding(snapshot, A.RampantHeart) ? "rampant"
            : Holding(snapshot, A.DurantHeart) ? "durant"
            : Holding(snapshot, A.EldritchHeart) ? "eldritch"
            : "none";

        // Named rather than derived from the Heart, so a log shows the ring being walked
        // out of order instead of quietly reporting what it should have been.
        var wants = Holding(snapshot, A.VolantHeart) ? " -> avalanche"
            : Holding(snapshot, A.RampantHeart) ? " -> mistral"
            : Holding(snapshot, A.DurantHeart) ? " -> spinning"
            : Holding(snapshot, A.EldritchHeart) ? " -> gale"
            : string.Empty;

        var compass = Holding(snapshot, A.Sunstrider) ? " | sunstrider" : string.Empty;
        compass += Holding(snapshot, A.Moonstalker) ? " | moonstalker" : string.Empty;

        var nature = Holding(snapshot, A.OneWithNature) ? " | one-with-nature" : string.Empty;
        var vantage = Holding(snapshot, A.LingeringVantage) ? " | vantage" : string.Empty;
        var wavering = Holding(snapshot, A.WaveringHeart) ? " | WAVERING" : string.Empty;

        return $"heart {heart}{wants}{compass}{nature}{vantage}{wavering}";
    }

    private static bool Holding(CombatSnapshot snapshot, StatusRef status)
    {
        for (var i = 0; i < snapshot.SelfStatuses.Count; i++)
        {
            if (snapshot.SelfStatuses[i].Id == status.Id)
                return true;
        }

        return false;
    }
}
