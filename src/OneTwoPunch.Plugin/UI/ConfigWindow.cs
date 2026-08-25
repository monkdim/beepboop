using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using OneTwoPunch.Plugin.Services;

namespace OneTwoPunch.Plugin.UI;

public sealed class ConfigWindow(
    Configuration config,
    PotionTracker potions,
    Func<IJobRotation?> currentJob,
    Func<IReadOnlyDictionary<uint, VerificationReport>> reports,
    Func<bool> isArmed)
    : Window("One Two Punch", ImGuiWindowFlags.AlwaysAutoResize)
{
    public override void Draw()
    {
        var enabled = config.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            config.Enabled = enabled;
            config.Save();
        }

        ImGui.SameLine();
        ImGui.TextDisabled("(one keypress is always one action - nothing is ever pressed for you)");

        ImGui.Separator();

        if (ImGui.BeginTabBar("##twobutton"))
        {
            if (ImGui.BeginTabItem("Rotation"))
            {
                DrawRotationTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Potion"))
            {
                DrawPotionTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Display"))
            {
                DrawDisplayTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Setup"))
            {
                DrawSetupTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawRotationTab()
    {
        ImGui.TextDisabled("How much the button is allowed to ask of you.");
        ImGui.Spacing();

        var weave = (int)config.WeaveStyle;
        if (ImGui.Combo("Off-globals per GCD", ref weave,
                "None - globals only, lowest effort\0One - the accessible default\0Two - highest damage\0"))
        {
            config.WeaveStyle = (WeaveStyle)weave;
            config.Save();
        }

        Help("Off-globals are the abilities that do not use the global cooldown. Fitting two "
             + "of them into every gap is where most of a rotation's button presses come from. "
             + "On 'One' you lose a little damage and a lot of typing.");

        var hold = config.SuggestionHoldSeconds;
        if (ImGui.SliderFloat("Hold suggestions for", ref hold, 0f, 0.5f, "%.2f s"))
        {
            config.SuggestionHoldSeconds = hold;
            config.Save();
        }

        Help("Stops the icon changing under your hand while you are reaching for the key. "
             + "The hold is dropped instantly if the action stops being usable, so it can "
             + "never cause a wasted press.");

        var margin = config.WeaveSafetyMargin;
        if (ImGui.SliderFloat("Weave safety margin", ref margin, 0f, 0.5f, "%.2f s"))
        {
            config.WeaveSafetyMargin = margin;
            config.Save();
        }

        Help("Extra head-room before an off-global is offered. Raise it if you play on a high "
             + "ping or find yourself clipping the global cooldown.");

        ImGui.Spacing();

        Toggle("Use the scripted opener", ref config.UseOpener,
            "Walks the job's opening sequence at the pull. Abandons it the moment you press "
            + "something else, and never starts one mid-fight.");

        Toggle("AoE button falls back to single target", ref config.AoeFallsBackToSingleTarget,
            "When only one enemy is in range, the AoE button uses the single-target rotation "
            + "instead of a weak AoE combo.");

        Toggle("Suggest True North for positionals", ref config.SuggestPositionalRescue,
            "Offers True North when the next hit wants a flank or rear position you are not "
            + "standing in. For anyone who cannot reliably reposition.");

        Toggle("Hold burst during downtime", ref config.HoldBurstDuringDowntime,
            "Stops raid buffs and big cooldowns being suggested while the boss is untargetable.");

        Toggle("Detect positionals", ref config.DetectPositionals,
            "Reads the target's facing. Only feeds the positional hint - turning it off never "
            + "changes which abilities the rotation picks.");
    }

    private void DrawPotionTab()
    {
        ImGui.TextWrapped(
            "A potion is an item, not an action, so the button cannot become one - and "
            + "pressing it for you would make this an automation plugin. What it can do is "
            + "tell you the exact weave window to pop it in, having already checked it is "
            + "off cooldown and that it fits without clipping your global cooldown.");
        ImGui.Spacing();
        ImGui.TextDisabled("Bind your potion to one spare key. It is one press every few minutes.");
        ImGui.Separator();

        Toggle("Prompt me for a potion", ref config.PotionEnabled);

        if (!config.PotionEnabled)
            return;

        var options = potions.Options;
        var current = potions.NameOf(config.PotionItemId);

        if (ImGui.BeginCombo("Potion", current))
        {
            _potionFilter ??= string.Empty;
            ImGui.SetNextItemWidth(220f);
            ImGui.InputTextWithHint("##potionfilter", "search", ref _potionFilter, 64);

            var shown = 0;
            foreach (var option in options)
            {
                if (_potionFilter.Length > 0
                    && !option.Name.Contains(_potionFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (shown++ > 60)
                    break;

                if (!ImGui.Selectable(option.Name, option.ItemId == config.PotionItemId))
                    continue;

                config.PotionItemId = option.ItemId;
                config.Save();
            }

            ImGui.EndCombo();
        }

        Help("Read from the game's own item list, so a new tier appears here without a "
             + "plugin update.");

        Toggle("Prefer high quality", ref config.PotionPreferHq,
            "Uses the HQ version when you are carrying one.");

        Toggle("Prompt during the opener", ref config.PotionInOpener,
            "Prompts at the job's opener potion point - the first weave window where it "
            + "lines up with your burst.");

        Toggle("Prompt on burst windows", ref config.PotionOnBurst,
            "Prompts whenever your burst buff is up and the potion is off cooldown. Jobs "
            + "whose burst is marked by a debuff on the target rather than a buff on you "
            + "only use the opener prompt.");
    }

    private string? _potionFilter;

    private void DrawDisplayTab()
    {
        Toggle("Show the next-action display", ref config.ShowPreview,
            "A large, readable panel showing what each button currently is.");

        Toggle("Show what comes after", ref config.ShowNextGcd,
            "Also shows the next global cooldown, so you can see one step ahead.");

        Toggle("Show the reason", ref config.ShowReason,
            "A short line explaining why the engine picked this action.");

        Toggle("Show the positional banner", ref config.ShowPositionalBanner,
            "A large banner telling you which side of the target to stand on for the coming "
            + "global cooldown - a beat before you need to be there, not as it comes up. It "
            + "goes quiet as soon as you are standing correctly.");

        Toggle("Show the potion prompt", ref config.ShowPotionPrompt,
            "A large banner when it is the moment to pop your potion.");

        Toggle("Only show in combat", ref config.ShowPreviewOnlyInCombat);

        Toggle("Lock in place", ref config.LockPreviewWindow,
            "Removes the background and stops the panel being dragged.");

        var scale = config.PreviewScale;
        if (ImGui.SliderFloat("Size", ref scale, 0.8f, 4f, "%.1fx"))
        {
            config.PreviewScale = scale;
            config.Save();
        }
    }

    private void DrawSetupTab()
    {
        var job = currentJob();

        if (job is null)
        {
            // These are three different situations and telling them apart matters: the first
            // is something you fix in one keystroke, and it used to be reported as the third.
            if (!isArmed())
            {
                ImGui.TextWrapped(
                    "Not running yet. Type /otp arm in chat to start it for this session - it "
                    + "deliberately does nothing until you do, and is inert again next time the "
                    + "game starts.");
            }
            else if (reports().Values.Any(r => !r.IsSafeToRun))
            {
                ImGui.TextWrapped(
                    "This job is switched off because some of its action ids could not be matched "
                    + "against the game's own data. Run /otp verify for the list.");
            }
            else
            {
                ImGui.TextWrapped(
                    "Your current job is not supported yet. One Two Punch only touches jobs it has a "
                    + "rotation for - everything else behaves exactly as it always did.");
            }

            return;
        }

        ImGui.TextWrapped($"Put these two actions on your hotbar. That is the whole setup for {job.Name}.");
        ImGui.Spacing();

        ImGui.BulletText($"Single target:  {job.SingleTargetButton.Name}");
        ImGui.BulletText($"AoE:            {job.AoeButton.Name}");

        foreach (var extra in job.ExtraButtons)
            ImGui.BulletText($"{extra.Name}:{new string(' ', Math.Max(1, 15 - extra.Name.Length))}{extra.Host.Name}");

        if (job.ExtraButtons.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(
                "This job needs more than two keys. Two cover almost everything, but some "
                + "mechanics are genuinely several presses per cast and folding those into a "
                + "changing icon would be worse than an extra key:");

            foreach (var extra in job.ExtraButtons)
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), extra.Name);
                ImGui.SameLine();
                ImGui.TextWrapped(extra.Purpose);
            }
        }

        ImGui.Spacing();
        ImGui.TextWrapped(
            "Their icons will change as you play to show the action that is correct right now. "
            + "Pressing one casts exactly what the icon shows, once.");

        ImGui.Separator();
        ImGui.TextDisabled("Action data");

        if (reports().TryGetValue(job.JobId, out var report))
        {
            if (report.UnresolvedCount > 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f),
                    $"{report.UnresolvedCount} action id(s) could not be matched. This job is switched off.");
            }
            else if (report.RepairedCount > 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.78f, 0.25f, 1f),
                    $"{report.RepairedCount} id(s) were out of date and have been corrected automatically.");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.4f, 0.9f, 0.4f, 1f), "All action ids check out.");
            }

            ImGui.TextDisabled("Run /otp verify after a patch, or to paste into a bug report.");
        }
    }

    private static void Help(string text)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");

        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private void Toggle(string label, ref bool value, string? help = null)
    {
        if (ImGui.Checkbox(label, ref value))
            config.Save();

        if (help is not null)
            Help(help);
    }
}
