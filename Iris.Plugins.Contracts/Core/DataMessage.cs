namespace Iris.Core;

/// <summary>
/// Represents a data payload travelling through the pipeline.
/// </summary>
public sealed class DataMessage
{
    /// <summary>Unique identifier for tracing this message.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Raw text body of the message.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Arbitrary metadata produced by the source.</summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
}
