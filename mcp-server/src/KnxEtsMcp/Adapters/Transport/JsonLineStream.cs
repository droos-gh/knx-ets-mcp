using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KnxEtsMcp.Adapters.Transport;

/// <summary>
/// Newline-delimited JSON framing over a byte stream (named pipe or TCP socket):
/// one UTF-8 JSON object per line, bounded line length, read timeout.
/// </summary>
internal sealed class JsonLineStream
{
    /// <summary>Largest accepted response line (a big project's ga.list is a single line).</summary>
    public const int MaxFrameBytes = 16 * 1024 * 1024;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Stream _stream;
    private readonly byte[] _buffer = new byte[64 * 1024];
    private int _start;
    private int _end;

    public JsonLineStream(Stream stream)
    {
        _stream = stream;
    }

    /// <summary>Writes <paramref name="message"/> as one compact JSON line and flushes.</summary>
    public async Task WriteAsync(JsonObject message, CancellationToken cancellationToken)
    {
        var line = Utf8NoBom.GetBytes(message.ToJsonString() + "\n");
        await _stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the next line and parses it (a leading UTF-8 BOM is tolerated).</summary>
    /// <exception cref="TransportException">EOF, oversized line or invalid JSON.</exception>
    public async Task<JsonNode?> ReadAsync(CancellationToken cancellationToken)
    {
        using var line = new MemoryStream();
        while (true)
        {
            if (_start == _end)
            {
                _start = 0;
                _end = await _stream.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
                if (_end == 0)
                    throw new TransportException("Connection closed (EOF) while reading response");
            }

            var newline = Array.IndexOf(_buffer, (byte)'\n', _start, _end - _start);
            var take = (newline >= 0 ? newline : _end) - _start;
            if (line.Length + take > MaxFrameBytes)
                throw new TransportException($"Response exceeded max frame size ({MaxFrameBytes} bytes)");

            line.Write(_buffer, _start, take);
            if (newline >= 0)
            {
                _start = newline + 1;
                break;
            }
            _start = _end;
        }

        var text = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimStart('﻿');
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new TransportException("Response is not valid JSON", ex);
        }
    }
}
