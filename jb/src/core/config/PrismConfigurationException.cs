namespace Prism.Core;

/// <summary>
/// Thrown when PRISM-owned configuration is missing, unreadable, or invalid.
/// PRISM-owned failures are never converted into per-image KO records.
/// </summary>
public sealed class PrismConfigurationException : InvalidOperationException
{
    /// <summary>
    /// Creates a configuration exception with a safe message.
    /// </summary>
    /// <param name="message">Safe failure description that will surface in health/diagnostic responses.</param>
    public PrismConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a configuration exception wrapping an inner cause.
    /// </summary>
    /// <param name="message">Safe failure description.</param>
    /// <param name="innerException">Underlying exception.</param>
    public PrismConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
