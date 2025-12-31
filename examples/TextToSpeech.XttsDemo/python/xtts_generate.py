#!/usr/bin/env python3
"""
XTTS Text-to-Speech Generator

Loads a finetuned XTTS model and generates speech from text.
Designed to be called from C# demo application.
"""

import argparse
import sys
import os
from pathlib import Path

try:
    import torch
    from TTS.tts.configs.xtts_config import XttsConfig
    from TTS.tts.models.xtts import Xtts
except ImportError:
    print("ERROR: Required packages not installed. Install with:", file=sys.stderr)
    print("pip install TTS torch", file=sys.stderr)
    sys.exit(1)


def load_model(base_model_path: str, finetuned_checkpoint: str, device: str = "cpu"):
    """Load XTTS model with finetuned weights."""
    print(f"Loading base model from: {base_model_path}", file=sys.stderr)

    # Load config
    config_path = os.path.join(base_model_path, "config.json")
    config = XttsConfig()
    config.load_json(config_path)

    # Initialize model
    model = Xtts.init_from_config(config)
    model.load_checkpoint(config, checkpoint_dir=base_model_path, use_deepspeed=False)

    # Load finetuned weights
    print(f"Loading finetuned weights from: {finetuned_checkpoint}", file=sys.stderr)
    checkpoint = torch.load(finetuned_checkpoint, map_location=torch.device(device))
    model.load_state_dict(checkpoint["model"], strict=False)

    model.to(device)
    model.eval()

    print(f"Model loaded on device: {device}", file=sys.stderr)
    return model


def compute_speaker_latents(model, reference_audio_path: str, device: str = "cpu"):
    """Compute speaker conditioning from reference audio."""
    print(f"Computing speaker latents from: {reference_audio_path}", file=sys.stderr)

    gpt_cond_latent, speaker_embedding = model.get_conditioning_latents(
        audio_path=[reference_audio_path],
        gpt_cond_len=model.config.gpt_cond_len,
        max_ref_length=model.config.max_ref_len,
        sound_norm_refs=model.config.sound_norm_refs,
    )

    return gpt_cond_latent, speaker_embedding


def generate_speech(
    model,
    text: str,
    gpt_cond_latent,
    speaker_embedding,
    language: str = "cs",
    temperature: float = 0.75,
    repetition_penalty: float = 3.0,
    top_k: int = 50,
    top_p: float = 0.85,
):
    """Generate speech audio from text."""
    print(f"Generating speech for text: {text[:50]}...", file=sys.stderr)

    out = model.inference(
        text=text,
        language=language,
        gpt_cond_latent=gpt_cond_latent,
        speaker_embedding=speaker_embedding,
        temperature=temperature,
        repetition_penalty=repetition_penalty,
        top_k=top_k,
        top_p=top_p,
    )

    return out


def save_audio(audio_tensor, output_path: str, sample_rate: int = 24000):
    """Save audio tensor to WAV file."""
    import torchaudio

    # Convert to tensor if needed
    if not isinstance(audio_tensor, torch.Tensor):
        audio_tensor = torch.tensor(audio_tensor)

    # Ensure 2D tensor [channels, samples]
    if audio_tensor.ndim == 1:
        audio_tensor = audio_tensor.unsqueeze(0)

    torchaudio.save(output_path, audio_tensor.cpu(), sample_rate)
    print(f"Audio saved to: {output_path}", file=sys.stderr)


def main():
    parser = argparse.ArgumentParser(
        description="Generate speech using finetuned XTTS model"
    )

    # Model paths
    parser.add_argument(
        "--base-model",
        required=True,
        help="Path to base XTTS model directory",
    )
    parser.add_argument(
        "--finetuned",
        required=True,
        help="Path to finetuned checkpoint (.pth file)",
    )
    parser.add_argument(
        "--reference-audio",
        required=True,
        help="Path to reference audio file for voice cloning",
    )

    # Generation parameters
    parser.add_argument(
        "--text",
        required=True,
        help="Text to convert to speech",
    )
    parser.add_argument(
        "--output",
        required=True,
        help="Output audio file path (.wav)",
    )
    parser.add_argument(
        "--language",
        default="cs",
        help="Language code (default: cs)",
    )
    parser.add_argument(
        "--temperature",
        type=float,
        default=0.75,
        help="Sampling temperature (default: 0.75)",
    )
    parser.add_argument(
        "--repetition-penalty",
        type=float,
        default=3.0,
        help="Repetition penalty (default: 3.0)",
    )
    parser.add_argument(
        "--top-k",
        type=int,
        default=50,
        help="Top-k sampling (default: 50)",
    )
    parser.add_argument(
        "--top-p",
        type=float,
        default=0.85,
        help="Top-p (nucleus) sampling (default: 0.85)",
    )
    parser.add_argument(
        "--device",
        default="cpu",
        choices=["cpu", "cuda"],
        help="Device to use (default: cpu)",
    )

    args = parser.parse_args()

    # Validate paths
    if not os.path.exists(args.base_model):
        print(f"ERROR: Base model not found: {args.base_model}", file=sys.stderr)
        sys.exit(1)

    if not os.path.exists(args.finetuned):
        print(f"ERROR: Finetuned checkpoint not found: {args.finetuned}", file=sys.stderr)
        sys.exit(1)

    if not os.path.exists(args.reference_audio):
        print(f"ERROR: Reference audio not found: {args.reference_audio}", file=sys.stderr)
        sys.exit(1)

    # Load model
    model = load_model(args.base_model, args.finetuned, args.device)

    # Compute speaker conditioning
    gpt_cond_latent, speaker_embedding = compute_speaker_latents(
        model, args.reference_audio, args.device
    )

    # Generate speech
    audio_output = generate_speech(
        model,
        args.text,
        gpt_cond_latent,
        speaker_embedding,
        language=args.language,
        temperature=args.temperature,
        repetition_penalty=args.repetition_penalty,
        top_k=args.top_k,
        top_p=args.top_p,
    )

    # Save to file
    audio_tensor = torch.tensor(audio_output["wav"])
    save_audio(audio_tensor, args.output)

    print("SUCCESS", file=sys.stderr)


if __name__ == "__main__":
    main()
