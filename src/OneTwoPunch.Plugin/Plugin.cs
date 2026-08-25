using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using OneTwoPunch.Plugin.Services;
using OneTwoPunch.Plugin.UI;

namespace OneTwoPunch.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    /// <summary>
    /// Both are registered. The short one is what anybody will actually type, but three
    /// letters is a small namespace and another plugin may already own it - so the long
    /// form is always there as the one that cannot be taken.
    /// </summary>
    private static readonly string[] CommandNames = ["/otp", "/onetwopunch"];

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IJobGauges Gauges { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration _config;
    private readonly WindowSystem _windows = new("OneTwoPunch");
    private readonly ConfigWindow _configWindow;
    private readonly PreviewWindow _previewWindow;

    private readonly MovementTracker _movement = new();
    private readonly ActionStateAdapter _actionState = new();
    private readonly ActionUseWatcher _useWatcher = new();
    private readonly GameStateProvider _state;
    private readonly LuminaGameData _gameData;
    private readonly PotionTracker _potions;
    private readonly ActionReplacer _replacer;

    private readonly Dictionary<uint, VerificationReport> _reports = [];

    private IJobRotation? _job;
    private RotationSession? _session;
    private uint _currentJobId;
    private double _now;

    /// <summary>Last resolved suggestion per mode, for the heads-up display.</summary>
    private readonly Dictionary<RotationMode, Suggestion> _lastSuggestion = [];

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Plugin>();

        _config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _gameData = new LuminaGameData(Data);
        _potions = new PotionTracker(Data);

        _state = new GameStateProvider(
            Condition, Targets, Objects, Gauges, _movement, _potions, _config);

        _configWindow = new ConfigWindow(_config, _potions, () => _job, () => _reports);
        _previewWindow = new PreviewWindow(_config, Textures, _gameData, () => _lastSuggestion, () => _job);
        _windows.AddWindow(_configWindow);
        _windows.AddWindow(_previewWindow);

        _replacer = new ActionReplacer(Interop, Log, Classify, Resolve);
        _replacer.Enable();

        _useWatcher.ActionUsed += OnActionUsed;

        Framework.Update += OnUpdate;

        PluginInterface.UiBuilder.Draw += _windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfig;

        foreach (var name in CommandNames)
        {
            try
            {
                Commands.AddHandler(name, new CommandInfo(OnCommand)
                {
                    HelpMessage =
                        "Open One Two Punch settings.\n"
                        + "/otp verify - check every action id against the game's own data\n"
                        + "/otp hud - toggle the next-action display\n"
                        + "/otp on|off - master switch",
                    ShowInHelp = name == CommandNames[0],
                });
            }
            catch (Exception ex)
            {
                // Another plugin owns this one. The other name still works, so this is a
                // note rather than a failure to load.
                Log.Warning(ex, "One Two Punch: could not register {Command}", name);
            }
        }
    }

    // ---- Frame loop ------------------------------------------------------

    private void OnUpdate(IFramework framework)
    {
        var delta = (float)framework.UpdateDelta.TotalSeconds;
        _now += delta;

        _movement.Update(Objects.LocalPlayer, delta);
        _state.Tick(delta);

        var player = Objects.LocalPlayer;
        var jobId = player?.ClassJob.RowId ?? 0;

        if (jobId != _currentJobId)
        {
            _currentJobId = jobId;
            SwitchJob(jobId);
        }

        if (_session is not null)
            _useWatcher.Tick();
    }

    private void SwitchJob(uint jobId)
    {
        _job = null;
        _session = null;
        _lastSuggestion.Clear();
        _movement.Reset();
        _useWatcher.Reset();

        var rotation = JobRegistry.Create(jobId);
        if (rotation is null)
            return;

        // Never run a job whose action table did not check out. Guessing an id means
        // pressing the wrong ability in a raid, which is worse than the plugin being off.
        if (!_reports.TryGetValue(jobId, out var report))
        {
            report = ActionTableVerifier.Verify(rotation, _gameData);
            _reports[jobId] = report;

            if (report.RepairedCount > 0 || report.UnresolvedCount > 0 || _config.VerboseLogging)
                Log.Information("One Two Punch: {Summary}", report.Summarise());
        }

        if (!report.IsSafeToRun)
        {
            Chat.PrintError(
                $"[One Two Punch] {rotation.Name} is switched off: {report.UnresolvedCount} action id(s) "
                + "could not be matched against the game's data. Run /otp verify for the list.");
            return;
        }

        _job = rotation;
        _session = new RotationSession(rotation, _config.ToRotationSettings());
        _session.RefreshActionIds();
        _useWatcher.Track(rotation);
    }

    private void OnActionUsed(uint actionId) => _session?.NotifyActionUsed(actionId);

    // ---- The two buttons -------------------------------------------------

    /// <summary>
    /// Decides whether an action id is one of our two buttons. Everything else is passed
    /// straight through untouched, so the rest of the hotbar behaves exactly as it always did.
    /// </summary>
    private RotationMode? Classify(uint actionId)
    {
        if (!_config.Enabled || _job is null || _session is null)
            return null;

        if (actionId == _job.SingleTargetButton.Id)
            return RotationMode.SingleTarget;

        if (actionId == _job.AoeButton.Id)
            return RotationMode.Aoe;

        for (var i = 0; i < _job.ExtraButtons.Count; i++)
        {
            if (actionId == _job.ExtraButtons[i].Host.Id)
                return i == 0 ? RotationMode.Extra1 : RotationMode.Extra2;
        }

        return null;
    }

    private Suggestion? Resolve(RotationMode mode)
    {
        if (_job is null || _session is null)
            return null;

        var snapshot = _state.Build(_job, _now);
        if (snapshot is null)
            return null;

        _actionState.BeginFrame(snapshot.Level);

        var suggestion = _session.Resolve(mode, snapshot, _actionState);
        _lastSuggestion[mode] = suggestion;
        return suggestion;
    }

    // ---- Commands --------------------------------------------------------

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
            case "config":
                ToggleConfig();
                break;

            case "verify":
                PrintVerification();
                break;

            case "hud":
                _config.ShowPreview = !_config.ShowPreview;
                _config.Save();
                Chat.Print($"[One Two Punch] Next-action display {(_config.ShowPreview ? "on" : "off")}.");
                break;

            case "on":
            case "off":
                _config.Enabled = args.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
                _config.Save();
                Chat.Print($"[One Two Punch] {(_config.Enabled ? "Enabled" : "Disabled")}.");
                break;

            default:
                Chat.PrintError($"[One Two Punch] Unknown option '{args}'. Try /otp verify.");
                break;
        }
    }

    /// <summary>
    /// Re-checks every supported job's action table against the game and prints the result.
    /// This is the thing to run after a patch, and the thing to paste into a bug report.
    /// </summary>
    private void PrintVerification()
    {
        _reports.Clear();

        foreach (var rotation in JobRegistry.CreateAll())
        {
            var report = ActionTableVerifier.Verify(rotation, _gameData);
            _reports[rotation.JobId] = report;

            Chat.Print($"[One Two Punch] {report.Summarise()}");
            Log.Information("One Two Punch verify: {Summary}", report.Summarise());
        }

        // Rebuild the current session so any repaired id takes effect immediately.
        var jobId = _currentJobId;
        _currentJobId = 0;
        SwitchJob(jobId);
        _currentJobId = jobId;
    }

    private void ToggleConfig() => _configWindow.Toggle();

    public void Dispose()
    {
        foreach (var name in CommandNames)
            Commands.RemoveHandler(name);

        PluginInterface.UiBuilder.Draw -= _windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfig;

        Framework.Update -= OnUpdate;
        _useWatcher.ActionUsed -= OnActionUsed;

        _replacer.Dispose();
        _windows.RemoveAllWindows();
    }
}
