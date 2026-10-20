using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace OneTwoPunch.Plugin.UI;

/// <summary>Native drawing keeps the mascot sharp without image downloads or textures.</summary>
public static class WelcomeContent
{
    private static readonly Vector4 Navy = new(.106f, .137f, .18f, 1);
    private static readonly Vector4 Panel = new(.153f, .192f, .243f, 1);
    private static readonly Vector4 Cream = new(1, .953f, .894f, 1);
    private static readonly Vector4 Muted = new(.73f, .79f, .83f, 1);
    private static readonly Vector4 Peach = new(1, .757f, .663f, 1);
    private static readonly Vector4 Mint = new(.678f, .886f, .796f, 1);

    public static void PushStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Navy);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, Navy);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Navy);
        ImGui.PushStyleColor(ImGuiCol.Text, Cream);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.36f, .44f, .49f, .55f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 20f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(26, 20));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 12));
    }

    public static void PopStyle()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(6);
    }

    public static bool Draw(string version)
    {
        var scale = ImGui.GetFontSize() / 17f;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        ImGui.TextColored(Mint, $"ONE TWO PUNCH   /   {version}");
        ImGui.Dummy(new Vector2(0, 6 * scale));
        var header = ImGui.GetCursorScreenPos();
        ImGui.SetWindowFontScale(1.9f);
        ImGui.TextUnformatted("Beep boop.\nNew things!");
        ImGui.SetWindowFontScale(1);
        DrawRobot(new Vector2(origin.X + width - 77 * scale, header.Y + 53 * scale), scale);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, header.Y + 112 * scale));
        ImGui.TextColored(Muted, "A few little upgrades, made for your hands.");
        ImGui.Dummy(new Vector2(0, 5 * scale));
        Card("01", "Settings listen now", "Change a setting and keep playing. No restart, no job swap.", Mint, width, scale);
        Card("02", "Your hands set the pace", "None, One or Two: your extra-attack limit stays your choice on every job.", Peach, width, scale);
        Card("03", "Viper learned some manners", "One really means one now. Some follow-ups may go unused, and that's okay. Pick what feels comfortable.", Mint, width, scale);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        ImGui.TextColored(Muted, "Changing the opener or globals-only mode mid-fight leaves the normal rotation in charge until your next pull.");
        ImGui.PopTextWrapPos();
        ImGui.Dummy(new Vector2(0, 3 * scale));
        ImGui.PushStyleColor(ImGuiCol.Button, Mint);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.78f, .95f, .87f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.57f, .79f, .70f, 1));
        ImGui.PushStyleColor(ImGuiCol.Text, Navy);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 13f);
        var close = ImGui.Button("Let's play!", new Vector2(width, 46 * scale));
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
        Centered("Still your buttons. Still your timing.", Peach, width);
        Centered("/otp arm to start  /  /otp changelog to visit again", Muted, width);
        return close;
    }

    private static void Centered(string text, Vector4 color, float width)
    {
        var x = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(x + Math.Max(0, (width - ImGui.CalcTextSize(text).X) / 2));
        ImGui.TextColored(color, text);
    }

    private static void Card(string number, string title, string body, Vector4 accent, float width, float scale)
    {
        var start = ImGui.GetCursorScreenPos();
        var padding = 17 * scale;
        var textX = start.X + 52 * scale;
        var height = padding * 2 + ImGui.GetTextLineHeight() + 6 * scale
            + ImGui.CalcTextSize(body, false, width - 70 * scale).Y;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(start, start + new Vector2(width, height), Color(Panel), 13 * scale);
        draw.AddCircleFilled(start + new Vector2(26, 28) * scale, 12 * scale, Color(accent));
        draw.AddText(start + new Vector2(26, 28) * scale - ImGui.CalcTextSize(number) / 2, Color(Navy), number);
        ImGui.SetCursorScreenPos(new Vector2(textX, start.Y + padding));
        ImGui.TextColored(accent, title);
        ImGui.SetCursorScreenPos(new Vector2(textX, start.Y + padding + ImGui.GetTextLineHeight() + 6 * scale));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width - 69 * scale);
        ImGui.TextWrapped(body);
        ImGui.PopTextWrapPos();
        ImGui.SetCursorScreenPos(start + new Vector2(0, height + 12 * scale));
    }

    private static uint Color(Vector4 value) => ImGui.ColorConvertFloat4ToU32(value);

    private static void DrawRobot(Vector2 center, float scale)
    {
        var d = ImGui.GetWindowDrawList();
        Vector2 P(float x, float y) => center + new Vector2(x, y) * scale;
        var peach = Color(Peach);
        var mint = Color(Mint);
        var ink = Color(Navy);
        d.AddCircleFilled(P(0, 3), 64 * scale, Color(new Vector4(.19f, .26f, .29f, 1)));
        d.AddLine(P(0, -43), P(0, -56), mint, 3 * scale);
        d.AddCircleFilled(P(0, -59), 5 * scale, peach);
        d.AddRectFilled(P(-30, 21), P(30, 52), mint, 11 * scale);
        d.AddRectFilled(P(-22, 49), P(-7, 57), peach, 4 * scale);
        d.AddRectFilled(P(7, 49), P(22, 57), peach, 4 * scale);
        d.AddLine(P(-28, 31), P(-43, 21), mint, 9 * scale);
        d.AddCircleFilled(P(-45, 19), 6 * scale, peach);
        d.AddLine(P(28, 31), P(45, 13), mint, 9 * scale);
        d.AddLine(P(45, 13), P(46, -1), mint, 9 * scale);
        d.AddCircleFilled(P(46, -5), 7 * scale, peach);
        d.AddRectFilled(P(-40, -39), P(40, 22), peach, 18 * scale);
        d.AddRectFilled(P(-29, -28), P(29, 10), ink, 12 * scale);
        d.AddCircleFilled(P(-13, -11), 4.5f * scale, mint);
        d.AddCircleFilled(P(13, -11), 4.5f * scale, mint);
        d.AddBezierQuadratic(P(-6, -1), P(0, 6), P(6, -1), mint, 2 * scale);
        d.AddCircleFilled(P(-29, 2), 4 * scale, Color(new Vector4(.96f, .54f, .51f, 1)));
        d.AddCircleFilled(P(29, 2), 4 * scale, Color(new Vector4(.96f, .54f, .51f, 1)));
        d.AddCircleFilled(P(0, 36), 5 * scale, peach);
        foreach (var point in new[] { P(-61, -39), P(63, 31) })
        {
            d.AddLine(point - new Vector2(5, 0) * scale, point + new Vector2(5, 0) * scale, mint, 2 * scale);
            d.AddLine(point - new Vector2(0, 5) * scale, point + new Vector2(0, 5) * scale, mint, 2 * scale);
        }
    }
}
