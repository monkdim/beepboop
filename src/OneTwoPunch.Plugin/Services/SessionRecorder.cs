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
    private IconTraffic _iconsAtStart;

    public bool IsRecording { get; private set; }

    public int Casts => _casts;

    public void Start(
        string job, byte level, string version, double now, HookTraffic traffic, IconTraffic icons)
    {
        _iconsAtStart = icons;
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
    public string? Stop(double now, HookTraffic traffic, IconTraffic icons, string? openerOutcome = null)
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
        var ours = traffic.AskedByOurOwnWork - _trafficAtStart.AskedByOurOwnWork;
        _lines.Add($"  the game asked about the buttons {asked} times during this pull "
                   + $"and was answered {answered} times"
                   + (traffic.LastAnswer is null ? "." : $"; last answer {traffic.LastAnswer}."));

        // Split out rather than hidden. These are the asks our own per-frame resolve causes,
        // and they land at about one a frame no matter what the hotbar is doing - which is
        // why they are not in the number above.
        _lines.Add($"  ({ours} further asks came from the plugin's own work, not the game.)");

        // And the other hook, which is the one that decides what the slot draws. These two
        // have been confused before: answering the game about the button is what makes the
        // key fire the right ability, and it does not touch the icon. A pull where the first
        // pair of numbers climbs and this pair does not is a button that works and does not
        // look like it does - which is worse than useless to somebody reading the icon.
        if (!icons.Active)
        {
            _lines.Add("  the icon hook is not installed, so the slot keeps its own art.");
        }
        else
        {
            var drawn = icons.Drawn - _iconsAtStart.Drawn;
            var replaced = icons.Replaced - _iconsAtStart.Replaced;
            _lines.Add($"  a slot holding one of your buttons was drawn {drawn} times "
                       + $"and showed the suggestion {replaced} of them.");

            // A drawn count of zero has three very different causes and the line above
            // cannot tell them apart: the hook never ran, every slot it saw was something
            // other than an action, or the ids on the bar are not the ones we are looking
            // for. All three read as "0". So say which.
            if (drawn == 0)
            {
                var seen = icons.SlotsSeen - _iconsAtStart.SlotsSeen;
                var notActions = icons.NotActions - _iconsAtStart.NotActions;

                _lines.Add($"  the hook saw {seen} slots drawn, {notActions} of them not actions.");

                // And when even that is zero, which is where the last pull landed: the
                // question is no longer "what did it see" but "did it run at all". These
                // three are counted before any of the guards, so a zero here is the game
                // never calling the function we are attached to - which is a different
                // problem from anything the plugin does after being called.
                var entered = icons.Entered - _iconsAtStart.Entered;
                if (seen == 0)
                {
                    var noSlot = icons.NoSlot - _iconsAtStart.NoSlot;
                    var reentrant = icons.Reentrant - _iconsAtStart.Reentrant;

                    _lines.Add($"  the hook was entered {entered} times, {noSlot} of them with "
                               + $"no slot and {reentrant} while already inside itself.");
                    _lines.Add($"  it is installed at 0x{icons.Address:X}.");
                }

                var ids = icons.UnrecognisedIds;
                if (ids is { Count: > 0 })
                    _lines.Add($"  action ids it drew and did not recognise: {string.Join(", ", ids)}");
            }
        }

        // The opener giving up used to be silent, and a pull that stopped being driven at
        // step seven read exactly like one that ran to the end. Giving up is only one of the
        // ways it stops, though - the first pass recorded only that, and the pull that
        // prompted it turned out not to be giving up at all.
        if (openerOutcome is not null)
            _lines.Add($"  the opener: {openerOutcome}");

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
public readonly record struct HookTraffic(
    long Asked, long Answered, string? LastAnswer, long AskedByOurOwnWork = 0);

/// <summary>
/// What the icon half of the plugin did. Separate from <see cref="HookTraffic"/> because it
/// is a different hook answering a different question, and the two have been confused
/// before: the action counters climbing into the thousands says the key fires the right
/// ability, and says nothing at all about whether the slot draws it.
/// </summary>
public readonly record struct IconTraffic(
    bool Active,
    long Drawn,
    long Replaced,
    long SlotsSeen = 0,
    long NotActions = 0,
    IReadOnlyCollection<uint>? UnrecognisedIds = null,
    long Entered = 0,
    long NoSlot = 0,
    long Reentrant = 0,
    nint Address = 0);
