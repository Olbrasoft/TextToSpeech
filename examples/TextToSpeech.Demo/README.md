# TextToSpeech Library - Demo Application

Interactive console application demonstrating the usage of the **Olbrasoft TextToSpeech** library with multiple TTS providers and automatic fallback mechanism.

## 📋 Overview

This demo application showcases:

- **5 TTS providers** with different characteristics (cloud API, WebSocket, local ONNX)
- **Automatic fallback orchestration** with circuit breaker pattern
- **Real-time audio generation** with timestamp proof (not pre-recorded)
- **Automatic audio playback** using PipeWire/PulseAudio/ALSA
- **Configuration pattern** demonstration (hosting app vs. library responsibility)

## 🎯 Supported TTS Providers

| # | Provider | Type | Requirements | Free? | Notes |
|---|----------|------|--------------|-------|-------|
| 1 | **Azure Cognitive Services** | Cloud API | API key + region | ❌ Pay-as-you-go | High quality, low latency |
| 2 | **EdgeTTS (WebSocket)** | WebSocket | None | ✅ Free | Direct Microsoft API, no server needed |
| 3 | **VoiceRSS** | Cloud API | API key | ⚠️ 350 req/day | Free tier available |
| 4 | **Google TTS (gTTS)** | Cloud API | None | ✅ Free | Simple, works out of the box |
| 5 | **Piper (ONNX)** | Local | ONNX model file | ✅ Free | Offline, privacy-friendly |
| 6 | **Orchestration** | Fallback chain | See above | Mixed | Automatic provider selection |

## 🚀 Quick Start

### Prerequisites

- **.NET 10.0** SDK
- **Linux** (tested on Debian 13)
- **Audio player**: `pw-cat` (PipeWire), `paplay` (PulseAudio), or `aplay` (ALSA)

### Run the Demo

```bash
cd ~/Olbrasoft/TextToSpeech/src/TextToSpeech.Demo
dotnet run
```

### Try Without Configuration

The following providers work **immediately without any configuration**:

- **Option 2**: EdgeTTS (WebSocket) - Direct Microsoft API
- **Option 4**: Google TTS - Free cloud service

Just select option 2 or 4 and press Enter!

## 📖 Detailed Provider Information

### 1. Azure Cognitive Services TTS

**Type**: Cloud API (HTTPS REST)
**Requirements**:
- Azure subscription key
- Azure region (e.g., `westeurope`)

**How to get credentials**:
1. Create Azure account: https://azure.microsoft.com/free/
2. Create Speech Service resource
3. Copy key and region from Azure Portal

**Configuration**:
```json
"TTS": {
  "Azure": {
    "SubscriptionKey": "your-key-here",
    "Region": "westeurope",
    "Voice": "cs-CZ-AntoninNeural"
  }
}
```

**Environment variables** (optional):
```bash
export AZURE_SPEECH_KEY="your-key-here"
export AZURE_SPEECH_REGION="westeurope"
```

**Characteristics**:
- ✅ High quality neural voices
- ✅ Low latency (~500-1000ms)
- ✅ Supports SSML, prosody control
- ❌ Requires API key
- ❌ Costs money (pay-as-you-go)

---

### 2. EdgeTTS (WebSocket)

**Type**: WebSocket (Direct Microsoft API)
**Requirements**: **NONE** ✅

**How it works**:
- Communicates **directly** with Microsoft Edge TTS WebSocket API
- **No HTTP server needed** (unlike EdgeTTS-HTTP variant)
- Uses same voices as Azure TTS
- Completely free to use

**Configuration**:
```json
"TTS": {
  "EdgeTTS": {
    "Voice": "cs-CZ-AntoninNeural",
    "Rate": "+10%",
    "Volume": "+0%",
    "Pitch": "+0Hz"
  }
}
```

**Characteristics**:
- ✅ **FREE** - no API key needed
- ✅ Same voices as Azure TTS
- ✅ Works out of the box
- ✅ Direct WebSocket communication
- ⚠️ Slightly higher latency than Azure (~2-3 seconds)
- ⚠️ No official SLA or support

**Technical details**:
- Provider name: `EdgeTTS-WebSocket`
- Implementation: `TextToSpeech.Providers.EdgeTTS.EdgeTtsProvider`
- Output: `MemoryAudioData` (audio in memory, then saved to temp file for playback)

---

### 3. VoiceRSS

**Type**: Cloud API (HTTPS REST)
**Requirements**: API key

**How to get credentials**:
1. Register at https://www.voicerss.org/
2. Free tier: **350 requests/day**
3. Copy API key from dashboard

**Configuration**:
```json
"TTS": {
  "VoiceRSS": {
    "ApiKey": "your-api-key-here",
    "Language": "cs-cz",
    "Voice": "Josef"
  }
}
```

