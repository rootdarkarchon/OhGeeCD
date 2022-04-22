using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using NAudio.Wave;
using Newtonsoft.Json;
using OhGeeCD.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace OhGeeCD.Sound
{
    [Serializable]
    public unsafe class SoundManager : IDisposable
    {
        private readonly PlayerConditionManager playerConditionState;

        [Signature("E8 ?? ?? ?? ?? 4D 39 BE ?? ?? ?? ??")]
        private readonly PlaySoundEffectDelegate PlayGameSoundEffect = null!;

        private SpeechSynthesizer speechSynthesizer;

        public SoundManager(PlayerConditionManager playerConditionState)
        {
            speechSynthesizer = new SpeechSynthesizer();
            speechSynthesizer.SetOutputToDefaultAudioDevice();
            AvailableVoices = speechSynthesizer.GetInstalledVoices();
            SetVoice(SelectedVoiceCulture);
            SignatureHelper.Initialise(this);
            this.playerConditionState = playerConditionState;
        }

        public delegate long PlaySoundEffectDelegate(int a1, long a2, long a3, int a4);

        [JsonIgnore]
        public ReadOnlyCollection<InstalledVoice> AvailableVoices { get; }

        [JsonProperty]
        public string SelectedVoiceCulture { get; set; } = "en-US";

        [JsonProperty]
        public int TTSVolume { get; set; } = 100;

        public void Dispose()
        {
            speechSynthesizer.Dispose();
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

        public void SetVoice(string cultureInfo, SpeechSynthesizer? synthesizer = null)
        {
            SelectedVoiceCulture = cultureInfo;
            if (synthesizer == null)
                speechSynthesizer.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, new System.Globalization.CultureInfo(cultureInfo));
            else
                synthesizer.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, new System.Globalization.CultureInfo(cultureInfo));
        }

        public void UnregisterSoundSource(ISoundSource soundSource)
        {
            soundSource.SoundEvent -= SoundEventTriggered;
        }

        private void SoundEventTriggered(object? sender, SoundEventArgs e)
        {
            if ((!playerConditionState?.ProcessingActive() ?? false) && !e.ForceSound) return;

            PluginLog.Debug("Playing " + e);

            _ = Task.Run(() =>
              {
                  if (e.SoundId > 0)
                  {
                      PlaySoundEffect(e.SoundId);
                  }

                  if (!string.IsNullOrEmpty(e.TextToSpeech))
                  {
                      var synth = new SpeechSynthesizer();
                      synth.SetOutputToDefaultAudioDevice();
                      SetVoice(SelectedVoiceCulture, synth);
                      synth.Volume = TTSVolume;
                      synth.Speak(e.TextToSpeech);
                  }

                  if (!string.IsNullOrEmpty(e.SoundPath))
                  {
                      if (!File.Exists(e.SoundPath)) return;

                      using var mf = new MediaFoundationReader(e.SoundPath);
                      using var wo = new WaveOutEvent();
                      wo?.Init(mf);
                      wo?.Play();
                      while (wo?.PlaybackState == PlaybackState.Playing)
                      {
                          Thread.Sleep(200);
                      }
                  }
              });
        }
    }
}