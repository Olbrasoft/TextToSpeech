# XTTS Demo - Finetuned Text-to-Speech

Demo application showcasing **XTTS (Extended Text-to-Speech)** with a custom-trained voice model (Jan Hyhlík).

This C# console application demonstrates:
- Using a finetuned XTTS model for high-quality Czech speech synthesis
- Calling Python XTTS library from C#
- Playing generated audio via PipeWire (pw-cat)

## 🎯 Features

- **Finetuned voice cloning** - Uses Jan Hyhlík's voice (trained on 13.5 hours of audiobook data)
- **Czech language support** - Optimized for Czech text-to-speech
- **Simple CLI interface** - Enter text interactively or pass as command-line argument
- **High-quality synthesis** - Configurable temperature, repetition penalty, and sampling parameters
- **Audio playback** - Automatically plays generated speech via PipeWire

## 📋 Prerequisites

### .NET Requirements
- **.NET 10.0** SDK or later
- **Linux** (tested on Debian 13)

### Python Requirements
- **Python 3.10+** with the following packages:
  ```bash
  pip install TTS torch torchaudio
  ```

### Audio Playback
- **pw-cat** (PipeWire) for audio playback
  ```bash
  sudo apt install pipewire pipewire-audio-client-libraries
  ```

### XTTS Model Files

The demo is configured to use the trained Jan Hyhlík voice model. Ensure these paths exist:

- **Base model**: `/home/jirka/projekty/xtts-test/output_hyhlik_cpu/run/training/XTTS_v2.0_original_model_files`
- **Finetuned checkpoint**: `/home/jirka/projekty/xtts-test/output_hyhlik_cpu_continue/run/training/GPT_XTTS_FT-December-24-2025_10+28PM-eff0407/best_model_9685.pth`
- **Reference audio**: `/home/jirka/projekty/xtts-test/hyhlik_dataset/wavs/vocals_sample_001_5.7s.wav`

> **Note**: To use a different voice model, edit the constants in `Program.cs`:
> - `BaseModelPath`
> - `FinetunedCheckpoint`
> - `ReferenceAudio`

## 🚀 Usage

### Build and Run

```bash
# Navigate to project directory
cd examples/TextToSpeech.XttsDemo

# Build the project
dotnet build

# Run interactively
dotnet run

# Or pass text directly
dotnet run "Dobrý den, toto je ukázka hlasové syntézy."
```

### Interactive Mode

```
╔═══════════════════════════════════════════════════════════╗
║   XTTS Demo - Finetuned Text-to-Speech (Jan Hyhlík)      ║
╚═══════════════════════════════════════════════════════════╝

Enter text to synthesize (Czech): Vítejte v ukázkové aplikaci.

📝 Text: Vítejte v ukázkové aplikaci.

🎙️  Generating speech...
   Script: /path/to/python/xtts_generate.py
   Output: /tmp/xtts_output_<guid>.wav

   [Python] Loading base model from: ...
   [Python] Loading finetuned weights from: ...
   [Python] Model loaded on device: cpu
   [Python] Computing speaker latents from: ...
   [Python] Generating speech for text: Vítejte v ukázkové aplikaci.
   [Python] Audio saved to: /tmp/xtts_output_<guid>.wav
   [Python] SUCCESS
✅ Speech generated successfully

🔊 Playing audio...
✅ Playback finished
🧹 Cleaned up temporary audio file

✅ Done!
```

## 🔧 How It Works

1. **C# Application** (`Program.cs`):
   - Accepts text input (interactively or via command-line)
   - Calls Python wrapper script with model paths and parameters
   - Waits for audio generation to complete
   - Plays the generated audio file via `pw-cat`
   - Cleans up temporary files

2. **Python Wrapper** (`python/xtts_generate.py`):
   - Loads the XTTS base model
   - Applies finetuned weights (custom voice)
   - Computes speaker conditioning from reference audio
   - Generates speech from input text
   - Saves audio to WAV file

3. **Audio Playback**:
   - Uses PipeWire's `pw-cat -p <file>` command
   - Falls back gracefully if `pw-cat` is not available

## ⚙️ Configuration

### Model Parameters

The demo uses these default parameters (configurable in `Program.cs:BuildPythonArguments()`):

- **Temperature**: 0.75 (balance between consistency and naturalness)
- **Repetition Penalty**: 3.0 (prevents repetitive speech patterns)
- **Top-k**: 50 (sampling diversity)
- **Top-p**: 0.85 (nucleus sampling threshold)
- **Language**: `cs` (Czech)
- **Device**: `cpu` (or `cuda` if GPU available)

### Changing Voice Models

To use a different trained voice:

1. Edit constants in `Program.cs`:
   ```csharp
   private const string BaseModelPath = "/path/to/base/model";
   private const string FinetunedCheckpoint = "/path/to/finetuned.pth";
   private const string ReferenceAudio = "/path/to/reference.wav";
   ```

2. Optionally change language:
   ```csharp
   private const string Language = "en"; // for English
   ```

## 📝 Example Texts

Try these Czech texts to test the voice quality:

```bash
dotnet run "Dobrý den, vítejte v ukázkové aplikaci."
dotnet run "Toto je demonstrace hlasové syntézy pomocí modelu XTTS."
dotnet run "Kvalita hlasu závisí na tréninkových datech a parametrech modelu."
```

## 🐛 Troubleshooting

### "Python script failed with exit code 1"

**Cause**: Python dependencies not installed or model files not found

**Solution**:
```bash
# Install Python dependencies
pip install TTS torch torchaudio

# Verify model paths exist
ls -l /home/jirka/projekty/xtts-test/output_hyhlik_cpu_continue/run/training/GPT_XTTS_FT-December-24-2025_10+28PM-eff0407/best_model_9685.pth
```

### "pw-cat not found"

**Cause**: PipeWire not installed

**Solution**:
```bash
sudo apt install pipewire pipewire-audio-client-libraries
```

The application will skip playback but save the audio file to `/tmp/`.

### "XTTS generator script not found"

**Cause**: Python script not copied to output directory

**Solution**:
```bash
# Rebuild the project
dotnet clean
dotnet build
```

## 📚 Related

- **Python GUI Demo**: `/home/jirka/projekty/xtts-test/gui_app.py` - PyQt5 GUI with streaming playback
- **Training Scripts**: `/home/jirka/projekty/xtts-test/` - XTTS model training
- **XTTS Documentation**: https://github.com/coqui-ai/TTS

## 📄 License

This demo application is part of the Olbrasoft TextToSpeech library.

---

**Training Details for Jan Hyhlík Voice:**
- **Dataset**: Nepohřbený rytíř (2519 samples) + Boskovická svodnice (1937 samples)
- **Total Training**: 13.5 hours (CPU)
- **Epochs**: 5
- **Final Model**: best_model_9685.pth
- **Quality**: High-fidelity Czech voice suitable for audiobook narration
