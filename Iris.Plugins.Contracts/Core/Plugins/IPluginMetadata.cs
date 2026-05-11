namespace Iris.Core.Plugins;

/// <summary>
/// Describes metadata about a plugin.
/// </summary>
public interface IPluginMetadata
{
    /// <summary>Unique name identifying the plugin.</summary>
    string Name { get; }

    /// <summary>Version of the plugin.</summary>
    string Version { get; }

    /// <summary>Author or organization that created the plugin.</summary>
    string Author { get; }

    /// <summary>Brief description of the plugin's functionality.</summary>
    string Description { get; }

    /// <summary>
    /// Whether the plugin is a <see cref="PluginType.Connector"/> (domain integration)
    /// or a <see cref="PluginType.Transport"/> (protocol delivery channel).
    /// </summary>
    PluginType Type { get; }
}
