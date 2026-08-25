using System.Text;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Engine;

/// <summary>
/// Read-only view of the game's own Action and Status sheets. Implemented over Lumina in
/// the plugin, and by a stub in the tests.
/// </summary>
public interface IGameDataLookup
{
    string? GetActionName(uint actionId);

    uint? FindActionIdByName(string name);

    string? GetStatusName(uint statusId);

    uint? FindStatusIdByName(string name);

    /// <summary>
    /// Whether the id belongs to something a player can actually press. Used to tell a table
    /// name that is merely a label from an id that points at nothing real.
    /// </summary>
    bool IsPlayerAction(uint actionId);
}

public enum VerificationOutcome
{
    /// <summary>Seeded id matched the sheet.</summary>
    Ok,

    /// <summary>Seeded id was wrong and has been rebound by name.</summary>
    Repaired,

    /// <summary>
    /// The id resolves to a real entry, but the game calls it something other than the name
    /// in the table. The id is kept and the game's name adopted for display.
    /// <para>
    /// This is the normal state for entries whose table name is a label rather than a name.
    /// The action tables are generated from BossMod, which has to tell apart several things
    /// the game gives the same name: ids 18873, 18874 and 18875 are all "Fuma Shuriken" to
    /// the game, and BossMod calls them Fuma Ten, Fuma Chi and Fuma Jin so they can be
    /// referred to separately. Ten II is "Ten", TCJ Raiton is "Raiton", and The Warden's
    /// Paean loses its "The". None of those are wrong ids; they are the same id under a
    /// working name.
    /// </para>
    /// </summary>
    Aliased,

    /// <summary>Neither the id nor the name could be resolved. The job is unsafe to run.</summary>
    Unresolved,
}

public sealed record VerificationEntry(
    string Name,
    uint SeededId,
    uint ResolvedId,
    VerificationOutcome Outcome,
    bool IsAction);

public sealed class VerificationReport(string jobName)
{
    private readonly List<VerificationEntry> _entries = [];

    public string JobName { get; } = jobName;

    public IReadOnlyList<VerificationEntry> Entries => _entries;

    public int RepairedCount => _entries.Count(e => e.Outcome == VerificationOutcome.Repaired);

    /// <summary>Entries kept under the game's own name. Harmless - see the outcome's note.</summary>
    public int AliasedCount => _entries.Count(e => e.Outcome == VerificationOutcome.Aliased);

    public int UnresolvedCount => _entries.Count(e => e.Outcome == VerificationOutcome.Unresolved);

    /// <summary>Unresolved entries that are actions rather than statuses.</summary>
    public int UnresolvedActionCount =>
        _entries.Count(e => e.Outcome == VerificationOutcome.Unresolved && e.IsAction);

    /// <summary>
    /// A job with an unresolvable <em>action</em> is disabled rather than run: the id would
    /// stay at whatever was seeded, and pressing the wrong ability in a raid is worse than
    /// the plugin being off.
    /// <para>
    /// An unresolvable <em>status</em> is only logged. A status that cannot be found simply
    /// reads as absent, so the rule that depends on it declines to fire - the job degrades
    /// by one condition instead of switching off entirely, which is the right trade for
    /// something as harmless as a stray entry in the table.
    /// </para>
    /// </summary>
    public bool IsSafeToRun => UnresolvedActionCount == 0;

    internal void Add(VerificationEntry entry) => _entries.Add(entry);

