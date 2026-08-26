using System.Text;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Plugin.Services;

/// <summary>
/// Records what the plugin suggested and what the player actually pressed, so a real pull
/// can be read back against a known-good rotation.
/// <para>
/// The interesting column is the disagreement. A list of casts alone only shows what
/// happened; putting the suggestion beside it shows whether the engine was wrong, or was
/// right and got ignored, which are opposite bugs and cannot be told apart afterwards
/// otherwise.
/// </para>
/// </summary>
public sealed class SessionRecorder
{
    private readonly List<string> _lines = [];
    private readonly StringBuilder _line = new();

    private double _startedAt;
    private int _casts;
    private int _followed;

    /// <summary>
    /// The hook counters as they stood when recording started, so the footer can report the
    /// traffic during this pull rather than since the plugin loaded.
    /// </summary>
    private HookTraffic _trafficAtStart;

    public bool IsRecording { get; private set; }

    public int Casts => _casts;

    public void Start(string job, byte level, string version, double now, HookTraffic traffic)
    {
        _lines.Clear();
        _casts = 0;
        _followed = 0;
        _startedAt = now;
        _trafficAtStart = traffic;
        IsRecording = true;

        _lines.Add($"One Two Punch {version} - {job} level {level}");
        _lines.Add("");
        _lines.Add("  time     cast                            suggested                       ");
        _lines.Add("  -------- ------------------------------- ------------------------------- ");
    }

    /// <summary>Notes an action the player actually used, beside what was being suggested.</summary>
    public void Cast(
        double now,
        string cast,
        uint castId,
        string? suggested,
        uint suggestedId,
        string? note,
        string? state = null)
    {
        if (!IsRecording)
            return;

        _casts++;

        // By id, not by name. The two names come from different places - the cast's from
        // the game's sheet, the suggestion's from our own table - and the game writes
        // "Heavens' Thrust" where the table says "Heavens Thrust". Comparing the strings
        // reported three disagreements in a clean Dragoon pull that were the same action.
        var agreed = suggested is not null && castId == suggestedId;

        if (agreed)
            _followed++;

        _line.Clear();
        _line.Append("  ")
             .Append(Stamp(now - _startedAt).PadRight(9))
             .Append(Trim(cast, 31).PadRight(32))
             .Append(Trim(suggested ?? "-", 31).PadRight(32));

        // Only mark the disagreements. Marking every line would bury them.
        if (!agreed)
            _line.Append("  <-- differs");

        if (!string.IsNullOrEmpty(note))
            _line.Append("   (").Append(note).Append(')');

        _lines.Add(_line.ToString());

        // The state the rules were reading. Without it a disagreement only says the engine
        // chose differently, not what it was looking at when it did - and a condition that
        // is never true is invisible from the choices alone.
        if (!string.IsNullOrEmpty(state))
            _lines.Add("             " + state);
    }

    public void Note(double now, string text)
    {
        if (IsRecording)
            _lines.Add($"  {Stamp(now - _startedAt).PadRight(9)}{text}");
    }

    /// <summary>Stops and writes the log out. Returns the path, or null if nothing was recorded.</summary>
    public string? Stop(double now, HookTraffic traffic)
    {
        if (!IsRecording)
            return null;

        IsRecording = false;

        if (_casts == 0)
            return null;

        _lines.Add("");
        _lines.Add($"  {_casts} casts over {Stamp(now - _startedAt)}, "
                   + $"{_followed} matched the suggestion, {_casts - _followed} did not.");

        // "The button fires the right ability but the icon never changes" is otherwise
        // impossible to diagnose from a recording. The hotbar draws its icons from the same
        // function the replacement runs in, so these two numbers separate the two causes:
        // near zero asks means the game is not asking about that slot at all, while asks
        // climbing into the thousands with answers to match means it was told and the icon
        // is the game's own drawing. It goes in the log rather than only behind a command
        // because the log is the thing that actually gets sent.
        var asked = traffic.Asked - _trafficAtStart.Asked;
        var answered = traffic.Answered - _trafficAtStart.Answered;
        _lines.Add($"  the game asked about the buttons {asked} times during this pull "
                   + $"and was answered {answered} times"
                   + (traffic.LastAnswer is null ? "." : $"; last answer {traffic.LastAnswer}."));

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        // Downloads is not guaranteed to exist; fall back to the profile itself rather than
        // losing the recording after somebody has just spent a pull making it.
        if (!Directory.Exists(directory))
            directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var path = Path.Combine(directory, $"onetwopunch-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllLines(path, _lines);
        return path;
    }

    private static string Stamp(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}.{span.Milliseconds / 100}";
    }

    private static string Trim(string value, int width) =>
        value.Length <= width ? value : value[..(width - 1)] + "…";
}

/// <summary>
/// How much traffic the <c>GetAdjustedActionId</c> hook has seen. Snapshotted at the start
/// and end of a recording so the footer can report the difference.
/// </summary>
/// <param name="Asked">Times the game asked about one of our buttons.</param>
/// <param name="Answered">How many of those were answered with a replacement.</param>
/// <param name="LastAnswer">The name of the last replacement handed back, if there was one.</param>
public readonly record struct HookTraffic(long Asked, long Answered, string? LastAnswer);
