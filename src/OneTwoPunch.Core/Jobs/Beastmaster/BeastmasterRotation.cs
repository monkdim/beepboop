using OneTwoPunch.Core.Engine;
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
/// <b>The third clock, settled.</b> The instinctuals are weaponskills on their own 5.0s
/// cooldown group carrying neither the global's group 58 nor the ability lock's group 71.
/// They do not roll the global and the global does not block them, which is what an
/// off-global is - so <c>OGcd</c> is not a compromise after all, it is the right shape. The
/// one thing it costs is weave budget they do not really consume, and at one instinctual per
/// two globals there is room to spare.
/// </para>
/// <para>
/// <b>Trick comes first.</b> An instinctual pressed after Trick is an <em>intentional</em>
/// combo and banks a stack of Mastered Instinct; the same skill pressed before Trick banks
/// nothing and leaves Rallying Cheer as the consolation. Three stacks is what Rally turns
/// into the full bar that arms the Lv50 finishers, so the order is the whole rotation, not a
/// refinement of it.
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

    /// <summary>
    /// The instinctuals are off-globals that do not really compete for the window - they run
    /// on their own 5.0s timer and carry no shared ability lock - so the job asks for the
    /// room to weave one alongside whatever else was due.
    /// </summary>
    public override WeaveStyle MinimumWeaveStyle => WeaveStyle.Double;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    /// <summary>
    /// Rally, on a 90 second cooldown from level 42, is Beastmaster's burst - not because it
    /// deals damage but because of what it arms. It converts stacks of Mastered Instinct into
    /// TP at 40 plus 70 a stack, which at three stacks is exactly 250: a full bar, and a full
    /// bar is what upgrades an instinctual into its 1,200 potency form. The job has no raid
    /// buff, so this is the periodic cooldown damage actually aligns to.
    /// <para>
    /// Still a marker only - nothing suggests it. The ring that banks the stacks is driven
    /// now, but what to do with a full bar is the Lv50 finishers, which arrive through the
    /// game's own ActionIndirection table and are not in the action list yet. The engine uses
    /// this to know when a potion is worth prompting for, which does not depend on a rule.
    /// </para>
    /// </summary>
    public override ActionRef? BurstAction => A.Rally;

    // No PositionalRescue: True North is a role action, and this job has none.
    // No BurstStatus: Rally leaves no buff behind, it just fills the gauge.
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

        AddInstinctualRing(p);

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

        // The instinctuals are the damage at any number of targets - the job has no area
        // weaponskill line at all - so the ring is walked here too.
        AddInstinctualRing(p);

        // Beastmaster has no AoE combo, so the single-target line is genuinely the right
        // answer at any number of targets. This is not a placeholder.
        p.Gcd(A.Shieldsplitter).When(c => c.ComboIs(A.AxebladeBite));
        p.Gcd(A.AxebladeBite).When(c => c.ComboIs(A.SmashAxe));
        p.Gcd(A.SmashAxe);
    }

    /// <summary>
    /// The instinctual ring, which is where the damage lives.
    /// <para>
    /// <b>Trick first.</b> Trick into an instinctual is an intentional combo and banks a
    /// stack of Mastered Instinct. The same instinctual pressed first banks nothing, so Trick
    /// sits above the ring rather than beside it: when both are available the button asks for
    /// Trick, and the instinctual follows in the next slot.
    /// </para>
    /// <para>
    /// <b>Then follow the Heart.</b> Each instinctual grants the Heart that makes the next
    /// one combo, and the Heart in hand names that next one outright - Volant wants
    /// Avalanche, Rampant wants Mistral, Durant wants Spinning, Eldritch wants Gale. That is
    /// the official guide's Inner Compass read clockwise, and it means the ring needs no
    /// state of ours: the buff <em>is</em> the position.
    /// </para>
    /// <para>
    /// <b>Opening it is the one guess.</b> With no Heart in hand any of the four is a legal
    /// start, and which one is best depends on the familiar - the community's reading is
    /// "the instinctual counter-clockwise from your pet", which is not something the engine
    /// can see. So the opener is ordered by level, highest first, which at least never offers
    /// a skill that has not been learned and never stalls a low-level player. If a log shows
    /// the familiar's affinity mattering, this is the rule to change.
    /// </para>
    /// </summary>
    private static void AddInstinctualRing(RotationPlan p)
    {
        p.OGcd(A.Trick)
            .When(c => !c.Downtime)
            .Because("Trick first - the instinctual after it banks a stack");

        p.OGcd(NextOnTheRing)
            .When(c => !c.Downtime && HeldHeart(c) is not null)
            .Because(c => $"combos off the {HeldHeart(c)} Heart");

        // No Heart: open the ring. Ready() gates each on level and on the shared timer.
        p.OGcd(A.GaleAxe).When(NoHeart).Because("open the ring");
        p.OGcd(A.SpinningAxe).When(NoHeart).Because("open the ring");
        p.OGcd(A.MistralAxe).When(NoHeart).Because("open the ring");
        p.OGcd(A.AvalancheAxe).When(NoHeart).Because("open the ring");
    }

    /// <summary>The Heart in hand, lower-cased, or null when the ring has not been entered.</summary>
    private static string? HeldHeart(RotationContext c) =>
        c.Buff(A.VolantHeart) ? "volant"
        : c.Buff(A.RampantHeart) ? "rampant"
        : c.Buff(A.DurantHeart) ? "durant"
        : c.Buff(A.EldritchHeart) ? "eldritch"
        : null;

    private static bool NoHeart(RotationContext c) => !c.Downtime && HeldHeart(c) is null;

    /// <summary>The instinctual the held Heart combos into.</summary>
    private static ActionRef? NextOnTheRing(RotationContext c) =>
        c.Buff(A.VolantHeart) ? A.AvalancheAxe
        : c.Buff(A.RampantHeart) ? A.MistralAxe
        : c.Buff(A.DurantHeart) ? A.SpinningAxe
        : c.Buff(A.EldritchHeart) ? A.GaleAxe
        : null;

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
