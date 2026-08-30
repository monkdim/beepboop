using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Plugin;

/// <summary>
/// The second draw path, and the one that actually works.
/// <para>
/// <see cref="HotbarIconReplacer"/> hooks <c>RaptureHotbarModule.GetSlotAppearance</c>, whose
/// own documentation says it runs "every frame for every visible hotbar slot". It does not.
/// A recorded twelve minute pull is unambiguous: <c>the hook was entered 0 times, 0 of them
/// with no slot and 0 while already inside itself</c>, with the address it was installed at
/// printed beside it and the action hook answering 433,739 times in the same fight. The hook
/// is on a real resolved function that the game never calls, so nothing done inside it could
/// ever have changed an icon.
/// </para>
/// <para>
/// So the icon is written where the game reads it from instead. ClientStructs documents the
/// pair of fields for exactly this: <c>ApparentActionId</c> and <c>ApparentSlotType</c> exist
/// so that "a hotbar slot can have the appearance of one action, but in reality trigger a
/// different action", and <c>IconId</c> is loaded from them by <c>LoadIconId</c>. They are the
/// display half of a slot and nothing else.
/// </para>
/// <para>
/// <c>CommandId</c> and <c>CommandType</c> - the id a keypress actually executes - are never
/// touched here. Pressing the key still enters <c>GetAdjustedActionId</c> and still gets the
/// answer it got before, so the promise the whole plugin rests on is unchanged: one keypress
/// is exactly one action, and no input is ever sent. What changes is only which picture the
/// slot draws while you look at it.
/// </para>
/// <para>
/// Everything written is remembered and put back. Disarming, switching job, moving the action
/// off the bar, or unloading the plugin all restore the slot to the appearance it had before
/// this ever touched it.
/// </para>
/// </summary>
public sealed unsafe class HotbarIconPainter : IDisposable
{
    private readonly IPluginLog _log;

    private readonly Func<uint, RotationMode?> _classify;
    private readonly Func<RotationMode, Suggestion?> _resolve;

    /// <summary>Standard hotbars are 0 to 9 and cross hotbars 10 to 17.</summary>
    private const uint HotbarCount = 18;

    /// <summary>Every hotbar holds sixteen slots.</summary>
    private const uint SlotCount = 16;

    /// <summary>
    /// What a slot looked like before it was painted, and what was painted onto it.
    /// <para>
    /// The written id is kept as well as the original so a slot the game or the player has
    /// changed underneath us can be told from one still carrying our picture. Without that,
    /// the second paint would save our own value as the "original" and the restore would put
    /// back a suggestion instead of the action the player put there.
    /// </para>
    /// </summary>
    private readonly record struct Painted(
        RaptureHotbarModule.HotbarSlotType SlotType,
        uint ActionId,
        uint Wrote);

    private readonly Dictionary<(uint Hotbar, uint Slot), Painted> _painted = [];

    /// <summary>
    /// Whether refreshing the cost text still works. It is resolved by signature like every
    /// other member function and is the least important thing here, so a failure switches it
    /// off on its own rather than taking the icon down with it.
    /// </summary>
    private bool _costs = true;

    /// <summary>
    /// A few of the action ids sitting on real action slots that were not recognised as ours.
    /// <para>
    /// The old hook had this and it was never reachable, because the hook never ran. Here it
    /// is: a level 80 duty read the hotbar 48 million times and recognised 4,110 slots, and
    /// the question "which id was actually in the slot then" is the one that settles why.
    /// </para>
    /// </summary>
    private readonly HashSet<uint> _unrecognised = [];

    private const int UnrecognisedSampleSize = 12;

    private long _scanned;
    private long _ours;
    private long _painting;
    private bool _unavailable;

    public HotbarIconPainter(
        IPluginLog log,
        Func<uint, RotationMode?> classify,
        Func<RotationMode, Suggestion?> resolve)
    {
        _log = log;
        _classify = classify;
        _resolve = resolve;
    }

    /// <summary>Slots looked at, ours or not - so "none were ours" reads differently
    /// from "the module was never there".</summary>
    public long SlotsScanned => _scanned;

    /// <summary>Slots found holding one of our buttons.</summary>
    public long SlotsThatAreOurs => _ours;

    /// <summary>Times a slot was actually given a new picture.</summary>
    public long TimesPainted => _painting;

    /// <summary>How many slots are currently carrying a suggestion rather than their own art.</summary>
    public int SlotsHeld => _painted.Count;

    /// <summary>A sample of action ids found on the bar that were not one of our buttons.</summary>
    public IReadOnlyCollection<uint> UnrecognisedIds => _unrecognised;

