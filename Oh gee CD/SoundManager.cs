using Dalamud.Utility.Signatures;
using Newtonsoft.Json;
using System;
using System.Speech.Synthesis;

namespace Oh_gee_CD
{
    [Serializable]
    public unsafe class SoundManager : IDisposable
    {
        public delegate long PlaySoundEffectDelegate(int a1, long a2, long a3, int a4);

        [Signature("E8 ?? ?? ?? ?? 4D 39 BE ?? ?? ?? ??")]
        private readonly PlaySoundEffectDelegate PlayGameSoundEffect = null!;

        private SpeechSynthesizer speechSynthesizer;
        [JsonProperty]
        public int TTSVolume
        {
            get => ttsVolume; set
            {
                ttsVolume = value;
                if (speechSynthesizer != null)
                    speechSynthesizer.Volume = ttsVolume;
            }
        }
        private int ttsVolume = 100;

        public SoundManager()
        {
            speechSynthesizer = new SpeechSynthesizer();
            speechSynthesizer.SetOutputToDefaultAudioDevice();
            SignatureHelper.Initialise(this);
        }

        public void PlaySoundEffect(int soundEffect)
        {
            if (soundEffect == 19 || soundEffect == 21) return;
            if (soundEffect <= 0) return;
            if (PlayGameSoundEffect != null)
            {
                _ = PlayGameSoundEffect(soundEffect, 0, 0, 0);
            }
        }

        public void RegisterSoundSource(ISoundSource soundSource)
        {
            soundSource.SoundEvent += SoundEventTriggered;
        }

        public void UnregisterSoundSource(ISoundSource soundSource)
        {
            soundSource.SoundEvent -= SoundEventTriggered;
        }

        private void SoundEventTriggered(object? sender, SoundEventArgs e)
        {
            if (e.SoundId > 0)
            {
                PlaySoundEffect(e.SoundId);
            }

            if (!string.IsNullOrEmpty(e.TextToSpeech))
            {
                speechSynthesizer.SpeakAsync(e.TextToSpeech);
            }
        }

        public void Dispose()
        {
            speechSynthesizer.Dispose();
        }
    }
}