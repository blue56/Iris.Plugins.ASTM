namespace Iris.Core.Plugins;

/// <summary>
/// Defines the type of plugin functionality.
/// </summary>
public enum PluginType
{
    /// <summary>
    /// Plugin is a connector — it models <em>what</em> you are integrating with
    /// (e.g. ASTM instruments, a LIMS, an OPC-UA server).
    /// A connector originates <see cref="DataMessage"/> items and decides which
    /// transports they are routed to.
    /// </summary>
    Connector,

    /// <summary>
    /// Plugin is a transport — it models <em>how</em> data moves between systems
    /// (e.g. MQTT, HTTP webhook, Kafka).
    /// A transport receives a <see cref="DataMessage"/> and delivers it over a
    /// specific protocol without knowing anything about the domain.
    /// </summary>
    Transport
}
