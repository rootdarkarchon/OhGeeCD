using Dalamud.Game;
using Dalamud.Logging;
using Newtonsoft.Json;
using System;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;

namespace Oh_gee_CD
{
    [Serializable]
    public class SoundManager : IDisposable
    {
        public delegate long PlaySoundEffectDelegate(int a1, long a2, long a3, int a4);
        public static PlaySoundEffectDelegate PlayGameSoundEffect { get; private set; }
        private SpeechSynthesizer speechSynthesizer;
        [JsonProperty]
        public int TTSVolume
        {
            get => ttsVolume; set
            {
                ttsVolume = value;
                if(speechSynthesizer != null)
                    speechSynthesizer.Volume = ttsVolume;
            }
        }
        private int ttsVolume = 100;

        public SoundManager(SigScanner scanner)
        {
            speechSynthesizer = new SpeechSynthesizer();
            speechSynthesizer.SetOutputToDefaultAudioDevice();
            PlayGameSoundEffect = Marshal.GetDelegateForFunctionPointer<PlaySoundEffectDelegate>(scanner.ScanText("E8 ?? ?? ?? ?? 4D 39 BE ?? ?? ?? ??"));
        }

        public static void PlaySoundEffect(int soundEffect)
        {
            if (soundEffect == 19 || soundEffect == 21) return;
            if (soundEffect <= 0) return;
            PlayGameSoundEffect(soundEffect, 0, 0, 0);
        }

        public void RegisterOGCD(OGCDAction ogcdAction)
        {
            ogcdAction.CooldownTriggered += OgcdAction_CooldownTriggered;
        }

        public void UnregisterOGCD(OGCDAction oGCDAction)
        {
            oGCDAction.CooldownTriggered -= OgcdAction_CooldownTriggered;
        }

        private void OgcdAction_CooldownTriggered(object? sender, CooldownTriggeredEventArgs e)
        {
            if (e.SoundId != 0)
            {
                PlaySoundEffect(e.SoundId);
            }

            if (!string.IsNullOrEmpty(e.TextToSpeech))
            {
                PluginLog.Debug("Playing: " + e.TextToSpeech);
                speechSynthesizer.SpeakAsync(e.TextToSpeech);
            }
        }

        public void Dispose()
        {
            speechSynthesizer.Dispose();
        }
    }
}