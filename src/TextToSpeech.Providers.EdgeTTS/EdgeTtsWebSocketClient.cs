using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Olbrasoft.TextToSpeech.Providers.EdgeTTS;

/// <summary>
/// WebSocket client for direct communication with Microsoft Edge TTS API.
/// </summary>
internal sealed class EdgeTtsWebSocketClient : IDisposable
{
    private const long WIN_EPOCH = 11644473600;
    private const double S_TO_NS = 1e9;

    private readonly EdgeTtsConfiguration _config;
    private ClientWebSocket? _webSocket;

    public EdgeTtsWebSocketClient(EdgeTtsConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Generates audio using Microsoft Edge TTS WebSocket API.
    /// </summary>
    /// <param name="text">Text to synthesize</param>
    /// <param name="voice">Voice name (e.g., cs-CZ-AntoninNeural)</param>
    /// <param name="rate">Speech rate (e.g., +10%)</param>
    /// <param name="volume">Volume (e.g., +0%)</param>
    /// <param name="pitch">Pitch (e.g., +0Hz)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audio data as byte array</returns>
    public async Task<byte[]> GenerateAudioAsync(
        string text,
        string? voice = null,
        string? rate = null,
        string? volume = null,
        string? pitch = null,
        CancellationToken cancellationToken = default)
    {
        voice = string.IsNullOrWhiteSpace(voice) ? _config.Voice : voice;
        rate = string.IsNullOrWhiteSpace(rate) ? _config.Rate : rate;
        volume = string.IsNullOrWhiteSpace(volume) ? _config.Volume : volume;
        pitch = string.IsNullOrWhiteSpace(pitch) ? _config.Pitch : pitch;

        _webSocket = new ClientWebSocket();

        try
        {
            // Configure WebSocket headers
            ConfigureWebSocketHeaders(_webSocket);

            // Build WebSocket URI
            var uri = BuildWebSocketUri();

            // Connect
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_config.Timeout);
            await _webSocket.ConnectAsync(uri, connectCts.Token);

            // Send speech config
            await SendSpeechConfigAsync(_webSocket, cancellationToken);

            // Send SSML request
            await SendSsmlRequestAsync(_webSocket, text, voice, rate, volume, pitch, cancellationToken);

            // Receive audio data
            var audioData = await ReceiveAudioDataAsync(_webSocket, cancellationToken);

            // Close connection gracefully
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            }

            return audioData;
        }
        finally
        {
            _webSocket?.Dispose();
            _webSocket = null;
        }
    }

    private void ConfigureWebSocketHeaders(ClientWebSocket client)
    {
        // Enable WebSocket compression (compress=15 in Python edge-tts)
        client.Options.DangerousDeflateOptions = new WebSocketDeflateOptions
        {
            ClientMaxWindowBits = 15,
            ServerMaxWindowBits = 15
        };

        var chromiumMajor = EdgeTtsConfiguration.CHROMIUM_FULL_VERSION.Split('.')[0];
        var muid = GenerateMuid();

        client.Options.SetRequestHeader("User-Agent",
            $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromiumMajor}.0.0.0 Safari/537.36 Edg/{chromiumMajor}.0.0.0");
        client.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
        client.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        client.Options.SetRequestHeader("Pragma", "no-cache");
        client.Options.SetRequestHeader("Cache-Control", "no-cache");
        client.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");

        // Set MUID as both cookie and header
        client.Options.SetRequestHeader("Cookie", $"muid={muid}");
        client.Options.SetRequestHeader("x-ms-useragent", $"azsdk-js-cognitiveservices-speech-sdk/1.0.0 Electron/{chromiumMajor}.0.0");
    }

    private static Uri BuildWebSocketUri()
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var secMsGec = GenerateSecMsGec();
        var secMsGecVersion = $"1-{EdgeTtsConfiguration.CHROMIUM_FULL_VERSION}";
        return new Uri($"{EdgeTtsConfiguration.WSS_URL}&ConnectionId={connectionId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={secMsGecVersion}");
    }

    private async Task SendSpeechConfigAsync(ClientWebSocket client, CancellationToken cancellationToken)
    {
        var timestamp = DateToString();
        var jsonPayload = "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{" +
                         "\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}," +
                         "\"outputFormat\":\"" + _config.OutputFormat + "\"}}}}";

        var configMessage = $"X-Timestamp:{timestamp}\r\n" +
                           "Content-Type:application/json; charset=utf-8\r\n" +
                           "Path:speech.config\r\n\r\n" +
                           jsonPayload;

        await client.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(configMessage)),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    private async Task SendSsmlRequestAsync(
        ClientWebSocket client,
        string text,
        string voice,
        string rate,
        string volume,
        string pitch,
        CancellationToken cancellationToken)
    {
        var timestamp = DateToString();
        var requestId = Guid.NewGuid().ToString("N");
        var ssml = GenerateSsml(text, voice, rate, volume, pitch);
        var ssmlMessage = $"X-RequestId:{requestId}\r\n" +
                         "Content-Type:application/ssml+xml\r\n" +
                         "Path:ssml\r\n\r\n" +
                         ssml;

        await client.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(ssmlMessage)),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    private static async Task<byte[]> ReceiveAudioDataAsync(ClientWebSocket client, CancellationToken cancellationToken)
    {
        var audioChunks = new List<byte>();
        var buffer = new byte[16384];

        while (client.State == WebSocketState.Open)
        {
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                ProcessBinaryMessage(buffer, result.Count, audioChunks);
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (message.Contains("Path:turn.end"))
                {
                    break;
                }
            }
        }

        return audioChunks.ToArray();
    }

    private static void ProcessBinaryMessage(byte[] buffer, int count, List<byte> audioChunks)
    {
        if (count < 2) return;

        var headerLength = (buffer[0] << 8) | buffer[1];
        var audioStart = 2 + headerLength;

        if (audioStart > count) return;

        var headerBytes = buffer.Skip(2).Take(headerLength).ToArray();
        var headerText = Encoding.UTF8.GetString(headerBytes);

        if (headerText.Contains("Path:audio"))
        {
            var audioBytes = count - audioStart;
            audioChunks.AddRange(buffer.Skip(audioStart).Take(audioBytes));
        }
    }

    private static string GenerateSsml(string text, string voice, string rate, string volume, string pitch)
    {
        var escapedText = System.Security.SecurityElement.Escape(text);
        return "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
               $"<voice name='{voice}'>" +
               $"<prosody pitch='{pitch}' rate='{rate}' volume='{volume}'>" +
               escapedText +
               "</prosody>" +
               "</voice>" +
               "</speak>";
    }

    private static string DateToString()
    {
        return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'",
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GenerateMuid()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private static string GenerateSecMsGec()
    {
        var ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ticks += WIN_EPOCH;
        ticks -= ticks % 300;
        var ticksInNs = (double)ticks * S_TO_NS / 100;
        var strToHash = $"{ticksInNs:F0}{EdgeTtsConfiguration.TRUSTED_CLIENT_TOKEN}";
        var hashBytes = SHA256.HashData(Encoding.ASCII.GetBytes(strToHash));
        return Convert.ToHexString(hashBytes);
    }

    public void Dispose()
    {
        _webSocket?.Dispose();
    }
}
