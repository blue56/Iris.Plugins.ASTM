namespace Iris.Core;

/// <summary>
/// A connector encapsulates <em>what</em> system or domain integration the agent
/// is talking to (e.g. an ASTM instrument, a LIMS API, an OPC-UA server, a
/// laboratory analyser).
/// It originates <see cref="DataMessage"/> items — handling device-specific
/// framing, protocol quirks, and data mapping — and emits them via the
/// <see cref="MessageReceived"/> event for downstream <see cref="ITransport"/>
/// instances to deliver.
/// </summary>
/// <remarks>
/// Use a connector when the concern is domain-specific: parsing ASTM records,
/// polling a LIMS REST endpoint, or subscribing to OPC-UA nodes. Pure
/// communication-channel concerns (MQTT, HTTP, Kafka) belong in an
/// <see cref="ITransport"/> instead.
/// </remarks>
public interface IConnector
{
    /// <summary>Raised whenever a new message is available from the integrated system.</summary>
    event Func<DataMessage, Task> MessageReceived;

    /// <summary>
    /// The transport this connector is directly coupled to.
    /// </summary>
    ITransport? Transport { get; }

    /// <summary>Unique name referencing this connector instance.</summary>
    string Name { get; }

    /// <summary>Start the connector and begin receiving messages from the integrated system.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stop the connector gracefully.</summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>Deliver a message to the integrated system.</summary>
    Task SendAsync(DataMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
