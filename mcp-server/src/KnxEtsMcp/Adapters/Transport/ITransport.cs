using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

/// <summary>
/// Carries complete protocol messages (one JSON object per line) to the ETS AddIn.
/// Implementations: <see cref="NamedPipeTransport"/> (same PC), <see cref="TcpTransport"/>
/// (remote ETS), <see cref="MockTransport"/> (in-memory fake for tests/development).
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>The session token sent with every request.</summary>
    string Token { get; }

    /// <summary>Establishes the connection (and, for the pipe, reads the session token).</summary>
    /// <exception cref="TransportException">The AddIn cannot be reached.</exception>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends one request and returns the parsed response line.</summary>
    /// <exception cref="TransportException">The connection failed, timed out or broke.</exception>
    Task<JsonNode?> SendAsync(JsonObject request, CancellationToken cancellationToken = default);

    /// <summary>Closes the connection; <see cref="ConnectAsync"/> may be called again.</summary>
    Task CloseAsync();
}

/// <summary>A connection-level failure (unreachable, timeout, EOF, oversized frame).</summary>
public sealed class TransportException : Exception
{
    public TransportException(string message) : base(message) { }

    public TransportException(string message, Exception inner) : base(message, inner) { }
}
