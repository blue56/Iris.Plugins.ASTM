namespace Iris.Core.Plugins;

/// <summary>
/// Factory for creating and registering plugin instances.
/// </summary>
/// <remarks>
/// Connector types model domain integrations (what you talk to: ASTM, LIMS, OPC-UA).
/// Transport types model protocol channels (how data moves: MQTT, HTTP, Kafka).
/// </remarks>
public interface IPluginFactory
{
    /// <summary>Registers a connector type by name.</summary>
    void RegisterConnectorType(string name, Type type);

    /// <summary>Registers a transport type by name.</summary>
    void RegisterTransportType(string name, Type type);

    /// <summary>
    /// Create a connector instance by registered type name.
    /// Connectors model domain integrations (e.g. ASTM, LIMS, OPC-UA).
    /// </summary>
    IConnector? CreateConnector(string typeName, IServiceProvider services, params object[] parameters);

    /// <summary>
    /// Create a transport instance by registered type name.
    /// Transports model protocol delivery channels (e.g. MQTT, HTTP webhook, Kafka).
    /// </summary>
    ITransport? CreateTransport(string typeName, IServiceProvider services, params object[] parameters);

    /// <summary>Get all registered connector type names (domain integrations).</summary>
    IEnumerable<string> GetAvailableConnectorTypes();

    /// <summary>Get all registered transport type names (protocol channels).</summary>
    IEnumerable<string> GetAvailableTransportTypes();
}
