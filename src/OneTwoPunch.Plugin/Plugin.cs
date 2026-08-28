using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
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

    // Forwarders, so the rest of the plugin reads the same as it always did. The services
    // themselves live on Svc - see the note there, which is the whole reason this plugin
    // used to freeze the game on load.
    internal static IDalamudPluginInterface PluginInterface => Svc.PluginInterface;
    internal static IClientState ClientState => Svc.ClientState;
    internal static ICondition Condition => Svc.Condition;
    internal static ITargetManager Targets => Svc.Targets;
    internal static IObjectTable Objects => Svc.Objects;
    internal static IPartyList Party => Svc.Party;
    internal static IJobGauges Gauges => Svc.Gauges;
    internal static IFramework Framework => Svc.Framework;
    internal static IGameInteropProvider Interop => Svc.Interop;
    internal static ICommandManager Commands => Svc.Commands;
    internal static IDataManager Data => Svc.Data;
    internal static ITextureProvider Textures => Svc.Textures;
    internal static IChatGui Chat => Svc.Chat;
    internal static IPluginLog Log => Svc.Log;

    private readonly Configuration _config;
    private readonly WindowSystem _windows = new("OneTwoPunch");
    private readonly ConfigWindow _configWindow;
    private readonly PreviewWindow _previewWindow;

    private readonly MovementTracker _movement = new();
    private readonly ActionStateAdapter _actionState = new();
    // Assigned in the constructor, not here: a field initialiser runs before the
    // constructor body, so before Create<Svc>() has injected anything - Log would be null.
    private readonly ActionUseWatcher _useWatcher;
    private readonly GameStateProvider _state;
    private readonly LuminaGameData _gameData;
    private readonly PotionTracker _potions;
    private readonly ActionReplacer _replacer;
    private readonly HotbarIconReplacer _icons;
    private readonly PartyTargetRedirect _partyTargeting;
    private readonly CastMovementLock _castLock;
    private readonly SessionRecorder _recorder = new();

    private readonly Dictionary<uint, VerificationReport> _reports = [];

    private IJobRotation? _job;
    private RotationSession? _session;
    private uint _currentJobId;

    /// <summary>
    /// Every id that is one of our buttons, including the upgraded form the player's hotbar
    /// actually carries. Rebuilt on a job switch and on a level change, which is the only
    /// time it can change.
    /// </summary>
    private readonly Dictionary<uint, RotationMode> _buttonForms = [];

    private byte _buttonFormsLevel;
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

        // Svc, never Plugin. Create<T> builds an instance of T, so passing the type whose
        // constructor you are standing in builds another one, and another, without end.
        pluginInterface.Create<Svc>();

        _config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        _useWatcher = new ActionUseWatcher(Log);
        _gameData = new LuminaGameData(Data);
        _potions = new PotionTracker(Data);

        _state = new GameStateProvider(
            Condition, Targets, Objects, Gauges, _movement, _potions, _config);

        _configWindow = new ConfigWindow(_config, _potions, () => _job, () => _reports, () => _armed);
        _previewWindow = new PreviewWindow(_config, Textures, _gameData, () => _lastSuggestion, () => _job);
        // Dalamud's WindowSystem only draws a window whose IsOpen is true; DrawConditions
        // is an extra gate on top of that, not a replacement. This was never set, so the
        // whole heads-up display - next action, positional banner, potion prompt - has
        // never once been drawn. It is a permanent HUD, so it is simply always open, and
        // DrawConditions decides when it actually has anything to say.
        _previewWindow.IsOpen = true;

        _windows.AddWindow(_configWindow);
        _windows.AddWindow(_previewWindow);

        // Deliberately not enabled here. Plugins are constructed while the game is still
        // starting up, and this one installs a hook into the action system - so it waits
        // until there is a character standing in the world. See OnUpdate.
        _replacer = new ActionReplacer(Interop, Log, Classify, Resolve);
        _icons = new HotbarIconReplacer(Interop, Log, Classify, Resolve);
        _partyTargeting = new PartyTargetRedirect(
            Interop, Log, Party, () => _config.Enabled && _config.AetherialManipulationToTank);

        _castLock = new CastMovementLock(
            Interop,
            Log,
            () => _config.Enabled && _config.LockMovementWhileCasting,
            () => _config.SlidecastWindowSeconds,
            () => Objects.LocalPlayer);

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
                        + "/otp record - start or stop recording a pull to your Downloads\n"
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

        // The upgraded form of a button changes exactly twice in a job's life - when the
        // upgrade is learned, and when a level sync takes it away again - so this asks the
        // game only when the level moves rather than every frame.
        if (player.Level != _buttonFormsLevel || _buttonForms.Count == 0)
        {
            _buttonFormsLevel = player.Level;
            RefreshButtonForms();
        }

        // Work out both buttons every frame, whether or not the game has asked.
        //
        // This used to happen only inside the hook, which means only when the game felt like
        // redrawing a hotbar slot - and out of combat, standing still, it does not. A
        // recorded Reaper pull proves it: the very first line is the pre-pull Harpe with
        // "no snapshot" beside it and no suggestion at all, because nothing had ever asked.
        // The heads-up display is the whole accessibility feature and it was reading state
        // that only existed as a side effect of the game drawing an icon.
        //
        // Costs one snapshot a frame, which is what already happened whenever the hook was
        // live; both buttons share it and everything after the first call is a cache hit.
        //
        // Marked as our own work, because resolving asks the game about the actions in the
        // priority list and the host action is one of them - so this loop asks about our own
        // buttons, through our own hook, once a frame. Left uncounted that made the hook's
        // "the game asked N times" read exactly one per frame whether or not the hotbar was
        // drawing anything at all, which is the one thing the number exists to tell apart.
        using (ActionReplacer.OwnWork())
        {
            Resolve(RotationMode.SingleTarget);
            Resolve(RotationMode.Aoe);

            var extras = _job?.ExtraButtons.Count ?? 0;
            for (var i = 0; i < extras; i++)
                Resolve(i == 0 ? RotationMode.Extra1 : RotationMode.Extra2);
        }

        // Nothing else to poll: uses arrive from the UseActionLocation hook.
    }

    /// <summary>
    /// The hook's counters, with the last answer resolved to a name. Snapshotted into the
    /// recording at both ends so the log itself can say whether the game was asking about
    /// the buttons - the difference between an icon we never got told about and one the
    /// game simply did not redraw.
    /// </summary>
    private HookTraffic Traffic()
    {
        var last = _replacer.LastAnswer == 0
            ? null
            : _gameData.GetActionName(_replacer.LastAnswer) ?? $"action {_replacer.LastAnswer}";

        return new HookTraffic(
            _replacer.TimesAsked, _replacer.TimesAnswered, last, _replacer.TimesAskedByOurOwnWork);
    }

    /// <summary>
    /// Starts or stops recording a pull. Writes what was pressed beside what was suggested,
    /// so the two can be read against a known-good rotation afterwards - which is the only
    /// way to tell "the engine was wrong" from "the engine was right and got ignored".
    /// </summary>
    private void ToggleRecording()
    {
        if (_recorder.IsRecording)
        {
            var path = _recorder.Stop(_now, Traffic(), _session?.OpenerReportForLog);

            if (path is null)
            {
                Chat.Print("[One Two Punch] Recording stopped - nothing was cast, so nothing was written.");
                return;
            }

            Chat.Print($"[One Two Punch] Recording stopped. Written to {path}");
            Log.Information("One Two Punch: recording written to {Path}", path);
            return;
        }

        if (_job is null)
        {
            Chat.PrintError("[One Two Punch] Nothing to record - no supported job is running. /otp arm first.");
            return;
        }

        var version = PluginInterface.Manifest.AssemblyVersion.ToString();
        _recorder.Start(_job.Name, _frameSnapshot?.Level ?? 0, version, _now, Traffic());
        Chat.Print("[One Two Punch] Recording. Do your pull, then /otp record again to write it out.");
    }

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

        if (_recorder.IsRecording)
        {
            var path = _recorder.Stop(_now, Traffic(), _session?.OpenerReportForLog);
            if (path is not null)
                Chat.Print($"[One Two Punch] Recording written to {path}");
        }

        Deactivate();

        Chat.Print(
            $"[One Two Punch] Stopped, hotbars back to normal. "
            + $"Nested action lookups turned away while running: {_replacer.SuppressedReentrantCalls}.");
    }

    /// <summary>
    /// Installs the hook, now that there is definitely a character in the world. Returns
    /// false if the hook is not available, in which case the plugin stays inert and tries
    /// again next frame.
    /// </summary>
    private bool Activate()
    {
        if (!_replacer.Enable())
            return false;

        // Deliberately not fatal. Answering GetAdjustedActionId is what makes the key fire
        // the right ability; this only makes the slot draw it. If the icon hook cannot be
        // established the plugin still does its job, and the slot keeps its own art.
        _icons.Enable();

        // Only ever hooked when the player has asked for it, so a plugin nobody opted in to
        // never touches the function a keypress enters.
        if (_config.AetherialManipulationToTank)
            _partyTargeting.Enable();

        if (_config.LockMovementWhileCasting)
            _castLock.Enable();

        // Opener progress and the weave budget both depend on knowing what was pressed.
        _useWatcher.Enable(Interop);

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
        _icons.Disable();
        _partyTargeting.Disable();
        _castLock.Disable();
        _useWatcher.Disable();
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
        RefreshButtonForms();
    }

    private void OnActionUsed(uint actionId)
    {
        _session?.NotifyActionUsed(actionId);

        if (!_recorder.IsRecording)
            return;

        // Both buttons are resolved every frame, so "what was being suggested" has two
        // answers and only one of them belongs to the key that was pressed. The one whose
        // action actually went off is that key; without this an AoE pull reads as a wall of
        // disagreements against the single-target list, which is how a recorded Doom Spike
        // came out as "differs, suggested True Thrust".
        var suggestion = MatchingSuggestion(actionId)
                         ?? _lastSuggestion.GetValueOrDefault(RotationMode.SingleTarget)
                         ?? _lastSuggestion.GetValueOrDefault(RotationMode.Aoe);

        // Both names read out of the game's own sheet, so the two columns are directly
        // comparable by eye as well as by id.
        var suggested = suggestion is null
            ? null
            : _gameData.GetActionName(suggestion.Action.Id) ?? suggestion.Action.Name;

        _recorder.Cast(
            _now,
            _gameData.GetActionName(actionId) ?? $"action {actionId}",
            actionId,
            suggested,
            suggestion?.Action.Id ?? 0,
            suggestion?.Note,
            DescribeState());
    }

    /// <summary>The button whose suggestion is what actually went off, if either.</summary>
    private Suggestion? MatchingSuggestion(uint actionId)
    {
        foreach (var entry in _lastSuggestion)
        {
            if (entry.Value.Action.Id == actionId)
                return entry.Value;
        }

        return null;
    }

    /// <summary>
    /// A one-line picture of what the rules could see. Buffs and debuffs by name, because
    /// "why did it never suggest Thunder" is answered by whether Thunderhead was up, and
    /// that is invisible in a list of choices.
    /// </summary>
    private string DescribeState()
    {
        var s = _frameSnapshot;
        if (s is null)
            return "no snapshot";

        var text = new System.Text.StringBuilder();
        text.Append($"mp {s.Mp} | gcd {s.GcdRemaining:0.0}s");

        // Movement and enemy count are decisions the rules make that leave no other trace.
        // A recorded pull of somebody running while a Black Mage tried to hard-cast could
        // not answer whether the engine had even noticed the movement, which is the first
        // thing worth knowing.
        if (s.IsMoving)
            text.Append($" | moving {s.MovingFor:0.0}s");
        else
            text.Append($" | still {s.StillFor:0.0}s");

        text.Append($" | enemies {s.EnemiesInAoeRange}");

        // The job's own gauge, when it has one worth saying. Soul and Shroud, Astral Fire
        // and Polyglot - the numbers whole rotations turn on - left no trace anywhere before
        // this, so a log could show every buff and cooldown and still not say why a rule
        // declined.
        var gauge = _job?.DescribeGauge(s);
        if (!string.IsNullOrEmpty(gauge))
            text.Append(" | ").Append(gauge);

        // Whether the plugin thinks the fight has started. The opener waits for the pull
        // rather than burning itself before it, so a log that cannot say this cannot say
        // why the opener was holding.
        if (!s.InCombat)
            text.Append(" | out of combat");

        if (s.InDowntime)
            text.Append(" | downtime");

        if (s.SelfStatuses.Count > 0)
        {
            text.Append(" | on you: ");
            AppendStatuses(text, s.SelfStatuses, self: true);
        }

        if (s.TargetStatuses.Count > 0)
        {
            text.Append(" | on target: ");
            AppendStatuses(text, s.TargetStatuses, self: false);
        }

        return text.ToString();
    }

    private void AppendStatuses(System.Text.StringBuilder text, IReadOnlyList<StatusEntry> list, bool self)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0)
                text.Append(", ");

            var name = _gameData.GetStatusName(list[i].Id);
            text.Append(string.IsNullOrEmpty(name) ? $"#{list[i].Id}" : name);

            if (!float.IsPositiveInfinity(list[i].Remaining))
                text.Append($" {list[i].Remaining:0}s");
        }
    }

    // ---- The two buttons -------------------------------------------------

    /// <summary>
    /// Decides whether an action id is one of our two buttons. Everything else is passed
    /// straight through untouched, so the rest of the hotbar behaves exactly as it always did.
    /// </summary>
    private RotationMode? Classify(uint actionId)
    {
        if (!_config.Enabled || _job is null || _session is null)
            return null;

        return _buttonForms.TryGetValue(actionId, out var mode) ? mode : null;
    }

    /// <summary>
    /// Works out every id that counts as one of our buttons: the action the rotation names,
    /// and the upgraded form the player's hotbar carries once they have learned it.
    /// <para>
    /// Comparing against the named id alone made the plugin look completely dead on half
    /// the roster. A Machinist's button is Split Shot, but from level 54 the slot carries
    /// Heated Split Shot and that is the id the game asks about - so nothing matched,
    /// nothing was replaced, and every press produced the plain first combo hit. Red Mage
    /// (Jolt), Bard (Heavy Shot), Monk (Bootshine), Samurai (Hakaze) and Summoner (Ruin)
    /// are all the same shape; the jobs that worked are the ones whose starter never
    /// upgrades.
    /// </para>
    /// </summary>
    private void RefreshButtonForms()
    {
        _buttonForms.Clear();

        if (_job is null)
            return;

        Remember(_job.SingleTargetButton.Id, RotationMode.SingleTarget);
        Remember(_job.AoeButton.Id, RotationMode.Aoe);

        for (var i = 0; i < _job.ExtraButtons.Count; i++)
            Remember(_job.ExtraButtons[i].Host.Id, i == 0 ? RotationMode.Extra1 : RotationMode.Extra2);

        void Remember(uint id, RotationMode mode)
        {
            if (id == 0)
                return;

            _buttonForms[id] = mode;

            var upgraded = _replacer.CurrentFormOf(id);
            if (upgraded != 0 && upgraded != id)
                _buttonForms[upgraded] = mode;
        }
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
                _actionState.BeginFrame(_frameSnapshot.Level, _frameSnapshot.TargetId);
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

            case "record":
                ToggleRecording();
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
    /// Prints what the game is actually drawing in every slot that holds one of our buttons.
    /// <para>
    /// The counters above say whether the game asked us; only this says what it did with the
    /// reply. Three builds of "the right ability fires but the icon never changes" have gone
    /// unanswered because nothing could tell "the game never asked" from "the game asked, was
    /// told, and drew something else anyway".
    /// </para>
    /// </summary>
    private void PrintHotbarSlots()
    {
        if (_job is null)
        {
            Chat.Print("[One Two Punch] No supported job is running, so there are no buttons to look for.");
            return;
        }

        List<HotbarInspector.SlotState> slots;
        try
        {
            slots = HotbarInspector.FindOurSlots(id => Classify(id) is not null);
        }
        catch (Exception ex)
        {
            // A diagnostic is never worth taking the game with it.
            Log.Error(ex, "One Two Punch: could not read the hotbar");
            Chat.PrintError("[One Two Punch] Could not read the hotbar - see the Dalamud log.");
            return;
        }

        if (slots.Count == 0)
        {
            Chat.Print(
                "[One Two Punch] Neither button is on a hotbar. The icon cannot change on a slot "
                + $"that is not there - put {_job.SingleTargetButton.Name} on a bar.");
            return;
        }

        foreach (var slot in slots)
        {
            var assigned = Name(slot.Assigned);
            var showing = Name(slot.Showing);

            var verdict = slot.Showing == slot.Assigned
                ? "unchanged - the game is not taking our answer for this slot"
                : slot.Showing == _replacer.LastAnswer
                    ? "ours - the game has it, so the icon itself is the game's drawing"
                    : "somebody else's - neither the assigned action nor our last answer";

            Chat.Print(
                $"[One Two Punch] Hotbar {slot.Hotbar + 1} slot {slot.Slot + 1}: "
                + $"holds {assigned}, showing {showing} - {verdict}.");
        }
    }

    private string Name(uint actionId) =>
        actionId == 0 ? "nothing" : _gameData.GetActionName(actionId) ?? $"action {actionId}";

    /// <summary>
    /// Re-checks every supported job's action table against the game and prints the result.
    /// This is the thing to run after a patch, and the thing to paste into a bug report.
    /// </summary>
    private void PrintVerification()
    {
        Chat.Print(
            $"[One Two Punch] Loaded in {_loadMilliseconds}ms. "
            + $"Nested action lookups turned away: {_replacer.SuppressedReentrantCalls}.");

        // The hotbar draws its icons from the same function the replacement runs in, so a
        // slot whose icon never changes is either a slot the game is not asking about, or
        // one we are declining to answer. These two numbers say which.
        var lastAnswer = _replacer.LastAnswer == 0
            ? "nothing yet"
            : _gameData.GetActionName(_replacer.LastAnswer) ?? $"action {_replacer.LastAnswer}";

        Chat.Print(
            $"[One Two Punch] The game asked about your buttons {_replacer.TimesAsked} times, "
            + $"answered {_replacer.TimesAnswered}, last answer {lastAnswer}. "
            + "Both numbers climbing while you play means the hotbar is being told; "
            + "an icon that still will not change is the game's drawing, not ours.");
        Chat.Print(
            _icons.IsActive
                ? $"[One Two Punch] Slots holding your buttons have been drawn {_icons.TimesDrawn} times, "
                  + $"{_icons.TimesReplaced} of them showing the suggestion."
                : "[One Two Punch] The icon hook is not running, so the slot will keep its own art. "
                  + "The button still fires the right ability.");

        PrintHotbarSlots();
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
        _icons.Dispose();
        _partyTargeting.Dispose();
        _castLock.Dispose();
        _useWatcher.Dispose();
        _windows.RemoveAllWindows();
    }
}
