using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Gunbreaker.GunbreakerActions;

namespace OneTwoPunch.Core.Jobs.Gunbreaker;

/// <summary>
/// Gunbreaker, Dawntrail. Cartridges come off the combo and go into Burst Strike, Gnashing
/// Fang and Double Down; No Mercy is a twenty second window every minute that everything
/// gets crammed into.
/// <para>
/// The Continuation follow-ups are the part that has to work. Each one is a free off-global
/// that the game hands you for a couple of seconds and takes back, and every one of them is
/// named outright by a status. They are offered as themselves rather than as Continuation,
/// which is a hotbar placeholder the game draws and swaps but will not accept - Viper had
/// three of those and every rule written against them was silently dead.
/// </para>
/// <para>
/// Mitigation is not here: Nebula, Great Nebula, Camouflage, Heart of Corundum, Heart of
/// Light, Aurora and Superbolide are declared for verification and left to the player.
/// </para>
/// <para>
/// No scripted opener - see PaladinRotation for why.
/// </para>
/// </summary>
public sealed class GunbreakerRotation : JobRotationBase
{
    public override uint JobId => 37;

    public override string Name => "Gunbreaker";

    public override ActionRef SingleTargetButton => A.KeenEdge;

    public override ActionRef AoeButton => A.DemonSlice;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? BurstAction => A.NoMercy;

    public override StatusRef? BurstStatus => A.NoMercyBuff;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // The Continuation follow-ups first: each is free, each is named by the status the
        // global before it left, and each expires in a couple of seconds.
        p.OGcd(A.Hypervelocity).When(c => c.Buff(A.ReadyToBlast));
        p.OGcd(A.JugularRip).When(c => c.Buff(A.ReadyToRip));
        p.OGcd(A.AbdomenTear).When(c => c.Buff(A.ReadyToTear));
        p.OGcd(A.EyeGouge).When(c => c.Buff(A.ReadyToGouge));

        p.OGcd(A.NoMercy).When(c => !c.Downtime).Because("burst window");

        // Bloodfest refills the whole magazine, so it is worth nothing thrown at a full one.
        p.OGcd(A.Bloodfest)
            .When(c => !c.Downtime && c.Gnb.Ammo == 0)
            .Because("refill the cartridges");

        p.OGcd(c => ZoneAction(c)).When(c => !c.Downtime);
        p.OGcd(A.BowShock).When(c => !c.Downtime);

        // A dash, so it is only offered inside the burst rather than as filler that moves
        // the player somewhere they did not ask to be.
        p.OGcd(A.Trajectory)
            .When(c => !c.Downtime && c.Buff(A.NoMercyBuff))
            .Because("spend a charge in burst");

        // ---- GCDs --------------------------------------------------------
<<<<<<< HEAD
        // Both chains live in the same gauge step, and I had this wrong: the reasoning was
        // "the gauge carries a step for Gnashing Fang and none for Reign, so Reign must be
        // the ordinary combo". It is one field counting both. A recorded pull settled it -
        // Reign of Beasts landed three times, the gauge read step 3 for ten seconds after
        // each, and Noble Blood and Lion Heart were not suggested once in two and a half
        // minutes. Two globals of every burst, gone the same way Viper's coils went.
        //
        // 1 Savage Claw, 2 Wicked Talon, 3 Noble Blood, 4 Lion Heart. The combo checks are
        // kept beside them because they cost nothing and this is the second time a chain
        // has not been where it looked like it should be.
        p.Gcd(A.LionHeart).When(c => c.Gnb.AmmoComboStep >= 4 || c.ComboIs(A.NobleBlood));
        p.Gcd(A.NobleBlood).When(c => c.Gnb.AmmoComboStep == 3 || c.ComboIs(A.ReignOfBeasts));
        p.Gcd(A.ReignOfBeasts).When(c => c.Buff(A.ReadyToReign)).Because("Ready to Reign");

=======
        // The Reign chain, deepest step first. This one really is the ordinary combo: the
        // job gauge carries a step for the Gnashing Fang chain and none for this, which is
        // the same tell that says to read Gnashing Fang off the gauge instead.
        p.Gcd(A.LionHeart).When(c => c.ComboIs(A.NobleBlood));
        p.Gcd(A.NobleBlood).When(c => c.ComboIs(A.ReignOfBeasts));
        p.Gcd(A.ReignOfBeasts).When(c => c.Buff(A.ReadyToReign)).Because("Ready to Reign");

        // The Gnashing Fang chain, read off the gauge step: 1 is Savage Claw, 2 is Wicked
        // Talon. A gauge field rather than the combo, for the same reason.
>>>>>>> origin/main
        p.Gcd(A.WickedTalon).When(c => c.Gnb.AmmoComboStep == 2);
        p.Gcd(A.SavageClaw).When(c => c.Gnb.AmmoComboStep == 1);

        p.Gcd(A.GnashingFang)
            .When(c => !c.Downtime && c.Gnb.AmmoComboStep == 0 && c.Gnb.Ammo > 0);

