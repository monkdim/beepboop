using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Model;
using OneTwoPunch.Plugin;
using Xunit;
using V = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Tests;

public sealed class LiveSettingsTests
{
    [Theory]
    [InlineData(RotationMode.SingleTarget)]
    [InlineData(RotationMode.Aoe)]
    public void SavingNoneUpdatesAnExistingSessionAndDropsAHeldWeave(RotationMode mode)
    {
        var config = new Configuration { WeaveStyle = WeaveStyle.Double, UseOpener = false, SuggestionHoldSeconds = .5f };
        var session = new RotationSession(JobRegistry.Create(41)!, config.ToRotationSettings());
        config.Changed += () => session.ApplySettings(config.ToRotationSettings());
        var snapshot = new SnapshotBuilder().Job(41).Gcd(1.6f).At(10).Build();
        var actions = new FakeActionState();
        Assert.Equal(ActionKind.OGcd, session.Resolve(mode, snapshot, actions).Kind);

        config.WeaveStyle = WeaveStyle.None;
        config.Save();
        snapshot.Now += .01;

        Assert.Equal(0, session.WeaveBudget);
        Assert.Equal(ActionKind.Gcd, session.Resolve(mode, snapshot, actions).Kind);
    }

    [Theory]
    [InlineData(RotationMode.SingleTarget)]
    [InlineData(RotationMode.Aoe)]
    public void LoweringTheLimitDoesNotForgetTheFirstWeave(RotationMode mode)
    {
        var session = new RotationSession(JobRegistry.Create(41)!, new RotationSettings { WeaveStyle = WeaveStyle.Double, UseOpener = false });
        var snapshot = new SnapshotBuilder().Job(41).Gcd(1.6f).Buff(V.SwiftskinsVenom.Id,30).Build();
        var actions = new FakeActionState().OnCooldown(V.SerpentsIre.Id,60);
        session.Resolve(mode,snapshot,actions);
        session.NotifyActionUsed(V.TwinfangBite.Id);
        session.ApplySettings(session.Settings with { WeaveStyle = WeaveStyle.Single });
        Assert.Equal(ActionKind.Gcd, session.Resolve(mode,snapshot,actions).Kind);
    }

