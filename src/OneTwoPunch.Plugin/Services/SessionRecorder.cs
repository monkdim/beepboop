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

    public bool IsRecording { get; private set; }

    public int Casts => _casts;

    public void Start(string job, byte level, string version, double now)
    {
        _lines.Clear();
        _casts = 0;
        _followed = 0;
        _startedAt = now;
        IsRecording = true;

        _lines.Add($"One Two Punch {version} - {job} level {level}");
        _lines.Add("");
        _lines.Add("  time     cast                            suggested                       ");
        _lines.Add("  -------- ------------------------------- ------------------------------- ");
    }

    /// <summary>Notes an action the player actually used, beside what was being suggested.</summary>
    public void Cast(double now, string cast, uint castId, string? suggested, string? note)
    {
        if (!IsRecording)
            return;

        _casts++;

        var agreed = suggested is not null
            && string.Equals(cast, suggested, StringComparison.OrdinalIgnoreCase);

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
    }

    public void Note(double now, string text)
    {
        if (IsRecording)
            _lines.Add($"  {Stamp(now - _startedAt).PadRight(9)}{text}");
    }

    /// <summary>Stops and writes the log out. Returns the path, or null if nothing was recorded.</summary>
    public string? Stop(double now)
    {
        if (!IsRecording)
            return null;

        IsRecording = false;

        if (_casts == 0)
            return null;

        _lines.Add("");
        _lines.Add($"  {_casts} casts over {Stamp(now - _startedAt)}, "
                   + $"{_followed} matched the suggestion, {_casts - _followed} did not.");

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
