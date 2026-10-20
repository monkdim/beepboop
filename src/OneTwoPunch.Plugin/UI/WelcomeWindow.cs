using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;

namespace OneTwoPunch.Plugin.UI;

/// <summary>Shown once per acknowledged version, after login and outside combat.</summary>
public sealed class WelcomeWindow : Window
{
    private readonly Configuration _config;
    private readonly string _version;

    public WelcomeWindow(Configuration config, string version)
        : base("One Two Punch##welcome", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        _config = config;
        _version = version;
        IsOpen = config.NeedsWelcome(version);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(540, 0),
            MaximumSize = new Vector2(740, 1200),
        };
    }

    public override bool DrawConditions() => Plugin.Objects.LocalPlayer is not null
        && !Plugin.Condition[ConditionFlag.InCombat];

    public override void PreDraw() => WelcomeContent.PushStyle();

    public override void PostDraw() => WelcomeContent.PopStyle();

    public override void Draw()
    {
        if (WelcomeContent.Draw(_version))
        {
            Acknowledge();
            IsOpen = false;
        }
    }

    public override void OnClose() => Acknowledge();

    private void Acknowledge()
    {
        _config.AcknowledgeWelcome(_version);
    }
}
