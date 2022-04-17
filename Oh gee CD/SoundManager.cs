using System;
using System.Speech.Synthesis;

namespace Oh_gee_CD
{
    public class SoundManager : IDisposable
    {
        private SpeechSynthesizer speechSynthesizer;

        public SoundManager()
        {
            speechSynthesizer = new SpeechSynthesizer();
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
            if (!string.IsNullOrEmpty(e.TextToSpeech))
            {
                speechSynthesizer.Speak(e.TextToSpeech);
            }
        }

        public void Dispose()
        {
            // nothing yet but something probably
        }
    }
}