using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace OneTwoPunch.Plugin;

/// <summary>
/// Dalamud's services, injected once at load.
/// <para>
/// This exists as a class of its own for a reason worth writing down, because getting it
/// wrong cost a great deal. <see cref="IDalamudPluginInterface.Create{T}"/> <em>builds an
/// instance of T</em> and injects services into it. These properties used to live on
/// <c>Plugin</c> itself, and <c>Plugin</c>'s constructor called
/// <c>pluginInterface.Create&lt;Plugin&gt;()</c> - so constructing a Plugin constructed a
/// Plugin, which constructed a Plugin. The constructor never returned, and since Dalamud
/// builds plugins on the game's frame thread, the game stopped dead with a core pinned.
/// </para>
/// <para>
/// The symptom was a plugin that froze the game the moment it was enabled, with no error
/// anywhere: the Dalamud log said "Creating plugin instance for OneTwoPunch" and never said
/// anything about it again. So: T must be a type that is not the one doing the creating.
/// </para>
/// </summary>
internal sealed class Svc
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IJobGauges Gauges { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
}
