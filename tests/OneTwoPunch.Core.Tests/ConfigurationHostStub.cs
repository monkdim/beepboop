// Configuration is tested with the real conversion/save notification code. Only Dalamud's
// persistence host is replaced, keeping these checks runnable without the game.
namespace Dalamud.Configuration
{
    public interface IPluginConfiguration { int Version { get; set; } }
}

namespace OneTwoPunch.Plugin
{
    internal static class Plugin
    {
        public static ConfigurationHost PluginInterface { get; } = new();
    }

    internal sealed class ConfigurationHost
    {
        public void SavePluginConfig(object config) { }
    }
}
