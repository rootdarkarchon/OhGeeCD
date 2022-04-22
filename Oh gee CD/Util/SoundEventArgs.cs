namespace OhGeeCD.Util
{
    public class SoundEventArgs
    {
        public SoundEventArgs(string? textToSpeech, int? soundId, string? soundPath)
        {
            SoundId = soundId ?? 0;
            TextToSpeech = textToSpeech ?? string.Empty;
            SoundPath = soundPath ?? string.Empty;
        }

        public bool ForceSound { get; set; } = false;
        public int SoundId { get; init; }
        public string SoundPath { get; init; }
        public string TextToSpeech { get; init; }

        public override string ToString()
        {
            return $"SoundEvent; Forced: {ForceSound}; TTS: {TextToSpeech}; SoundId: {SoundId}; SoundPath: {SoundPath}";
        }
    }
}