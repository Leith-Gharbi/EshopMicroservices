namespace BuildingBlocks.Logging;

/// <summary>
/// Implementation of ICorrelationIdAccessor using AsyncLocal for thread-safe access
/// </summary>
public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Gets the correlation ID for the current async context
    /// </summary>
    public string? CorrelationId => _correlationId.Value;

    /// <summary>
    /// Sets the correlation ID for the current async context
    /// </summary>
    public void SetCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
        }

        _correlationId.Value = correlationId;
    }
}
