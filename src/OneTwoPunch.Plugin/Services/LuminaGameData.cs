using System.Text;
using Dalamud.Plugin.Services;
using OneTwoPunch.Core.Engine;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// <see cref="IGameDataLookup"/> over the game's own Excel sheets. This is what lets a
/// wrong hard-coded id be caught and repaired at startup rather than mis-cast in a raid.
/// </summary>
public sealed class LuminaGameData : IGameDataLookup
{
    private readonly IDataManager _data;

    // Reverse name lookups, keyed by a normalised name so a table spelling "HeavensThrust"
    // still finds "Heavens' Thrust". Matches ActionTableVerifier.Normalise.
    //
    // Built on first use, not at construction. Every row of the Action sheet has to be read
    // and have its name extracted to build one, which is tens of thousands of string
    // allocations on the game thread - and it is only ever needed to *repair* an id whose
    // name did not match, which is rare and usually never. Paying that at load froze the
    // game while the plugin installed.
    private Dictionary<string, uint>? _actionsByName;
    private Dictionary<string, uint>? _statusesByName;

    // Icons are asked for once per heads-up row per frame, so they are remembered.
    private readonly Dictionary<uint, uint> _iconCache = [];

    public LuminaGameData(IDataManager data) => _data = data;

    public string? GetActionName(uint actionId)
    {
        var row = _data.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
        return row?.Name.ExtractText();
    }

    public uint? FindActionIdByName(string name)
    {
        _actionsByName ??= BuildActionIndex();
        return _actionsByName.TryGetValue(Normalise(name), out var id) ? id : null;
    }

    private Dictionary<string, uint> BuildActionIndex()
    {
        var index = new Dictionary<string, uint>();
        var sheet = _data.GetExcelSheet<LuminaAction>();
        if (sheet is null)
            return index;

        foreach (var row in sheet)
        {
            // Player actions only, and the lowest id wins for a duplicated name, so we land
            // on the real action rather than an NPC copy of it.
            if (!row.IsPlayerAction)
                continue;

            var key = Normalise(row.Name.ExtractText());
            if (key.Length > 0)
                index.TryAdd(key, row.RowId);
        }

        return index;
    }

    public string? GetStatusName(uint statusId)
    {
        var row = _data.GetExcelSheet<LuminaStatus>()?.GetRowOrDefault(statusId);
        return row?.Name.ExtractText();
    }

    public uint? FindStatusIdByName(string name)
    {
        _statusesByName ??= BuildStatusIndex();
        return _statusesByName.TryGetValue(Normalise(name), out var id) ? id : null;
    }

    private Dictionary<string, uint> BuildStatusIndex()
    {
        var index = new Dictionary<string, uint>();
        var sheet = _data.GetExcelSheet<LuminaStatus>();
        if (sheet is null)
            return index;

        foreach (var row in sheet)
        {
            var key = Normalise(row.Name.ExtractText());
            if (key.Length > 0)
                index.TryAdd(key, row.RowId);
        }

        return index;
    }

    private static string Normalise(string value)
    {
        var buffer = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
                buffer.Append(char.ToLowerInvariant(c));
        }

        return buffer.ToString();
    }

    /// <summary>Icon id for an action, for the heads-up display. Zero when unknown.</summary>
    public uint GetActionIcon(uint actionId)
    {
        if (_iconCache.TryGetValue(actionId, out var cached))
            return cached;

        var row = _data.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
        var icon = row?.Icon ?? 0u;
        _iconCache[actionId] = icon;
        return icon;
    }
}
