using FFXIVClientStructs.FFXIV.Client.Game;
using TwoButton.Core.Jobs;

namespace TwoButton.Plugin.Services;

/// <summary>
/// Notices when one of the job's actions actually goes off, so the engine's weave budget
/// and opener stay in step with what the player really pressed.
/// <para>
/// This watches cooldowns rather than hooking UseAction. A hook would be more direct, but
/// its signature moves with every patch, and a broken hook here would break the plugin. A
/// cooldown that was zero last frame and is not zero now is a use, and that is true in
/// every patch.
/// </para>
/// </summary>
public sealed unsafe class ActionUseWatcher
{
    private readonly Dictionary<uint, float> _lastRemaining = [];
    private readonly List<uint> _tracked = [];

    public event Action<uint>? ActionUsed;

    public void Track(IJobRotation job)
    {
        _tracked.Clear();
        _lastRemaining.Clear();

        foreach (var action in job.AllActions)
            _tracked.Add(action.Id);
    }

    public void Tick()
    {
        var manager = ActionManager.Instance();
        if (manager is null)
            return;

        foreach (var actionId in _tracked)
        {
            var recast = manager->GetRecastTime(ActionType.Action, actionId);
            var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, actionId);
            var remaining = Math.Max(0f, recast - elapsed);

            if (_lastRemaining.TryGetValue(actionId, out var previous))
            {
                // A cooldown that jumped up is a use. The threshold keeps a recast that was
                // simply ticking from registering.
                if (remaining > previous + 0.05f)
                    ActionUsed?.Invoke(actionId);
            }

            _lastRemaining[actionId] = remaining;
        }
    }

    public void Reset() => _lastRemaining.Clear();
}
