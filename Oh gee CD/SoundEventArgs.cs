namespace Oh_gee_CD
{
    public class SoundEventArgs
    {
        public int SoundId { get; init; }
        public string TextToSpeech { get; init; }
        public string SoundPath { get; init; }
        public bool ForceSound { get; set; } = false;

        public SoundEventArgs(string? textToSpeech, int? soundId, string? soundPath)
        {
            SoundId = soundId ?? 0;
            TextToSpeech = textToSpeech ?? string.Empty;
            SoundPath = soundPath ?? string.Empty;
        }

        public override string ToString()
        {
            return $"SoundEvent; Forced: {ForceSound}; TTS: {TextToSpeech}; SoundId: {SoundId}; SoundPath: {SoundPath}";
        }
    }
}