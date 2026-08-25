using System.Numerics;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using OneTwoPunch.Plugin.Services;

namespace OneTwoPunch.Plugin.UI;

/// <summary>
/// Shows what each button currently is, and what is coming after it.
/// <para>
/// This is not decoration. Being able to see the next action a beat before you need it is
/// what turns a changing button from something you react to into something you plan for -
/// which is the difference between playable and exhausting if your hands are the
/// bottleneck. It is deliberately large, high contrast and legible at a glance.
/// </para>
/// </summary>
public sealed class PreviewWindow(
    Configuration config,
    ITextureProvider textures,
    LuminaGameData gameData,
    Func<IReadOnlyDictionary<RotationMode, Suggestion>> suggestions,
    Func<IJobRotation?> job)
    : Window("One Two Punch##preview",
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
{
    public override bool DrawConditions()
    {
        if (!config.ShowPreview || job() is null)
            return false;

        return !config.ShowPreviewOnlyInCombat || Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];
    }

    public override void PreDraw()
    {
        Flags = ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.AlwaysAutoResize;

        if (config.LockPreviewWindow)
            Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBackground;
    }

    public override void Draw()
    {
        var current = suggestions();
        var size = 48f * config.PreviewScale;

        var single = current.GetValueOrDefault(RotationMode.SingleTarget);
        var aoe = current.GetValueOrDefault(RotationMode.Aoe);

        // Deliberately at the top and loud: the potion window is short, and it is the one
        // press the plugin cannot put on the button for you.
        if (config.ShowPotionPrompt && (single?.PotionPrompt == true || aoe?.PotionPrompt == true))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.3f, 1f));
            ImGui.SetWindowFontScale(1.4f * config.PreviewScale);
            ImGui.TextUnformatted("POTION NOW");
            ImGui.SetWindowFontScale(1f);
            ImGui.PopStyleColor();
            ImGui.Separator();
        }

        DrawRow("Single target", single, size);
        ImGui.Spacing();
        DrawRow("AoE", aoe, size);

        var rotation = job();
        if (rotation is null)
            return;

        for (var i = 0; i < rotation.ExtraButtons.Count; i++)
        {
            var mode = i == 0 ? RotationMode.Extra1 : RotationMode.Extra2;
            var suggestion = current.GetValueOrDefault(mode);
            if (suggestion is null)
                continue;

            ImGui.Spacing();
            DrawRow(rotation.ExtraButtons[i].Name, suggestion, size);
        }
    }

    private void DrawRow(string label, Suggestion? suggestion, float size)
    {
        ImGui.TextDisabled(label);

        if (suggestion is null)
        {
            ImGui.TextUnformatted("-");
            return;
        }

        DrawIcon(suggestion.Action.Id, size);
        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.TextUnformatted(suggestion.Action.Name);

        if (config.ShowReason && !string.IsNullOrEmpty(suggestion.Note))
            ImGui.TextDisabled(suggestion.Note);

        if (suggestion.Positional != PositionalHint.None)
        {
            ImGui.TextColored(
                new Vector4(1f, 0.78f, 0.25f, 1f),
                suggestion.Positional == PositionalHint.Rear ? "stand behind" : "stand to the side");
        }

        ImGui.EndGroup();

        if (!config.ShowNextGcd || suggestion.NextGcd is null)
            return;

        if (suggestion.NextGcd.Id == suggestion.Action.Id)
            return;

        ImGui.SameLine();
        ImGui.TextDisabled("then");
        ImGui.SameLine();
        DrawIcon(suggestion.NextGcd.Id, size * 0.6f);
    }

    private void DrawIcon(uint actionId, float size)
    {
        var iconId = gameData.GetActionIcon(actionId);
        if (iconId == 0)
        {
            ImGui.Dummy(new Vector2(size, size));
            return;
        }

        var texture = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
        if (texture is null)
        {
            ImGui.Dummy(new Vector2(size, size));
            return;
        }

        ImGui.Image(texture.Handle, new Vector2(size, size));
    }
}