        p.Gcd(A.DoubleDown)
            .When(c => c.Buff(A.NoMercyBuff) && c.Gnb.Ammo >= 2)
            .Because("in No Mercy");

        // Ready to Break is the proc No Mercy hands out at the levels that have it; below
        // those, Sonic Break is an ordinary sixty second global that belongs in the window
        // anyway, so both spellings of "now" are accepted.
        p.Gcd(A.SonicBreak)
            .When(c => c.Buff(A.ReadyToBreak) || c.Buff(A.NoMercyBuff))
            .Because("in No Mercy");

        // Burst Strike inside the window, and outside it only to stop the magazine
        // overflowing - a cartridge lost to a full gauge is a Gnashing Fang that never
        // happened.
        p.Gcd(A.BurstStrike)
            .When(c => c.Gnb.Ammo > 0 && c.Buff(A.NoMercyBuff))
            .Because("in No Mercy");

        p.Gcd(A.BurstStrike)
            .When(c => c.Gnb.Ammo >= MaxAmmo(c) && ComboWouldOverflow(c))
            .Because("the magazine is about to overflow");

        p.Gcd(A.SolidBarrel).When(c => c.ComboIs(A.BrutalShell));
        p.Gcd(A.BrutalShell).When(c => c.ComboIs(A.KeenEdge));
        p.Gcd(A.KeenEdge);

        p.Gcd(A.LightningShot).When(c => !c.InRange).Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.FatedBrand).When(c => c.Buff(A.ReadyToRaze));
        p.OGcd(A.Hypervelocity).When(c => c.Buff(A.ReadyToBlast));

        p.OGcd(A.NoMercy).When(c => !c.Downtime).Because("burst window");

        p.OGcd(A.Bloodfest)
            .When(c => !c.Downtime && c.Gnb.Ammo == 0)
            .Because("refill the cartridges");

        p.OGcd(c => ZoneAction(c)).When(c => !c.Downtime);
        p.OGcd(A.BowShock).When(c => !c.Downtime);

<<<<<<< HEAD
        p.Gcd(A.LionHeart).When(c => c.Gnb.AmmoComboStep >= 4 || c.ComboIs(A.NobleBlood));
        p.Gcd(A.NobleBlood).When(c => c.Gnb.AmmoComboStep == 3 || c.ComboIs(A.ReignOfBeasts));
=======
        p.Gcd(A.LionHeart).When(c => c.ComboIs(A.NobleBlood));
        p.Gcd(A.NobleBlood).When(c => c.ComboIs(A.ReignOfBeasts));
>>>>>>> origin/main
        p.Gcd(A.ReignOfBeasts).When(c => c.Buff(A.ReadyToReign));

        p.Gcd(A.DoubleDown)
            .When(c => c.Gnb.Ammo >= 2)
            .Because("spend two cartridges at once");

        p.Gcd(A.SonicBreak).When(c => c.Buff(A.ReadyToBreak) || c.Buff(A.NoMercyBuff));

        p.Gcd(A.FatedCircle)
            .When(c => c.Gnb.Ammo > 0 && (c.Buff(A.NoMercyBuff) || c.Gnb.Ammo >= MaxAmmo(c)))
            .Because("spend a cartridge");

        p.Gcd(A.DemonSlaughter).When(c => c.ComboIs(A.DemonSlice));
        p.Gcd(A.DemonSlice);
    }

    /// <summary>
    /// How many cartridges the magazine holds: two, and three once Cartridge Charge II
    /// arrives at level 88.
    /// </summary>
    private static byte MaxAmmo(RotationContext c) => c.Level >= 88 ? (byte)3 : (byte)2;

    /// <summary>
    /// True when the combo is one step from handing over a cartridge there is no room for.
    /// Solid Barrel and Demon Slaughter are what load the magazine, so a full one plus a
    /// live combo about to finish is a cartridge on the floor.
    /// </summary>
    private static bool ComboWouldOverflow(RotationContext c) =>
        c.ComboIs(A.BrutalShell) || c.ComboIs(A.DemonSlice);

    /// <summary>Blasting Zone is Danger Zone's upgrade; both are the same press.</summary>
    private static ActionRef ZoneAction(RotationContext c) =>
        c.Has(A.BlastingZone) ? A.BlastingZone : A.DangerZone;

    /// <summary>What the recorded log prints for a Gunbreaker line.</summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.Gunbreaker;
<<<<<<< HEAD
        // Not "gnashing step": the same field counts the Reign chain, and calling it after
        // one of the two chains is what made it look like the other had none.
        var chain = g.AmmoComboStep > 0 ? $", chain step {g.AmmoComboStep}" : string.Empty;
=======
        var chain = g.AmmoComboStep > 0 ? $", gnashing step {g.AmmoComboStep}" : string.Empty;
>>>>>>> origin/main

        return $"cartridges {g.Ammo}{chain}";
    }
}