**Environment variable** (optional):
```bash
export VOICERSS_API_KEY="your-api-key-here"
```

**Characteristics**:
- ✅ Free tier available (350 req/day)
- ✅ Czech male voice "Josef"
- ✅ Simple REST API
- ❌ Requires API key
- ⚠️ Limited daily quota on free tier

---

### 4. Google TTS (gTTS)

**Type**: Cloud API (Google Translate TTS)
**Requirements**: **NONE** ✅

**How it works**:
- Uses Google Translate's TTS endpoint
- Completely free
- No authentication needed

**Configuration**:
```json
"TTS": {
  "Google": {
    "Language": "cs",
    "TopLevelDomain": "com"
  }
}
```

**Characteristics**:
- ✅ **FREE** - no API key needed
- ✅ Works out of the box
- ✅ Simple and reliable
- ⚠️ Lower quality than neural voices
- ⚠️ Higher latency (~5-10 seconds)
- ⚠️ No official API (uses Google Translate)

---

### 5. Piper (Local ONNX)

**Type**: Local ONNX model
**Requirements**:
- Piper ONNX model file (`.onnx`)
- Model config file (`.onnx.json`)

**How to get models**:
1. Download from https://github.com/rhasspy/piper/releases
2. Czech models: https://huggingface.co/rhasspy/piper-voices/tree/main/cs/cs_CZ
3. Place in local directory (e.g., `/home/user/piper-models/`)

**Configuration**:
```json
"TTS": {
  "Piper": {
    "ModelPath": "/home/jirka/piper-models/cs_CZ-jirka-medium.onnx",
    "SpeakerId": 0,
    "LengthScale": 1.0
  }
}
```

**Characteristics**:
- ✅ **Completely offline** - no internet needed
- ✅ **Privacy-friendly** - audio never leaves your machine
- ✅ Fast inference (~500ms)
- ✅ Multiple voice models available
- ❌ Requires model download (~50-200MB per model)
- ⚠️ Quality depends on model size

---

### 6. Orchestration (Fallback Chain)

**Type**: Automatic provider selection with circuit breaker
**Requirements**: At least one working provider

**How it works**:
1. Tries providers in **priority order** (1 = highest)
2. If provider fails → **circuit breaker opens** (temporary disable)
3. Automatically tries **next provider** in chain
4. Failed providers recover after timeout
5. Returns result from **first successful provider**

**Configuration**:
```json
"TTS": {
  "Orchestration": {
    "Providers": [
      { "Name": "AzureTTS", "Priority": 1, "Enabled": true, "FailureThreshold": 3 },
      { "Name": "EdgeTTS-WebSocket", "Priority": 2, "Enabled": true },
      { "Name": "VoiceRSS", "Priority": 3, "Enabled": true },
      { "Name": "GoogleTTS", "Priority": 4, "Enabled": true },
      { "Name": "Piper", "Priority": 5, "Enabled": true }
    ]
  }
}
```

**Fallback Demo**:
- Option 6 demonstrates fallback mechanism
- Azure TTS has **intentionally empty API key**
- Orchestration automatically falls back to **EdgeTTS-WebSocket**
- Shows attempted providers and error messages

**Characteristics**:
- ✅ High availability - no single point of failure
- ✅ Automatic recovery from transient errors
- ✅ Circuit breaker prevents cascading failures
- ✅ Configurable priority and thresholds
- 📊 Detailed attempt history for debugging

## ⚙️ Configuration

### Configuration File: `appsettings.json`

The demo application follows the **hosting application pattern**:

1. **Hosting app** (this demo) is responsible for:
   - Loading configuration from `appsettings.json`
   - Reading environment variables
   - Calling `services.Configure<T>()` to populate options

2. **Library** (TextToSpeech.Providers) is responsible for:
   - Registering services with `services.AddTtsProviders()`
   - **NOT** loading configuration values

### Output Configuration

Controls how audio is stored:

```json
"TTS": {
  "Output": {
    "Mode": "File",                          // "File", "Memory", or "Both"
    "OutputDirectory": "./audio-output",     // Where to save files
    "FileNamePattern": "{provider}_{timestamp}.mp3"
  }
}
```

**Output modes**:
- `File` - Save to disk as MP3 file
- `Memory` - Return audio as byte array in memory
- `Both` - Save to disk AND return in memory

## 🎵 Audio Playback

The demo automatically plays generated audio using available audio systems:

**Priority order**:
1. **pw-cat** (PipeWire) - Modern Linux audio system
2. **paplay** (PulseAudio) - Widely supported
3. **aplay** (ALSA) - Basic but universal

