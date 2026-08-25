using System.Text;
using Dalamud.Plugin.Services;
using TwoButton.Core.Engine;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace TwoButton.Plugin.Services;

/// <summary>
/// <see cref="IGameDataLookup"/> over the game's own Excel sheets. This is what lets a
/// wrong hard-coded id be caught and repaired at startup rather than mis-cast in a raid.
/// </summary>
public sealed class LuminaGameData : IGameDataLookup
{
    private readonly IDataManager _data;
    // Keyed by a normalised name so a table spelling "HeavensThrust" still finds
    // "Heavens' Thrust". Matches ActionTableVerifier.Normalise.
    private readonly Dictionary<string, uint> _actionsByName = [];
    private readonly Dictionary<string, uint> _statusesByName = [];

    public LuminaGameData(IDataManager data)
    {
        _data = data;

        var actions = data.GetExcelSheet<LuminaAction>();
        if (actions is not null)
        {
            foreach (var row in actions)
            {
                var name = row.Name.ExtractText();
                if (string.IsNullOrEmpty(name))
                    continue;

                // Player actions only, and keep the lowest id for a duplicated name so we
                // land on the real action rather than an NPC copy of it.
                if (!row.IsPlayerAction)
                    continue;

                var key = Normalise(name);
                if (key.Length > 0 && !_actionsByName.ContainsKey(key))
                    _actionsByName[key] = row.RowId;
            }
        }

        var statuses = data.GetExcelSheet<LuminaStatus>();
        if (statuses is not null)
        {
            foreach (var row in statuses)
            {
                var name = row.Name.ExtractText();
                if (string.IsNullOrEmpty(name))
                    continue;

                var key = Normalise(name);
                if (key.Length > 0 && !_statusesByName.ContainsKey(key))
                    _statusesByName[key] = row.RowId;
            }
        }
    }

    public string? GetActionName(uint actionId)
    {
        var row = _data.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
        return row?.Name.ExtractText();
    }

    public uint? FindActionIdByName(string name) =>
        _actionsByName.TryGetValue(Normalise(name), out var id) ? id : null;

    public string? GetStatusName(uint statusId)
    {
        var row = _data.GetExcelSheet<LuminaStatus>()?.GetRowOrDefault(statusId);
        return row?.Name.ExtractText();
    }

    public uint? FindStatusIdByName(string name) =>
        _statusesByName.TryGetValue(Normalise(name), out var id) ? id : null;

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
        var row = _data.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
        return row?.Icon ?? 0u;
    }
}
