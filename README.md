# Olbrasoft.TextToSpeech

[![Build & Publish](https://github.com/Olbrasoft/TextToSpeech/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/Olbrasoft/TextToSpeech/actions/workflows/publish-nuget.yml)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)

## Archive Notice

**Status:** Archived (2025-12-30)

This standalone ASP.NET Core service has been integrated directly into VirtualAssistant.
The TTS provider libraries (Core, Providers, Orchestration) are still actively maintained as NuGet packages.

**Migration:** See VirtualAssistant issue #404 for inline TTS integration details.

**Libraries still in use:**
- Olbrasoft.TextToSpeech.Core
- Olbrasoft.TextToSpeech.Providers
- Olbrasoft.TextToSpeech.Providers.EdgeTTS
- Olbrasoft.TextToSpeech.Providers.Piper
- Olbrasoft.TextToSpeech.Orchestration

**Archived project:**
- src/TextToSpeech.Service (ASP.NET Core API - no longer used)

---

A comprehensive .NET library for Text-to-Speech synthesis with multiple providers, circuit breaker pattern, and automatic fallback support.

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| **Olbrasoft.TextToSpeech.Core** | Core interfaces and models (ITtsProvider, TtsRequest, TtsResult) | [![NuGet](https://img.shields.io/nuget/v/Olbrasoft.TextToSpeech.Core.svg)](https://www.nuget.org/packages/Olbrasoft.TextToSpeech.Core/) |
| **Olbrasoft.TextToSpeech.Providers** | Provider implementations (Azure, EdgeTTS-HTTP, Google, VoiceRSS) | [![NuGet](https://img.shields.io/nuget/v/Olbrasoft.TextToSpeech.Providers.svg)](https://www.nuget.org/packages/Olbrasoft.TextToSpeech.Providers/) |
| **Olbrasoft.TextToSpeech.Providers.EdgeTTS** | EdgeTTS WebSocket provider - FREE, direct Microsoft API, no server needed | [![NuGet](https://img.shields.io/nuget/v/Olbrasoft.TextToSpeech.Providers.EdgeTTS.svg)](https://www.nuget.org/packages/Olbrasoft.TextToSpeech.Providers.EdgeTTS/) |
| **Olbrasoft.TextToSpeech.Providers.Piper** | Piper TTS provider for offline synthesis using ONNX models | [![NuGet](https://img.shields.io/nuget/v/Olbrasoft.TextToSpeech.Providers.Piper.svg)](https://www.nuget.org/packages/Olbrasoft.TextToSpeech.Providers.Piper/) |
| **Olbrasoft.TextToSpeech.Orchestration** | Orchestration with circuit breaker and multi-provider fallback | [![NuGet](https://img.shields.io/nuget/v/Olbrasoft.TextToSpeech.Orchestration.svg)](https://www.nuget.org/packages/Olbrasoft.TextToSpeech.Orchestration/) |

## Features

- **Multiple TTS Providers**: Azure Cognitive Services, EdgeTTS (WebSocket & HTTP), Google TTS, VoiceRSS, and Piper (offline)
- **Circuit Breaker Pattern**: Automatic failure detection with exponential backoff using Polly
- **Provider Fallback Chain**: Automatic failover to backup providers when primary fails
- **Configurable Priority**: Define provider priority order via configuration
- **FREE Edge TTS**: Direct WebSocket communication with Microsoft Edge TTS API - no API key or server required
- **Offline Support**: Piper provider works completely offline with ONNX neural voice models
- **Voice Profiles**: Configure different voice settings per use case

## Installation

```bash
# Core abstractions
dotnet add package Olbrasoft.TextToSpeech.Core

# Cloud providers (Azure, EdgeTTS-HTTP, Google, VoiceRSS)
dotnet add package Olbrasoft.TextToSpeech.Providers

# EdgeTTS WebSocket (FREE - direct Microsoft API, no server)
dotnet add package Olbrasoft.TextToSpeech.Providers.EdgeTTS

# Offline Piper provider
dotnet add package Olbrasoft.TextToSpeech.Providers.Piper

# Orchestration with circuit breaker
dotnet add package Olbrasoft.TextToSpeech.Orchestration
```

## Quick Start

### 1. Register Services

```csharp
// Program.cs
using Olbrasoft.TextToSpeech.Providers.Extensions;
using Olbrasoft.TextToSpeech.Providers.Piper.Extensions;
using Olbrasoft.TextToSpeech.Orchestration.Extensions;

builder.Services.AddTtsProviders(configuration);      // Azure, EdgeTTS-HTTP, Google, VoiceRSS
builder.Services.AddPiperTts(configuration);          // Piper (offline)

// Optional: Add EdgeTTS WebSocket (FREE, no server needed)
builder.Services.AddSingleton<ITtsProvider, Olbrasoft.TextToSpeech.Providers.EdgeTTS.EdgeTtsProvider>();

builder.Services.AddTtsOrchestration(configuration);  // Circuit breaker & fallback chain
```

### 2. Configure Providers

```json
{
  "TTS": {
    "Orchestration": {
      "Providers": [
        { "Name": "EdgeTTS-WebSocket", "Priority": 1, "Enabled": true },
        { "Name": "AzureTTS", "Priority": 2, "Enabled": true },
        { "Name": "GoogleTTS", "Priority": 3, "Enabled": true },
        { "Name": "Piper", "Priority": 4, "Enabled": true }
      ]
    },
    "EdgeTTS": {
      "Voice": "cs-CZ-AntoninNeural",
      "Rate": "+10%",
      "Volume": "+0%",
      "Pitch": "+0Hz"
    },
    "AzureTTS": {
      "SubscriptionKey": "your-key-here",
      "Region": "westeurope",
      "Voice": "cs-CZ-AntoninNeural"
    },
    "Google": {
      "Language": "cs",
      "Voice": "cs-CZ-Standard-A"
    },
    "Piper": {
      "ModelPath": "/path/to/model.onnx",
      "PiperPath": "piper"
    }
  }
}
```

### 3. Use the Service

```csharp
public class SpeechService
{
    private readonly ITtsProviderChain _ttsChain;

    public SpeechService(ITtsProviderChain ttsChain)
    {
        _ttsChain = ttsChain;
    }

    public async Task<byte[]?> SpeakAsync(string text, CancellationToken ct = default)
    {
        var request = new TtsRequest
        {
            Text = text,
            Voice = "cs-CZ-AntoninNeural",
            Rate = 10,  // +10%
            Pitch = 0   // default
        };

        var result = await _ttsChain.SynthesizeAsync(request, ct);

        if (result.Success)
        {
            Console.WriteLine($"Audio generated by: {result.ProviderUsed}");
            return result.Audio?.ToArray();
        }

        Console.WriteLine($"TTS failed: {result.ErrorMessage}");
        return null;
    }
}
```

## Supported Providers

| Provider | Type | API Key | Features |
|----------|------|---------|----------|
| **Azure Cognitive Services** | Cloud API | ✅ Required | High quality neural voices, SSML support, low latency |
| **EdgeTTS WebSocket** | Direct API | ❌ FREE | Microsoft voices, no server needed, WebSocket communication |
| **EdgeTTS HTTP** | HTTP Server | ❌ FREE | Requires edge-tts-server running on localhost |
| **Google TTS (gTTS)** | Cloud API | ❌ FREE | Google Translate TTS, simple and reliable |
| **VoiceRSS** | Cloud API | ✅ Required | Free tier (350 req/day), Czech voice "Josef" |
| **Piper** | Local ONNX | ❌ FREE | Completely offline, privacy-friendly, custom voice models |

## Circuit Breaker Configuration

```json
{
  "CircuitBreaker": {
    "FailureThreshold": 3,
    "ResetTimeout": "00:05:00",
    "UseExponentialBackoff": true,
    "MaxResetTimeout": "01:00:00"
  }
}
```

## Demo Application

A comprehensive demo console application is available in the `examples/` directory:

```bash
cd examples/TextToSpeech.Demo
dotnet run
```

**Features:**
- Interactive menu for testing all TTS providers
- Real-time timestamp in speech (proves it's not pre-recorded)
- Automatic audio playback using PipeWire/PulseAudio/ALSA
- Demonstrates automatic fallback mechanism
- No configuration required for EdgeTTS WebSocket and Google TTS

See [examples/TextToSpeech.Demo/README.md](examples/TextToSpeech.Demo/README.md) for detailed documentation.

## Project Structure

```
TextToSpeech/
├── src/                                    # Library packages (published to NuGet)
│   ├── TextToSpeech.Core/                  # Core interfaces and models
│   ├── TextToSpeech.Providers/             # Cloud providers (Azure, EdgeTTS-HTTP, Google, VoiceRSS)
│   ├── TextToSpeech.Providers.EdgeTTS/     # EdgeTTS WebSocket provider
│   ├── TextToSpeech.Providers.Piper/       # Piper offline provider
│   └── TextToSpeech.Orchestration/         # Circuit breaker and fallback chain
├── tests/                                  # Unit tests
│   ├── TextToSpeech.Core.Tests/
│   ├── TextToSpeech.Providers.Tests/
│   └── TextToSpeech.Orchestration.Tests/
└── examples/                               # Demo applications (not published)
    └── TextToSpeech.Demo/                  # Interactive console demo
```

## Building from Source

```bash
git clone https://github.com/Olbrasoft/TextToSpeech.git
cd TextToSpeech
dotnet build
dotnet test
```

## Requirements

- **.NET 10.0** or later
- **EdgeTTS WebSocket**: No requirements - works out of the box (FREE)
- **EdgeTTS HTTP**: edge-tts-server running on localhost:5555 (optional)
- **Piper**: piper-tts binary and ONNX voice model file
- **Azure/VoiceRSS**: API key from respective services

## License

MIT License - see [LICENSE](LICENSE) for details.

## Author

**Jiri Tuma** | [Olbrasoft](https://github.com/Olbrasoft)

---

<p align="center">
  <img src="./assets/text-to-speech.png" alt="Text-to-Speech" width="128" />
</p>

<p align="center"><strong>Copyright 2024-2025 Olbrasoft</strong></p>