**How it works**:
- Checks which audio player is available using `which` command
- Uses `-p` flag for `pw-cat` (playback mode)
- For `MemoryAudioData`, saves to `/tmp/tts-demo-*.mp3` first

## 🔧 Troubleshooting

### "Invalid choice!" Error

**Cause**: Provider not found in service collection
**Solution**: Check that provider name matches exactly:
- Azure: `AzureTTS`
- EdgeTTS: `EdgeTTS-WebSocket` (NOT `EdgeTTS-HTTP`)
- VoiceRSS: `VoiceRSS`
- Google: `GoogleTTS`
- Piper: `Piper`

### "API key not configured"

**Cause**: Azure or VoiceRSS missing API key
**Solution**:
1. Add key to `appsettings.json`
2. OR set environment variable (`AZURE_SPEECH_KEY`, `VOICERSS_API_KEY`)
3. OR use providers that don't need keys (EdgeTTS, Google TTS)

### "Connection refused"

**Cause**: EdgeTTS-HTTP provider trying to connect to localhost:5555
**Solution**: This should NOT happen - demo uses EdgeTTS-WebSocket (direct API)

### "No audio player found"

**Cause**: Missing `pw-cat`, `paplay`, and `aplay`
**Solution**: Install audio system:
```bash
# PipeWire (recommended)
sudo apt install pipewire pipewire-pulse

# OR PulseAudio
sudo apt install pulseaudio

# OR ALSA (basic)
sudo apt install alsa-utils
```

### Audio Generated But Not Playing

**Cause**: MemoryAudioData not saved to temp file
**Solution**: Update to latest version - automatic temp file creation added

## 📊 Real-Time Validation

Each generated audio includes **current date and time** in Czech:

```
"Toto je kontrolní text pro testování TTS knihovny.
Dnes je sobota, 20.12.2025 a čas je 12:51:39.
Tata nahrávka je vygenerována v reálném čase, není to přednahraný audio soubor."
```

This **proves** the audio is generated fresh, not pre-recorded.

## 🏗️ Architecture

### Dependency Injection Pattern

```csharp
// Step 1: Hosting app configures providers (loads values from appsettings.json)
ConfigureTtsProviders(services, configuration);

// Step 2: Library registers services (does NOT configure values)
services.AddTtsProviders(configuration);
services.AddPiperTts(configuration);
services.AddTtsOrchestration(configuration);
```

### Project References

```xml
<ProjectReference Include="..\TextToSpeech.Core\TextToSpeech.Core.csproj" />
<ProjectReference Include="..\TextToSpeech.Providers\TextToSpeech.Providers.csproj" />
<ProjectReference Include="..\TextToSpeech.Providers.EdgeTTS\TextToSpeech.Providers.EdgeTTS.csproj">
  <Aliases>EdgeTtsWebSocket</Aliases>  <!-- Avoid naming collision -->
</ProjectReference>
<ProjectReference Include="..\TextToSpeech.Providers.Piper\TextToSpeech.Providers.Piper.csproj" />
<ProjectReference Include="..\TextToSpeech.Orchestration\TextToSpeech.Orchestration.csproj" />
```

### Extern Alias for EdgeTTS

To avoid naming collision between `EdgeTTS-HTTP` and `EdgeTTS-WebSocket`:

```csharp
extern alias EdgeTtsWebSocket;

using EdgeTtsProvider = EdgeTtsWebSocket::Olbrasoft.TextToSpeech.Providers.EdgeTTS.EdgeTtsProvider;
```

## 📚 Further Reading

- **Library documentation**: `../README.md`
- **Provider comparison**: See table above
- **Configuration best practices**: CLAUDE.md in repository root
- **Architecture decisions**: Why hosting app handles configuration

## 🎯 Use Cases

This demo is useful for:

1. **Evaluating TTS providers** - Compare quality, latency, cost
2. **Testing configuration** - Verify appsettings.json before production
3. **Learning the library** - See real-world usage patterns
4. **Debugging issues** - Detailed error messages and attempt history
5. **Prototyping** - Quick test of TTS integration

## 📝 Notes

- **All commits are local only** - No auto-deploy on push
- **Audio files** in `audio-output/` are gitignored
- **Temp files** in `/tmp/tts-demo-*.mp3` for MemoryAudioData playback
- **No server required** - EdgeTTS-WebSocket communicates directly

## 🤝 Contributing

This is a demo application for the TextToSpeech library. For issues or feature requests:

1. Check existing issues in main repository
2. Test with individual providers first
3. Include full error messages and configuration
4. Specify which provider (1-6) you're testing

---

**Generated with** [Claude Code](https://claude.com/claude-code)
**License**: Same as TextToSpeech library
**Version**: 1.0.0
