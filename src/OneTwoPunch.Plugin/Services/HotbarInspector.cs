using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// Reads what the game itself currently believes each hotbar slot should draw.
/// <para>
/// This exists because "the button fires the right ability but the icon never changes" had
/// gone three builds without an answer, and every instrument so far has measured the wrong
/// thing. The hook counters say whether the game asked us; they cannot say what the game did
/// with the reply. A slot carries both the action assigned to it and the action it is
/// actually showing, and comparing those two against our own suggestion splits the problem
/// three ways with nothing left over:
/// </para>
/// <list type="bullet">
/// <item>showing our suggestion - the game took the answer, so the icon is a drawing matter
/// and the two are probably just similar-looking Reaper art.</item>
/// <item>showing the assigned action unchanged - the game asked nobody, or discarded the
/// reply.</item>
/// <item>showing something that is neither - somebody else is answering for that slot.</item>
/// </list>
/// <para>
/// Strictly read-only. Nothing here writes to the hotbar.
/// </para>
/// </summary>
public sealed unsafe class HotbarInspector
{
    /// <summary>What one slot holds and what it is drawing.</summary>
    /// <param name="Hotbar">Zero-based hotbar index; 0-9 are the normal bars.</param>
    /// <param name="Slot">Zero-based slot within that bar.</param>
    /// <param name="Assigned">The action the player dragged onto the slot.</param>
    /// <param name="Showing">The action the game has resolved it to for display.</param>
    public readonly record struct SlotState(int Hotbar, int Slot, uint Assigned, uint Showing);

    /// <summary>
    /// Every action slot whose assigned action <paramref name="isOurs"/> claims, with what the
    /// game is drawing there. Empty means the buttons are not on a hotbar at all, which is its
    /// own answer.
    /// </summary>
    public static List<SlotState> FindOurSlots(Func<uint, bool> isOurs)
    {
        var found = new List<SlotState>();

        var module = RaptureHotbarModule.Instance();
        if (module is null)
            return found;

        // 0-9 are the normal hotbars, 10-17 the cross hotbars. Slots are asked for by index
        // rather than walked as an array so a shape change in the game's structure is a null
        // return rather than a read off the end of it.
        for (var bar = 0; bar < 18; bar++)
        {
            for (var slot = 0; slot < 16; slot++)
            {
                var s = module->GetSlotById((uint)bar, (uint)slot);
                if (s is null)
                    continue;

                if (s->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
                    continue;

                if (!isOurs(s->CommandId))
                    continue;

                found.Add(new SlotState(bar, slot, s->CommandId, s->ApparentActionId));
            }
        }

        return found;
    }
}
