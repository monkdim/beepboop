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
    private readonly Dictionary<string, uint> _actionsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, uint> _statusesByName = new(StringComparer.OrdinalIgnoreCase);

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

                if (!_actionsByName.ContainsKey(name))
                    _actionsByName[name] = row.RowId;
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

                if (!_statusesByName.ContainsKey(name))
                    _statusesByName[name] = row.RowId;
            }
        }
    }

    public string? GetActionName(uint actionId)
    {
        var row = _data.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
        return row?.Name.ExtractText();
    }

    public uint? FindActionIdByName(string name) =>
        _actionsByName.TryGetValue(name, out var id) ? id : null;

    public string? GetStatusName(uint statusId)
    {
        var row = _data.GetExcelSheet<LuminaStatus>()?.GetRowOrDefault(statusId);
        return row?.Name.ExtractText();
    }

    public uint? FindStatusIdByName(string name) =>
        _statusesByName.TryGetValue(name, out var id) ? id : null;

    /// <summary>Icon id for an action, for the heads-up display. Zero when unknown.</summary>
    public uint GetActionIcon(uint actionId)
    {
        var row = _data.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
        return row?.Icon ?? 0u;
    }
}
