using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace TwoButton.Plugin.Services;

/// <summary>One potion the player can choose from, discovered from the game's own item sheet.</summary>
public sealed record PotionOption(uint ItemId, string Name);

/// <summary>
/// Tracks whether the configured potion is usable right now.
/// <para>
/// The list of potions is built from the game's Item sheet rather than hard-coded, so a new
/// tier arriving in a patch shows up without a plugin update - and there are no item ids in
/// this repository to go stale.
/// </para>
/// </summary>
public sealed unsafe class PotionTracker
{
    /// <summary>HQ items are addressed as id + this offset when asking about cooldowns.</summary>
    private const uint HqOffset = 1_000_000;

    private readonly List<PotionOption> _options = [];

    public PotionTracker(IDataManager data)
    {
        var items = data.GetExcelSheet<LuminaItem>();
        if (items is null)
            return;

        foreach (var row in items)
        {
            var name = row.Name.ExtractText();
            if (string.IsNullOrEmpty(name))
                continue;

            // Battle potions across expansions: Gemdraught (Dawntrail), Tincture (earlier).
            if (!name.Contains("Gemdraught", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Tincture", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _options.Add(new PotionOption(row.RowId, name));
        }

        // Newest tiers last in the sheet; show them first.
        _options.Reverse();
    }

    public IReadOnlyList<PotionOption> Options => _options;

    public string NameOf(uint itemId)
    {
        foreach (var option in _options)
        {
            if (option.ItemId == itemId)
                return option.Name;
        }

        return itemId == 0 ? "(none selected)" : $"item {itemId}";
    }

    /// <summary>
    /// Seconds until the potion can be used. Zero when it is ready, and
    /// <see cref="float.MaxValue"/> when none is configured or none is held.
    /// </summary>
    public float CooldownRemaining(uint itemId, bool preferHq)
    {
        if (itemId == 0)
            return float.MaxValue;

        var manager = ActionManager.Instance();
        if (manager is null)
            return float.MaxValue;

        var inventory = InventoryManager.Instance();
        if (inventory is null)
            return float.MaxValue;

        // Prefer HQ if the player is carrying any, since that is what they mean to use.
        var useHq = preferHq && inventory->GetInventoryItemCount(itemId, true) > 0;
        if (!useHq && inventory->GetInventoryItemCount(itemId) == 0
            && inventory->GetInventoryItemCount(itemId, true) == 0)
        {
            return float.MaxValue;
        }

        var resolved = useHq ? itemId + HqOffset : itemId;

        var recast = manager->GetRecastTime(ActionType.Item, resolved);
        var elapsed = manager->GetRecastTimeElapsed(ActionType.Item, resolved);

        return Math.Max(0f, recast - elapsed);
    }
}
