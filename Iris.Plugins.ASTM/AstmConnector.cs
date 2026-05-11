using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Iris.Core;
using Iris.Core.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Iris.Plugins.ASTM;

/// <summary>
/// ASTM connector plugin that relays messages between a laboratory instrument
/// (ASTM E1381 LLP over TCP) and an MQTT transport in both directions.
/// The bridge connects to the instrument as a TCP client and keeps the
/// connection alive between sessions, retrying automatically on failure.
/// </summary>
[Plugin("ASTM Connector", "1.0.0", PluginType.Connector, Description = "Relays ASTM E1381 messages between a TCP instrument and MQTT.")]
public sealed class AstmConnector : IConnector, IAsyncDisposable
{
    private readonly AstmOptions _options;
    private readonly ILogger<AstmConnector> _logger;
    private CancellationTokenSource? _cts;
    private Task? _connectionLoop;

    // ASTM LLP control characters
    private const byte ENQ = 0x05;
    private const byte ACK = 0x06;
    private const byte NAK = 0x15;
    private const byte STX = 0x02;
    private const byte ETX = 0x03;
    private const byte ETB = 0x17;
    private const byte EOT = 0x04;
    private const byte CR  = 0x0D;

    // Only one ASTM session may run at a time.
    private readonly SemaphoreSlim _sessionSemaphore = new(1, 1);

    public event Func<DataMessage, Task>? MessageReceived;

    public string Name { get; }
    public ITransport? Transport { get; }

    public AstmConnector(IOptions<AstmOptions> options, ILogger<AstmConnector> logger,
        string name = "ASTM Connector", ITransport? transport = null)
    {
        Name = name;
        _options = options.Value;
        _logger = logger;
        Transport = transport;
    }

    // -------------------------------------------------------------------------
    // Topic helpers
    // -------------------------------------------------------------------------

    private string ResultsTopic  => $"astm/results/{_options.InstrumentId}";
    private string OrdersTopic   => $"astm/orders/{_options.InstrumentId}";
    private string StatusTopic   => $"astm/status/{_options.InstrumentId}";

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (Transport is not null)
            Transport.MessageReceived += OnTransportMessageAsync;

        _connectionLoop = RunConnectionLoopAsync(_cts.Token);

        _logger.LogInformation("ASTM connector started (instrument={Host}:{Port}, id={Id})",
            _options.Host, _options.Port, _options.InstrumentId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        if (_connectionLoop is not null)
        {
            try { await _connectionLoop; }
            catch (OperationCanceledException) { }
        }

        await PublishStatusAsync("offline", CancellationToken.None);

        if (Transport is not null)
            Transport.MessageReceived -= OnTransportMessageAsync;

        _logger.LogInformation("ASTM connector stopped.");
    }

    // -------------------------------------------------------------------------
    // TCP connection loop (instrument ? MQTT direction)
    // -------------------------------------------------------------------------