    [Fact]
    public void TwoStillAllowsBothViperBites()
    {
        var session = new RotationSession(JobRegistry.Create(41)!, new RotationSettings { WeaveStyle = WeaveStyle.Double, UseOpener = false, SuggestionHoldSeconds = 0 });
        var snapshot = new SnapshotBuilder().Job(41).Gcd(1.6f).Buff(V.SwiftskinsVenom.Id,30).Build();
        var actions = new FakeActionState().OnCooldown(V.SerpentsIre.Id,60);
        session.Resolve(RotationMode.SingleTarget,snapshot,actions);
        session.NotifyActionUsed(V.TwinfangBite.Id);
        Assert.Equal(V.TwinbloodBite.Id, session.Resolve(RotationMode.SingleTarget,snapshot,actions).Action.Id);
        session.NotifyActionUsed(V.TwinbloodBite.Id);
        Assert.Equal(ActionKind.Gcd, session.Resolve(RotationMode.SingleTarget,snapshot,actions).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoneDoesNotAskForVipersPrePullSlither(bool inCombat)
    {
        var session = new RotationSession(JobRegistry.Create(41)!, new RotationSettings { WeaveStyle = WeaveStyle.None });
        var snapshot = new SnapshotBuilder().Job(41).Gcd(1.6f).Build();
        snapshot.InCombat = inCombat;
        snapshot.CombatDuration = 0;
        Assert.Equal(ActionKind.Gcd, session.Resolve(RotationMode.SingleTarget,snapshot,new FakeActionState()).Kind);
        Assert.False(session.OpenerActive);
    }

    [Theory]
    [InlineData(WeaveStyle.Single, ActionKind.Gcd)]
    [InlineData(WeaveStyle.Double, ActionKind.OGcd)]
    public void TheViperOpenerObeysTheLimitForItsSecondBite(WeaveStyle style, ActionKind expected)
    {
        var job = JobRegistry.Create(41)!;
        var session = new RotationSession(job,new RotationSettings { WeaveStyle = style, SuggestionHoldSeconds = 0 });
        var snapshot = new SnapshotBuilder().Job(41).Gcd(1.6f).Build();
        snapshot.CombatDuration = 0;
        var actions = new FakeActionState();
        session.Resolve(RotationMode.SingleTarget,snapshot,actions);
        foreach (var step in job.Opener!.Steps.Take(7)) session.NotifyActionUsed(step.Id);
        Assert.Equal(expected,session.Resolve(RotationMode.SingleTarget,snapshot,actions).Kind);
    }

    [Fact]
    public void OtherSettingsApplyWithoutRewindingTheOpener()
    {
        var session = new RotationSession(JobRegistry.Create(41)!,new RotationSettings());
        var snapshot = new SnapshotBuilder().Job(41).Gcd(1.6f).Build();
        snapshot.CombatDuration = 0;
        session.Resolve(RotationMode.SingleTarget,snapshot,new FakeActionState());
        session.NotifyActionUsed(V.Slither.Id);
        var step = session.OpenerStep;
        var updated = session.Settings with { WeaveSafetyMargin = .3f, SuggestSelfHeal = false, AoeFallsBackToSingleTarget = true, SuggestionHoldSeconds = .4f, PotionEnabled = true, SuggestPositionalRescue = false };
        Assert.True(session.ApplySettings(updated));
        Assert.Equal(step,session.OpenerStep);
        Assert.Equal(updated,session.Settings);
        Assert.False(session.ApplySettings(updated with { }));
    }

    [Fact]
    public void TogglingTheOpenerDuringCombatDoesNotRestartIt()
    {
        var session = new RotationSession(JobRegistry.Create(41)!,new RotationSettings());
        var snapshot = new SnapshotBuilder().Job(41).Gcd(1.6f).Build();
        snapshot.CombatDuration = 0;
        session.Resolve(RotationMode.SingleTarget,snapshot,new FakeActionState());
        session.NotifyActionUsed(V.Slither.Id);
        session.ApplySettings(session.Settings with { UseOpener = false });
        session.ApplySettings(session.Settings with { UseOpener = true });
        Assert.False(session.OpenerActive);
        snapshot.InCombat = false;
        session.Resolve(RotationMode.SingleTarget,snapshot,new FakeActionState());
        Assert.True(session.OpenerActive);
    }

    [Fact]
    public void WelcomeAppearsOnInstallAndUpdateButNotEveryRestart()
    {
        var config = new Configuration();
        Assert.True(config.NeedsWelcome("1.0.83.0"));
        config.AcknowledgeWelcome("1.0.83.0");
        Assert.False(config.NeedsWelcome("1.0.83.0"));
        var reloaded = new Configuration { LastSeenVersion = config.LastSeenVersion };
        Assert.False(reloaded.NeedsWelcome("1.0.83.0"));
        Assert.True(reloaded.NeedsWelcome("1.0.84.0"));
    }

    /// <summary>
    /// "Globals only" is absolute: no job may weave against it. Above that the budget is the
    /// selected limit or the job's own minimum, whichever is higher - a job that declares no
    /// minimum therefore gets exactly what was selected.
    /// </summary>
    [Fact]
    public void GlobalsOnlyBindsEveryJobAndOtherwiseTheJobMinimumCanRaiseIt()
    {
        foreach (var job in JobRegistry.CreateAll())
        foreach (var style in new[] { WeaveStyle.None, WeaveStyle.Single, WeaveStyle.Double })
        {
            var session = new RotationSession(job, new RotationSettings { WeaveStyle = style });

            var expected = style == WeaveStyle.None
                ? 0
                : Math.Max((int)style, (int)job.MinimumWeaveStyle);

            Assert.Equal(expected, session.WeaveBudget);
            Assert.InRange(session.WeaveBudget, 0, 2);
        }
    }

    [Fact]
    public void RestoringWeavesMidFightDoesNotStartAnOpener()
    {
        var session = new RotationSession(JobRegistry.Create(41)!,new RotationSettings { WeaveStyle = WeaveStyle.None });
        session.Resolve(RotationMode.SingleTarget,new SnapshotBuilder().Job(41).Gcd(1.6f).Build(),new FakeActionState());
        session.ApplySettings(session.Settings with { WeaveStyle = WeaveStyle.Single });
        Assert.False(session.OpenerActive);
    }
}
