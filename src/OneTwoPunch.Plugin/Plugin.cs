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

    /// <summary>
    /// Frame counter, used to answer the hook from cache.
    /// <para>
    /// The game asks what a hotbar slot should cast for every slot, every frame, just to
    /// draw the icons - and a player with several bars showing the same action asks many
    /// times over. Rebuilding the whole picture of the world for each of those calls meant
    /// sweeping the object table dozens of times a frame. It is built once per frame now,
    /// and every call after the first in the same frame is a dictionary lookup.
    /// </para>
    /// </summary>
    private long _frame;

    private CombatSnapshot? _frameSnapshot;
    private long _frameSnapshotAt = -1;

    private readonly Dictionary<RotationMode, (long Frame, Suggestion Suggestion)> _resolved = [];

    /// <summary>Last resolved suggestion per mode, for the heads-up display.</summary>
    private readonly Dictionary<RotationMode, Suggestion> _lastSuggestion = [];

    /// <summary>Consecutive failing frames after which the plugin switches itself off.</summary>
    private const int FailureLimit = 20;

    /// <summary>True once the hook is installed, which only happens in the world.</summary>
    private bool _active;

    private int _consecutiveFailures;
    private bool _shutDown;

    /// <summary>Jobs whose verification is in flight, so it is not started twice.</summary>
    private readonly HashSet<uint> _verifying = [];

    /// <summary>
    /// Whether the plugin is allowed to touch the game at all.
    /// <para>
    /// Starts false, every session, and is deliberately not saved. Installing this plugin
    /// has repeatedly frozen the game, and the cause has not been found - so installing it
    /// must not be the thing that runs it. Loading now does nothing but register two
    /// commands; the hook is not installed and the frame handler returns immediately until
    /// somebody types /otp arm.
    /// </para>
    /// <para>
    /// Not saved, on purpose: whatever state an armed session gets into, restarting the
    /// game always comes back inert. There is no way to end up stuck in a game that will
    /// not start.
    /// </para>
    /// </summary>
    private bool _armed;

    /// <summary>How long the constructor took, reported once the player can read chat.</summary>
    private readonly long _loadMilliseconds;

    private bool _saidHello;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        // Timed and reported, because a plugin that is slow to load is felt as the game
        // hanging and there is no way to tell from the outside whose fault it is. If the
        // number below is small and the game still stalled while installing, it was not
        // this plugin's loading that did it.
        var started = Environment.TickCount64;

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

        // Deliberately not enabled here. Plugins are constructed while the game is still
        // starting up, and this one installs a hook into the action system - so it waits
        // until there is a character standing in the world. See OnUpdate.
        _replacer = new ActionReplacer(Interop, Log, Classify, Resolve);

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
                        + "/otp arm - let it run, this session only (it starts inert)\n"
                        + "/otp disarm - stop it and remove the hook\n"
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

        _loadMilliseconds = Environment.TickCount64 - started;
        Log.Information("One Two Punch: loaded in {Elapsed}ms.", _loadMilliseconds);
    }

    // ---- Frame loop ------------------------------------------------------

    private void OnUpdate(IFramework framework)
    {
        if (_shutDown)
            return;

        try
        {
            Update(framework);
            _consecutiveFailures = 0;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            Log.Error(ex, "One Two Punch: update failed ({Count} in a row)", _consecutiveFailures);

            // A fault that repeats every frame is not going to fix itself, and a plugin that
            // keeps throwing inside the game's frame loop is worse than one that is off. Shut
            // down for the rest of the session rather than making the player's game unusable.
            if (_consecutiveFailures >= FailureLimit)
            {
                _shutDown = true;
                Deactivate();
                Chat.PrintError(
                    "[One Two Punch] Switched off after repeated errors. Your hotbars are back to "
                    + "normal. Please send the Dalamud log to the plugin author.");
                Log.Error("One Two Punch: shut down after {Count} consecutive update failures.", _consecutiveFailures);
            }
        }
    }

    private void Update(IFramework framework)
    {
        // Disarmed: not a single thing happens. No hook, no reads, no work.
        if (!_armed)
            return;

        var player = Objects.LocalPlayer;

        // Not in the world: title screen, character select, a loading screen, a cutscene
        // transition. Nothing here reads the game while that is true, and the hook is not
        // installed - so the plugin cannot be the thing that breaks your login.
        if (player is null)
        {
            if (_active)
                Deactivate();

            return;
        }

        if (!_active && !Activate())
            return;

        var delta = (float)framework.UpdateDelta.TotalSeconds;
        _now += delta;
        _frame++;

        _movement.Update(player, delta);
        _state.Tick(delta);

        var jobId = player.ClassJob.RowId;
        if (jobId != _currentJobId)
        {
            _currentJobId = jobId;
            SwitchJob(jobId);
        }

        if (_session is not null)
            _useWatcher.Tick();
    }

    /// <summary>
    /// Installs the hook, now that there is definitely a character in the world. Returns
    /// false if the hook is not available, in which case the plugin stays inert and tries
    /// again next frame.
    /// </summary>
    /// <summary>
    /// Lets the plugin start working, for this session only. This is the moment the hook
    /// goes in - deliberately a thing you do on purpose, rather than a thing that happens
    /// to you because you installed something.
    /// </summary>
    private void Arm()
    {
        if (_armed)
        {
            Chat.Print("[One Two Punch] Already running this session.");
            return;
        }

        _armed = true;
        _shutDown = false;
        _consecutiveFailures = 0;

        Chat.Print(
            $"[One Two Punch] Armed. Loaded in {_loadMilliseconds}ms. "
            + "It is off again next time the game starts - /otp disarm to stop it now.");
    }

    /// <summary>Stops everything and takes the hook back out. Hotbars return to normal.</summary>
    private void Disarm()
    {
        if (!_armed)
        {
            Chat.Print("[One Two Punch] Already inert.");
            return;
        }

        _armed = false;
        Deactivate();

        Chat.Print(
            $"[One Two Punch] Stopped, hotbars back to normal. "
            + $"Nested action lookups turned away while running: {_replacer.SuppressedReentrantCalls}.");
    }

    private bool Activate()
    {
        if (!_replacer.Enable())
            return false;

        _active = true;

        if (!_saidHello)
        {
            _saidHello = true;
            Chat.Print($"[One Two Punch] Ready. Loaded in {_loadMilliseconds}ms. /otp for settings.");
        }

        return true;
    }

    /// <summary>Removes the hook and forgets everything about the last character.</summary>
    private void Deactivate()
    {
        _replacer.Disable();
        _active = false;

        _currentJobId = 0;
        _job = null;
        _session = null;
        _lastSuggestion.Clear();
        _resolved.Clear();
        _frameSnapshotAt = -1;
        _frameSnapshot = null;
        _movement.Reset();
        _useWatcher.Reset();
    }

    /// <summary>
    /// Puts the plugin back to knowing nothing about the current job, and starts working
    /// out the new one. The work itself happens off the game's thread - see
    /// <see cref="VerifyInBackground"/>.
    /// </summary>
    private void SwitchJob(uint jobId)
    {
        _job = null;
        _session = null;
        _lastSuggestion.Clear();
        _resolved.Clear();
        _frameSnapshotAt = -1;
        _frameSnapshot = null;
        _movement.Reset();
        _useWatcher.Reset();

        if (!JobRegistry.IsSupported(jobId))
            return;

        // Already checked out this session: nothing to do but switch it on.
        if (_reports.TryGetValue(jobId, out var known))
        {
            Adopt(jobId, known);
            return;
        }

        VerifyInBackground(jobId);
    }

    /// <summary>
    /// Checks a job's action table against the game's data on a worker thread, then hands
    /// the result back to the framework thread to be adopted.
    /// <para>
    /// This is off the game's thread because it is the one piece of start-up work with no
    /// upper bound. If a single action id fails to match its name - after a patch shuffles
    /// them, say - the verifier asks for a lookup by name, and building that lookup means
    /// reading every row of the game's Action sheet. Doing that between two frames is how
    /// you stop the game dead.
    /// </para>
    /// </summary>
    private void VerifyInBackground(uint jobId)
    {
        if (!_verifying.Add(jobId))
            return;

        Task.Run(() =>
        {
            try
            {
                var rotation = JobRegistry.Create(jobId);
                if (rotation is null)
                {
                    Framework.RunOnFrameworkThread(() => _verifying.Remove(jobId));
                    return;
                }

                var started = Environment.TickCount64;
                var report = ActionTableVerifier.Verify(rotation, _gameData);
                var elapsed = Environment.TickCount64 - started;

                Framework.RunOnFrameworkThread(() =>
                {
                    _verifying.Remove(jobId);
                    _reports[jobId] = report;

                    if (report.RepairedCount > 0 || report.UnresolvedCount > 0 || _config.VerboseLogging)
                        Log.Information("One Two Punch: {Summary}", report.Summarise());

                    Log.Information(
                        "One Two Punch: verified {Job} in {Elapsed}ms (off the framework thread).",
                        rotation.Name, elapsed);

                    // The player may have changed job again while this ran.
                    if (_currentJobId == jobId)
                        Adopt(jobId, report);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "One Two Punch: verifying job {JobId} failed", jobId);

                // _verifying is only ever touched on the framework thread.
                Framework.RunOnFrameworkThread(() => _verifying.Remove(jobId));
            }
        });
    }

    /// <summary>
    /// Switches a verified job on. Never run a job whose action table did not check out:
    /// guessing an id means pressing the wrong ability in a raid, which is worse than the
    /// plugin being off.
    /// </summary>
    private void Adopt(uint jobId, VerificationReport report)
    {
        var rotation = JobRegistry.Create(jobId);
        if (rotation is null)
            return;

        if (!report.IsSafeToRun)
        {
            Chat.PrintError(
                $"[One Two Punch] {rotation.Name} is switched off: {report.UnresolvedActionCount} action id(s) "
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

        // Answered already this frame for this button.
        if (_resolved.TryGetValue(mode, out var cached) && cached.Frame == _frame)
            return cached.Suggestion;

        // The world does not change between hook calls within a frame, and the snapshot does
        // not depend on which button is being asked about, so both buttons share one.
        if (_frameSnapshotAt != _frame)
        {
            _frameSnapshot = _state.Build(_job, _now);
            _frameSnapshotAt = _frame;

            if (_frameSnapshot is not null)
                _actionState.BeginFrame(_frameSnapshot.Level);
        }

        if (_frameSnapshot is null)
            return null;

        var suggestion = _session.Resolve(mode, _frameSnapshot, _actionState);
        _resolved[mode] = (_frame, suggestion);
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

            case "arm":
                Arm();
                break;

            case "disarm":
                Disarm();
                break;

            case "on":
            case "off":
                _config.Enabled = args.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
                _config.Save();
                Chat.Print($"[One Two Punch] {(_config.Enabled ? "Enabled" : "Disabled")}.");

                if (_config.Enabled && !_armed)
                    Chat.Print("[One Two Punch] Still inert this session - type /otp arm to let it run.");

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
        Chat.Print(
            $"[One Two Punch] Loaded in {_loadMilliseconds}ms. "
            + $"Nested action lookups turned away: {_replacer.SuppressedReentrantCalls}.");
        Chat.Print("[One Two Punch] Checking every job's action ids against the game's data...");

        // On a worker thread for the same reason job switching is: a single mismatched id
        // sends the verifier to a lookup built by reading the whole Action sheet, and doing
        // that thirteen times between two frames would stop the game.
        Task.Run(() =>
        {
            var lines = new List<string>();
            var fresh = new Dictionary<uint, VerificationReport>();

            try
            {
                foreach (var rotation in JobRegistry.CreateAll())
                {
                    var report = ActionTableVerifier.Verify(rotation, _gameData);
                    fresh[rotation.JobId] = report;
                    lines.Add(report.Summarise());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "One Two Punch: verification sweep failed");
                Framework.RunOnFrameworkThread(
                    () => Chat.PrintError("[One Two Punch] Verification failed - see the Dalamud log."));
                return;
            }

            Framework.RunOnFrameworkThread(() =>
            {
                _reports.Clear();
                foreach (var pair in fresh)
                    _reports[pair.Key] = pair.Value;

                foreach (var line in lines)
                {
                    Chat.Print($"[One Two Punch] {line}");
                    Log.Information("One Two Punch verify: {Summary}", line);
                }

                // Rebuild the current session so any repaired id takes effect immediately.
                var jobId = _currentJobId;
                _currentJobId = 0;
                SwitchJob(jobId);
                _currentJobId = jobId;
            });
        });
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