    private async Task RunConnectionLoopAsync(CancellationToken ct)
    {
        await PublishStatusAsync("online", ct);

        while (!ct.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = new TcpClient();
                _logger.LogDebug("Connecting to instrument at {Host}:{Port}…", _options.Host, _options.Port);
                await client.ConnectAsync(_options.Host, _options.Port, ct);
                _logger.LogInformation("Connected to instrument at {Host}:{Port}.", _options.Host, _options.Port);

                await HandleInstrumentConnectionAsync(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Instrument connection error. Retrying in {Delay} ms.", _options.RetryDelayMs);
            }
            finally
            {
                client?.Dispose();
            }

            if (!ct.IsCancellationRequested)
                await Task.Delay(_options.RetryDelayMs, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles a live TCP connection to the instrument, receiving ASTM sessions
    /// and raising <see cref="MessageReceived"/> for each complete message.
    /// </summary>
    private async Task HandleInstrumentConnectionAsync(TcpClient client, CancellationToken ct)
    {
        var stream = client.GetStream();
        var buffer = new byte[1];
        var messageBuilder = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            int bytesRead = await stream.ReadAsync(buffer, ct);
            if (bytesRead == 0)
            {
                _logger.LogWarning("Instrument closed the connection.");
                break;
            }

            byte b = buffer[0];

            if (b == ENQ)
            {
                await _sessionSemaphore.WaitAsync(ct);
                try
                {
                    messageBuilder.Clear();
                    await stream.WriteAsync(new byte[] { ACK }, ct);
                    await ReceiveAstmMessageAsync(stream, messageBuilder, ct);

                    string body = messageBuilder.ToString();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        _logger.LogDebug("ASTM message received ({Length} chars), forwarding to results topic.", body.Length);
                        var msg = new DataMessage
                        {
                            Body = body,
                            Metadata =
                            {
                                ["Source"]  = "ASTM",
                                ["Topic"]   = ResultsTopic,
                                ["InstrumentId"] = _options.InstrumentId
                            }
                        };

                        if (MessageReceived is not null)
                            await MessageReceived(msg);

                        if (Transport is not null)
                            await Transport.SendAsync(msg, ct);
                    }
                }
                finally
                {
                    _sessionSemaphore.Release();
                }
            }
        }
    }

    /// <summary>
    /// Reads frames from <paramref name="stream"/> until <c>EOT</c>, appending
    /// decoded frame content to <paramref name="messageBuilder"/>.
    /// </summary>
    private async Task ReceiveAstmMessageAsync(NetworkStream stream, StringBuilder messageBuilder, CancellationToken ct)
    {
        var singleByte = new byte[1];

        while (!ct.IsCancellationRequested)
        {
            int n = await stream.ReadAsync(singleByte, ct);
            if (n == 0) return;

            byte b = singleByte[0];

            if (b == STX)
            {
                (string text, bool isIntermediate) = await ReadFrameBodyAsync(stream, ct);
                messageBuilder.Append(text);

                // ACK each frame
                await stream.WriteAsync(new byte[] { ACK }, ct);
            }
            else if (b == EOT)
            {
                return;
            }
        }
    }

    // -------------------------------------------------------------------------
    // MQTT ? Instrument direction (orders)
    // -------------------------------------------------------------------------

    private async Task OnTransportMessageAsync(DataMessage message)
    {
        // Only forward messages addressed to the orders topic for this instrument.
        if (message.Metadata.TryGetValue("Topic", out var topic) && topic != OrdersTopic)
            return;

        var ct = _cts?.Token ?? CancellationToken.None;

        await _sessionSemaphore.WaitAsync(ct);
        try
        {
            TcpClient? client = null;
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(_options.Host, _options.Port, ct);
                var stream = client.GetStream();

                _logger.LogDebug("Sending ASTM order to instrument ({Length} chars).", message.Body.Length);
                await SendAstmMessageAsync(stream, message.Body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to forward order to instrument.");
            }
            finally
            {
                client?.Dispose();
            }
        }
        finally
        {
            _sessionSemaphore.Release();
        }
    }

    // -------------------------------------------------------------------------
    // IConnector.SendAsync – called by the framework to push an order to the instrument
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task SendAsync(DataMessage message, CancellationToken cancellationToken)
    {
        await OnTransportMessageAsync(message);
    }

    // -------------------------------------------------------------------------
    // ASTM LLP frame encoding / decoding
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends <paramref name="text"/> to the instrument as an ASTM LLP session:
    /// ENQ ? ACK ? frames ? EOT.
    /// </summary>
    private async Task SendAstmMessageAsync(NetworkStream stream, string text, CancellationToken ct)
    {
        // Negotiate session
        await stream.WriteAsync(new byte[] { ENQ }, ct);
        using var ackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ackCts.CancelAfter(_options.AcknowledgeTimeoutMs);
        byte ack = await ReadByteAsync(stream, ackCts.Token);
        if (ack != ACK)
        {
            _logger.LogWarning("Instrument responded with {Byte} instead of ACK; aborting send.", ack);
            return;
        }

        byte[] data = _options.Encoding.GetBytes(text);
        int totalChunks = (int)Math.Ceiling(data.Length / (double)_options.MaxFrameDataBytes);
        int frameNumber = 1;

        for (int i = 0; i < data.Length; i += _options.MaxFrameDataBytes)
        {
            int chunkLen  = Math.Min(_options.MaxFrameDataBytes, data.Length - i);
            bool isLast   = (i + chunkLen) >= data.Length;
            byte terminator = isLast ? ETX : ETB;
            byte fn = (byte)('0' + (frameNumber % 8));

            byte checksum = ComputeChecksum(fn, data, i, chunkLen, terminator);
            string checksumHex = checksum.ToString("X2");

            using var ms = new MemoryStream();
            ms.WriteByte(STX);
            ms.WriteByte(fn);
            ms.Write(data, i, chunkLen);
            ms.WriteByte(terminator);
            ms.Write(_options.Encoding.GetBytes(checksumHex));
            ms.WriteByte(CR);

            byte[] frame = ms.ToArray();

            bool frameAcked = false;
            for (int attempt = 0; attempt < 3 && !frameAcked; attempt++)
            {
                await stream.WriteAsync(frame, ct);
                using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                frameCts.CancelAfter(_options.AcknowledgeTimeoutMs);
                byte response = await ReadByteAsync(stream, frameCts.Token);
                if (response == ACK)
                    frameAcked = true;
                else
                    _logger.LogWarning("Frame {FN} NAK'd (attempt {A}); retrying.", fn, attempt + 1);
            }

            if (!frameAcked)
            {
                _logger.LogError("Frame {FN} not acknowledged after 3 attempts; aborting.", fn);
                return;
            }

            frameNumber++;
        }

        await stream.WriteAsync(new byte[] { EOT }, ct);
        _logger.LogDebug("ASTM order sent ({Chunks} frame(s)).", totalChunks);
    }

    /// <summary>
    /// Reads a single ASTM frame body after <c>STX</c> has been consumed.
    /// Returns the decoded text and whether this is an intermediate frame (ETB).
    /// Does NOT send ACK – the caller is responsible.
    /// </summary>
    private async Task<(string Text, bool IsIntermediate)> ReadFrameBodyAsync(NetworkStream stream, CancellationToken ct)
    {
        var raw = new List<byte>();
        var singleByte = new byte[1];
        bool isIntermediate = false;

        // Read until ETX or ETB
        while (true)
        {
            int n = await stream.ReadAsync(singleByte, ct);
            if (n == 0) break;

            byte b = singleByte[0];
            if (b == ETX) { isIntermediate = false; break; }
            if (b == ETB) { isIntermediate = true;  break; }

            raw.Add(b);
        }

        // Consume 2-byte checksum + CR
        var trailer = new byte[3];
        int read = 0;
        while (read < trailer.Length)
        {
            int n = await stream.ReadAsync(trailer.AsMemory(read, trailer.Length - read), ct);
            if (n == 0) break;
            read += n;
        }

        // Skip leading frame-number byte
        int skip = raw.Count > 0 ? 1 : 0;
        string text = _options.Encoding.GetString(raw.ToArray(), skip, Math.Max(0, raw.Count - skip));
        return (text, isIntermediate);
    }

    // -------------------------------------------------------------------------
    // Status
    // -------------------------------------------------------------------------

    private async Task PublishStatusAsync(string state, CancellationToken ct)
    {
        if (Transport is null) return;

        var payload = JsonSerializer.Serialize(new
        {
            instrumentId = _options.InstrumentId,
            status       = state,
            timestamp    = DateTimeOffset.UtcNow
        });

        var msg = new DataMessage
        {
            Body     = payload,
            Metadata = { ["Topic"] = StatusTopic, ["InstrumentId"] = _options.InstrumentId }
        };

        try
        {
            await Transport.SendAsync(msg, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish status '{State}'.", state);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<byte> ReadByteAsync(NetworkStream stream, CancellationToken ct)
    {
        var buf = new byte[1];
        int n = await stream.ReadAsync(buf, ct);
        if (n == 0) throw new EndOfStreamException("Stream closed while waiting for response byte.");
        return buf[0];
    }

    /// <summary>
    /// Computes the ASTM LLP checksum: sum of bytes from frame-number through terminator
    /// (inclusive), modulo 256.
    /// </summary>
    private static byte ComputeChecksum(byte frameNumber, byte[] data, int offset, int length, byte terminator)
    {
        int sum = frameNumber;
        for (int i = 0; i < length; i++)
            sum += data[offset + i];
        sum += terminator;
        return (byte)(sum % 256);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        _sessionSemaphore.Dispose();
    }
}
