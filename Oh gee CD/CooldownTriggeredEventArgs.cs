namespace OhGeeCD
{
    public class CooldownTriggeredEventArgs
    {
        public int SoundId { get; init; }
        public string TextToSpeech { get; init; }

        public CooldownTriggeredEventArgs(string textToSpeech, int soundId)
        {
            SoundId = soundId;
            TextToSpeech = textToSpeech;
        }
    }
}