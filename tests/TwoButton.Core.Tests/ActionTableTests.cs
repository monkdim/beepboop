using TwoButton.Core.Jobs;
using TwoButton.Core.Model;
using Xunit;
using Drg = TwoButton.Core.Jobs.Dragoon.DragoonActions;
using Mch = TwoButton.Core.Jobs.Machinist.MachinistActions;

namespace TwoButton.Core.Tests;

/// <summary>
/// The action tables are generated (see tools/generate_action_tables.py). These pin the
/// facts that are easy to get wrong on a regeneration and expensive to get wrong in a raid.
/// </summary>
public sealed class ActionTableTests
{
    /// <summary>
    /// Weaponskills that carry their own cooldown still roll the global cooldown. Classing
    /// one as an off-global would have the engine try to weave it, which clips a GCD every
    /// time it comes up.
    /// </summary>
    [Fact]
    public void WeaponskillsWithTheirOwnCooldownAreStillGlobals()
    {
        Assert.Equal(ActionKind.Gcd, Mch.Drill.Kind);
        Assert.Equal(ActionKind.Gcd, Mch.Bioblaster.Kind);
        Assert.Equal(ActionKind.Gcd, Mch.AirAnchor.Kind);
        Assert.Equal(ActionKind.Gcd, Mch.ChainSaw.Kind);
        Assert.Equal(ActionKind.Gcd, Mch.HeatBlast.Kind);
    }

    [Fact]
    public void RealOffGlobalsAreStillOffGlobals()
    {
        Assert.Equal(ActionKind.OGcd, Drg.LifeSurge.Kind);
        Assert.Equal(ActionKind.OGcd, Drg.LanceCharge.Kind);
        Assert.Equal(ActionKind.OGcd, Drg.TrueNorth.Kind);
        Assert.Equal(ActionKind.OGcd, Mch.Reassemble.Kind);
        Assert.Equal(ActionKind.OGcd, Mch.Hypercharge.Kind);
    }

    /// <summary>
    /// Levels gate rules in synced content. These three were wrong when the tables were
    /// written by hand: Drakesbane is a level 64 finisher, and Lance Barrage and Spiral Blow
    /// are level 96 upgrades rather than the low-level actions they replace.
    /// </summary>
    [Fact]
    public void UpgradeLevelsAreRight()
    {
        Assert.Equal(64, Drg.Drakesbane.Level);
        Assert.Equal(96, Drg.LanceBarrage.Level);
        Assert.Equal(96, Drg.SpiralBlow.Level);
        Assert.Equal(4, Drg.VorpalThrust.Level);
        Assert.Equal(18, Drg.Disembowel.Level);
        Assert.Equal(86, Drg.HeavensThrust.Level);
    }

    [Fact]
    public void NoTwoActionsInAJobShareAnId()
    {
        foreach (var job in JobRegistry.CreateAll())
        {
            var ids = job.AllActions.Select(a => a.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }

    [Fact]
    public void EveryDeclaredActionHasAPlausibleIdAndLevel()
    {
        foreach (var job in JobRegistry.CreateAll())
        {
            foreach (var action in job.AllActions)
            {
                Assert.True(action.Id > 0, $"{job.Name}: {action.Name} has no id");
                Assert.InRange(action.Level, (byte)1, (byte)100);
            }

            foreach (var status in job.AllStatuses)
                Assert.True(status.Id > 0, $"{job.Name}: {status.Name} has no id");
        }
    }
}
