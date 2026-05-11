namespace Iris.Core;

/// <summary>
/// A transport encapsulates <em>how</em> data moves between systems.
/// It is a protocol-level adapter (e.g. MQTT, HTTP, Kafka) that accepts a
/// <see cref="DataMessage"/> and delivers it over a specific wire protocol,
/// with no knowledge of the domain or the integration it is serving.
/// </summary>
/// <remarks>
/// Use a transport when the concern is purely about the communication channel:
/// publishing to an MQTT broker, posting to an HTTP webhook, or writing to a
/// Kafka topic. Domain-specific logic (ASTM framing, LIMS mapping, OPC-UA node
/// addressing) belongs in an <see cref="IConnector"/> instead.
/// </remarks>
public interface ITransport
{
    /// <summary>Unique name used to reference this transport from connector routing configuration.</summary>
    string Name { get; }

    /// <summary>Whether this transport supports sending messages.</summary>
    bool CanSend => true;

    /// <summary>Whether this transport supports receiving messages.</summary>
    bool CanReceive => true;

    /// <summary>Deliver the message over the transport's protocol.</summary>
    Task SendAsync(DataMessage message, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Raised whenever a new message is received from the transport.</summary>
    event Func<DataMessage, Task>? MessageReceived;

    /// <summary>Starts listening for incoming messages (if supported).</summary>
    Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Stops listening for incoming messages (if supported).</summary>
    Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
