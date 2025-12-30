# TextToSpeech Project - Claude Instructions

## 📦 Archive Status

**The standalone ASP.NET Core service (src/TextToSpeech.Service) has been archived as of 2025-12-30.**

**What's still active:**
- All NuGet library packages (Core, Providers, Orchestration) are actively maintained
- Used directly in VirtualAssistant project via inline integration (issue #404)

**What's archived:**
- src/TextToSpeech.Service (standalone ASP.NET Core API - no longer used)

---

## ⚠️ CRITICAL: Python Ban

**ABSOLUTE BAN on Python usage in this project.**

- ❌ NO Python scripts
- ❌ NO Python wrappers
- ❌ NO Python dependencies
- ❌ NO calling Python from C#
- ❌ NO Python-based solutions

**Why:**
- This is a C# project (net10.0)
- All TTS providers must be pure C# implementations
- Use ONNX Runtime, native libraries, or API calls only

## Approved TTS Providers

### ✅ Piper (RECOMMENDED - GUARANTEED FALLBACK)
- **Implementation**: `TextToSpeech.Providers.Piper` (already exists)
- **Technology**: ONNX Runtime (pure C#)
- **Models**: Pre-trained ONNX models (download ready-made)
- **Czech voices**: Available at https://rhasspy.github.io/piper-samples/
- **NO TRAINING REQUIRED** - use pre-trained models
- **CRITICAL**: Piper is configured as the FINAL FALLBACK with circuit breaker effectively disabled
- **Purpose**: Ensures notifications are ALWAYS read, even when all cloud providers fail
- **Configuration**: FailureThreshold=999999, no exponential backoff - always attempts synthesis

### ✅ EdgeTTS
- **Implementation**: `TextToSpeech.Providers.EdgeTTS` (already exists)
- **Technology**: WebSocket API (pure C#)
- **Voices**: Microsoft cloud voices
- **Quality**: High

### ✅ Azure Cognitive Services
- **Technology**: REST API (pure C#)
- **Voices**: Premium neural voices
- **Requires**: Azure subscription

## XTTS Situation

**XTTS is NOT SUPPORTED in this C# project.**

**Why:**
- XTTS is a complex PyTorch model (Python-only)
- Converting to ONNX failed (too complex)
- Porting to TorchSharp would take weeks
- The 2-day trained XTTS model CANNOT be used in C#

**Lesson learned:**
- Should have verified C# compatibility BEFORE training
- Python prototyping was a mistake for a C# production project

## Recommended Path Forward

**For high-quality Czech voice synthesis:**

1. **Use Piper with pre-trained model**:
   - Download: `cs_CZ-jirka-medium.onnx` from Piper samples
   - Configure in `appsettings.json`
   - Works immediately in pure C#
   - NO training needed

2. **Or use EdgeTTS**:
   - Excellent quality
   - No setup needed
   - Works immediately

## Project Structure

```
src/
  TextToSpeech.Core/          # Core interfaces
  TextToSpeech.Providers/     # Base provider classes
  TextToSpeech.Providers.Piper/      ✅ ONNX-based (C#)
  TextToSpeech.Providers.EdgeTTS/    ✅ API-based (C#)
  TextToSpeech.Providers.Xtts/       ❌ DELETE THIS (Python dependency)
```

## Action Items

1. ❌ **Delete** `TextToSpeech.Providers.Xtts` project (Python TorchSharp won't work)
2. ✅ **Use** `TextToSpeech.Providers.Piper` with pre-trained models
3. ✅ **Download** Czech Piper model from official repository
4. ✅ **Configure** Piper in demo application
5. ✅ **Test** with Czech text

## Never Again

- ❌ NO more Python in this project
- ❌ NO more "prototype in Python, port to C#" - verify compatibility FIRST
- ❌ NO more training models without verifying they work in target platform