    public string Summarise()
    {
        var sb = new StringBuilder();
        sb.Append(JobName).Append(": ").Append(_entries.Count).Append(" ids checked");

        if (RepairedCount > 0)
            sb.Append(", ").Append(RepairedCount).Append(" repaired by name");

        if (AliasedCount > 0)
            sb.Append(", ").Append(AliasedCount).Append(" under the game's own name");

        if (UnresolvedCount > 0)
        {
            sb.Append(", ").Append(UnresolvedCount).Append(" unresolved");

            if (UnresolvedActionCount > 0)
                sb.Append(" (").Append(UnresolvedActionCount).Append(" action(s) - job disabled)");
        }

        foreach (var entry in _entries)
        {
            // Aliases are expected and numerous; listing them buries the real problems.
            if (entry.Outcome is VerificationOutcome.Ok or VerificationOutcome.Aliased)
                continue;

            sb.AppendLine();
            sb.Append("  ")
              .Append(entry.Outcome == VerificationOutcome.Repaired ? "repaired " : "UNRESOLVED ")
              .Append(entry.Name)
              .Append(": seeded ")
              .Append(entry.SeededId);

            if (entry.Outcome == VerificationOutcome.Repaired)
                sb.Append(" -> ").Append(entry.ResolvedId);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Checks every id a job can suggest against the game's own sheets before the job is
/// allowed to run.
/// <para>
/// Hard-coded ids are the most likely thing in this whole plugin to be wrong: they get
/// shuffled by patches and mistyped by contributors, and a wrong one is invisible until it
/// casts the wrong ability in a raid. So they are treated as a guess. The name is the real
/// identity; the id is repaired to match it at startup, and a job whose names cannot be
/// resolved at all is switched off with a message rather than run on hope.
/// </para>
/// </summary>
public static class ActionTableVerifier
{
    /// <summary>
    /// Compares names ignoring case, punctuation and spacing, so a table that spells an
    /// action <c>HeavensThrust</c> still matches the sheet's <c>Heavens' Thrust</c>, and
    /// <c>Fang And Claw</c> matches <c>Fang and Claw</c>. Without this every apostrophe and
    /// lowercase connector in the game's naming would read as a mismatch.
    /// </summary>
    private static bool NamesMatch(string? sheetName, string tableName)
    {
        if (sheetName is null)
            return false;

        if (string.Equals(sheetName, tableName, StringComparison.OrdinalIgnoreCase))
            return true;

        return Normalise(sheetName).Equals(Normalise(tableName), StringComparison.Ordinal);
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

    public static VerificationReport Verify(IJobRotation job, IGameDataLookup lookup)
    {
        var report = new VerificationReport(job.Name);

        foreach (var action in job.AllActions)
        {
            var seeded = action.Id;
            var sheetName = lookup.GetActionName(seeded);

            if (NamesMatch(sheetName, action.Name))
            {
                action.Bind(seeded);
                report.Add(new VerificationEntry(action.Name, seeded, seeded, VerificationOutcome.Ok, true));
                continue;
            }

            // Repair by name first, so a genuinely mistyped id still gets corrected.
            var byName = lookup.FindActionIdByName(action.Name);
            if (byName is not null)
            {
                action.Bind(byName.Value);
                report.Add(new VerificationEntry(
                    action.Name, seeded, byName.Value, VerificationOutcome.Repaired, true));
                continue;
            }

            // The name is not in the sheet at all, but the id points at a real player action.
            // That is a table name being a label rather than a name - see VerificationOutcome
            // .Aliased. Keep the id; it is the identity here, and it is not a reason to
            // switch a whole job off.
            if (lookup.IsPlayerAction(seeded))
            {
                action.Bind(seeded);
                report.Add(new VerificationEntry(
                    action.Name, seeded, seeded, VerificationOutcome.Aliased, true));
                continue;
            }

            report.Add(new VerificationEntry(action.Name, seeded, seeded, VerificationOutcome.Unresolved, true));
        }

        foreach (var status in job.AllStatuses)
        {
            var seeded = status.Id;
            var sheetName = lookup.GetStatusName(seeded);

            if (NamesMatch(sheetName, status.Name))
            {
                status.Bind(seeded);
                report.Add(new VerificationEntry(status.Name, seeded, seeded, VerificationOutcome.Ok, false));
                continue;
            }

            var byName = lookup.FindStatusIdByName(status.Name);
            if (byName is not null)
            {
                status.Bind(byName.Value);
                report.Add(new VerificationEntry(
                    status.Name, seeded, byName.Value, VerificationOutcome.Repaired, false));
                continue;
            }

            // Same as for actions: a real status under a working name.
            if (!string.IsNullOrEmpty(sheetName))
            {
                status.Bind(seeded);
                report.Add(new VerificationEntry(
                    status.Name, seeded, seeded, VerificationOutcome.Aliased, false));
                continue;
            }

            report.Add(new VerificationEntry(status.Name, seeded, seeded, VerificationOutcome.Unresolved, false));
        }

        return report;
    }
}