    /// <summary>
    /// One pass over every hotbar slot. Called once a frame, after both buttons have been
    /// resolved, so every lookup here is a cache hit rather than a fresh snapshot.
    /// </summary>
    public void Paint()
    {
        if (_unavailable)
            return;

        try
        {
            var module = RaptureHotbarModule.Instance();
            if (module is null)
                return;

            for (uint bar = 0; bar < HotbarCount; bar++)
            {
                for (uint index = 0; index < SlotCount; index++)
                {
                    var slot = module->Hotbars[(int)bar].GetHotbarSlot(index);
                    if (slot is null)
                        continue;

                    _scanned++;
                    PaintSlot(slot, (bar, index));
                }
            }
        }
        catch (Exception ex)
        {
            // The icon is worth having, but never at the cost of the game. If anything here
            // throws, the slot keeps its own art and this stops trying.
            _unavailable = true;
            _log.Error(ex, "One Two Punch: painting the hotbar failed; icons will not follow.");
        }
    }

    private void PaintSlot(RaptureHotbarModule.HotbarSlot* slot, (uint, uint) key)
    {
        // Only real actions. A macro, an item or an emote is somebody else's slot even if the
        // id underneath happens to collide with one of ours.
        if (slot->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
        {
            Restore(slot, key);
            return;
        }

        // CommandId is what the player put on the bar and is the id that survives our own
        // painting - ApparentActionId is not, because we are the thing overwriting it.
        // OriginalApparentActionId is the game's own "base action for display", which is what
        // an upgraded starter reads as.
        var mode = _classify(slot->CommandId) ?? _classify(slot->OriginalApparentActionId);
        if (mode is null)
        {
            if (_unrecognised.Count < UnrecognisedSampleSize && slot->CommandId != 0)
                _unrecognised.Add(slot->CommandId);

            Restore(slot, key);
            return;
        }

        _ours++;

        var suggestion = _resolve(mode.Value);
        if (suggestion is null || suggestion.Action.Id == 0)
            return;

        // Remember the slot's own appearance the first time it is touched, and again whenever
        // it is carrying something other than what we last wrote - the player swapping the
        // action on the bar, or the game recomputing it.
        if (!_painted.TryGetValue(key, out var was) || slot->ApparentActionId != was.Wrote)
        {
            was = new Painted(slot->ApparentSlotType, slot->ApparentActionId, 0);
        }

        // Already showing it. Reloading the icon every frame for a picture that has not
        // changed is work the game does not need.
        if (slot->ApparentActionId == suggestion.Action.Id
            && slot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.Action)
        {
            _painted[key] = was with { Wrote = suggestion.Action.Id };
            return;
        }

        slot->ApparentSlotType = RaptureHotbarModule.HotbarSlotType.Action;
        slot->ApparentActionId = suggestion.Action.Id;
        slot->LoadIconId();
        RefreshCost(slot);

        _painted[key] = was with { Wrote = suggestion.Action.Id };
        _painting++;
    }

    /// <summary>Puts one slot back the way it was, if this ever touched it.</summary>
    private void Restore(RaptureHotbarModule.HotbarSlot* slot, (uint, uint) key)
    {
        if (!_painted.TryGetValue(key, out var was))
            return;

        _painted.Remove(key);

        // Only if it is still carrying what we wrote. If something else has changed it since,
        // that is the truth now and putting an older value back would be the vandalism this
        // is trying to avoid.
        if (slot->ApparentActionId != was.Wrote)
            return;

        slot->ApparentSlotType = was.SlotType;
        slot->ApparentActionId = was.ActionId;
        slot->LoadIconId();
        RefreshCost(slot);
    }

    /// <summary>The mana cost under the icon, which is worth having and not worth failing for.</summary>
    private void RefreshCost(RaptureHotbarModule.HotbarSlot* slot)
    {
        if (!_costs)
            return;

        try
        {
            slot->LoadCostDataForSlot();
        }
        catch (Exception ex)
        {
            _costs = false;
            _log.Warning(ex, "One Two Punch: cost text will not follow the icon.");
        }
    }

    /// <summary>
    /// Puts every painted slot back. Disarming, leaving the world and unloading all come
    /// through here, so a slot is never left holding a suggestion with nothing driving it.
    /// </summary>
    public void RestoreAll()
    {
        if (_painted.Count == 0)
            return;

        try
        {
            var module = RaptureHotbarModule.Instance();
            if (module is null)
            {
                // The hotbars are gone - a logout, or the game shutting down. There is nothing
                // left to put back and the slots are rebuilt from the save on the way in.
                _painted.Clear();
                return;
            }

            // Copied first, because Restore removes from the dictionary as it goes.
            var keys = new (uint Hotbar, uint Slot)[_painted.Count];
            _painted.Keys.CopyTo(keys, 0);

            foreach (var key in keys)
            {
                var slot = module->Hotbars[(int)key.Hotbar].GetHotbarSlot(key.Slot);
                if (slot is not null)
                    Restore(slot, key);
            }

            _painted.Clear();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "One Two Punch: could not put the hotbar icons back.");
            _painted.Clear();
        }
    }

    public void Dispose() => RestoreAll();
}
