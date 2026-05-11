namespace Iris.Core.Plugins;

/// <summary>
/// Attribute to mark and describe plugin classes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute
{
    /// <summary>Unique name identifying the plugin.</summary>
    public string Name { get; }

    /// <summary>Version of the plugin.</summary>
    public string Version { get; }

    /// <summary>Author or organization that created the plugin.</summary>
    public string Author { get; set; } = "Unknown";

    /// <summary>Brief description of the plugin's functionality.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether the attributed class is a <see cref="PluginType.Connector"/> (domain
    /// integration — what you are talking to) or a <see cref="PluginType.Transport"/>
    /// (protocol channel — how data is moved).
    /// </summary>
    public PluginType Type { get; }

    /// <summary>
    /// Creates a new plugin metadata attribute.
    /// </summary>
    /// <param name="name">Unique name identifying the plugin.</param>
    /// <param name="version">Version of the plugin.</param>
    /// <param name="type">The plugin functionality classification.</param>
    public PluginAttribute(string name, string version, PluginType type)
    {
        Name = name;
        Version = version;
        Type = type;
    }
}
