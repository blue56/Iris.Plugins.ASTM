namespace Iris.Core.Plugins;

/// <summary>
/// Central registry for managing loaded plugins.
/// </summary>
/// <remarks>
/// Plugins fall into two categories:
/// <list type="bullet">
///   <item><term>Connector</term><description>Encapsulates <em>what</em> system is being integrated (ASTM, LIMS, OPC-UA). Originates messages.</description></item>
///   <item><term>Transport</term><description>Encapsulates <em>how</em> data moves (MQTT, HTTP, Kafka). Delivers messages over a protocol channel.</description></item>
/// </list>
/// </remarks>
public interface IPluginRegistry
{
    /// <summary>
    /// Register a connector — a domain integration that originates messages
    /// (e.g. ASTM instrument reader, LIMS poller, OPC-UA subscriber).
    /// </summary>
    void RegisterConnector(IConnector connector);

    /// <summary>
    /// Register a transport — a protocol channel that delivers messages
    /// (e.g. MQTT publisher, HTTP webhook, Kafka producer).
    /// </summary>
    void RegisterTransport(ITransport transport);

    /// <summary>Get all registered connectors.</summary>
    IEnumerable<IConnector> GetConnectors();

    /// <summary>Get all registered transports.</summary>
    IEnumerable<ITransport> GetTransports();

    /// <summary>Get a transport by name.</summary>
    ITransport? GetTransport(string name);
}
