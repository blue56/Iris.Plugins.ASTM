using System.Text;

namespace Iris.Plugins.ASTM;

/// <summary>
/// Configuration options shared by the ASTM source and target.
/// </summary>
public sealed class AstmOptions
{
    /// <summary>Hostname or IP address to bind (source) or connect to (target).</summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>TCP port number.</summary>
    public int Port { get; set; } = 1234;

    /// <summary>
    /// Unique identifier for this instrument instance, used to build MQTT topics:
    /// <c>astm/results/{InstrumentId}</c>, <c>astm/orders/{InstrumentId}</c>,
    /// <c>astm/status/{InstrumentId}</c>.
    /// </summary>
    public string InstrumentId { get; set; } = "default";

    /// <summary>
    /// Text encoding used when converting bytes to/from ASTM message strings.
    /// Defaults to ASCII, which is the encoding mandated by ASTM E1381.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.ASCII;

    /// <summary>
    /// How long (in milliseconds) to wait for an acknowledgement before timing out.
    /// </summary>
    public int AcknowledgeTimeoutMs { get; set; } = 5_000;

    /// <summary>
    /// Delay in milliseconds between connection retry attempts.
    /// </summary>
    public int RetryDelayMs { get; set; } = 5_000;

    /// <summary>Maximum number of data bytes per ASTM frame (ASTM E1381 maximum is 240).</summary>
    public int MaxFrameDataBytes { get; set; } = 240;
}
