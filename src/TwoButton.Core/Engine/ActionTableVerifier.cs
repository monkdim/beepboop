using System.Text;
using TwoButton.Core.Jobs;
using TwoButton.Core.Model;

namespace TwoButton.Core.Engine;

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
}

public enum VerificationOutcome
{
    /// <summary>Seeded id matched the sheet.</summary>
    Ok,

    /// <summary>Seeded id was wrong and has been rebound by name.</summary>
    Repaired,

    /// <summary>Neither the id nor the name could be resolved. The job is unsafe to run.</summary>
    Unresolved,
}

public sealed record VerificationEntry(
    string Name,
    uint SeededId,
    uint ResolvedId,
    VerificationOutcome Outcome);

public sealed class VerificationReport(string jobName)
{
    private readonly List<VerificationEntry> _entries = [];

    public string JobName { get; } = jobName;

    public IReadOnlyList<VerificationEntry> Entries => _entries;

    public int RepairedCount => _entries.Count(e => e.Outcome == VerificationOutcome.Repaired);

    public int UnresolvedCount => _entries.Count(e => e.Outcome == VerificationOutcome.Unresolved);

    /// <summary>
    /// A job with an unresolvable action is disabled rather than run. Guessing would mean
    /// pressing the wrong button in a raid, which is worse than the plugin being off.
    /// </summary>
    public bool IsSafeToRun => UnresolvedCount == 0;

    internal void Add(VerificationEntry entry) => _entries.Add(entry);

    public string Summarise()
    {
        var sb = new StringBuilder();
        sb.Append(JobName).Append(": ").Append(_entries.Count).Append(" ids checked");

        if (RepairedCount > 0)
            sb.Append(", ").Append(RepairedCount).Append(" repaired by name");

        if (UnresolvedCount > 0)
            sb.Append(", ").Append(UnresolvedCount).Append(" UNRESOLVED - job disabled");

        foreach (var entry in _entries)
        {
            if (entry.Outcome == VerificationOutcome.Ok)
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
    public static VerificationReport Verify(IJobRotation job, IGameDataLookup lookup)
    {
        var report = new VerificationReport(job.Name);

        foreach (var action in job.AllActions)
        {
            var seeded = action.Id;
            var sheetName = lookup.GetActionName(seeded);

            if (string.Equals(sheetName, action.Name, StringComparison.OrdinalIgnoreCase))
            {
                action.Bind(seeded);
                report.Add(new VerificationEntry(action.Name, seeded, seeded, VerificationOutcome.Ok));
                continue;
            }

            var byName = lookup.FindActionIdByName(action.Name);
            if (byName is null)
            {
                report.Add(new VerificationEntry(action.Name, seeded, seeded, VerificationOutcome.Unresolved));
                continue;
            }

            action.Bind(byName.Value);
            report.Add(new VerificationEntry(
                action.Name, seeded, byName.Value, VerificationOutcome.Repaired));
        }

        foreach (var status in job.AllStatuses)
        {
            var seeded = status.Id;
            var sheetName = lookup.GetStatusName(seeded);

            if (string.Equals(sheetName, status.Name, StringComparison.OrdinalIgnoreCase))
            {
                status.Bind(seeded);
                report.Add(new VerificationEntry(status.Name, seeded, seeded, VerificationOutcome.Ok));
                continue;
            }

            var byName = lookup.FindStatusIdByName(status.Name);
            if (byName is null)
            {
                report.Add(new VerificationEntry(status.Name, seeded, seeded, VerificationOutcome.Unresolved));
                continue;
            }

            status.Bind(byName.Value);
            report.Add(new VerificationEntry(
                status.Name, seeded, byName.Value, VerificationOutcome.Repaired));
        }

        return report;
    }
}
