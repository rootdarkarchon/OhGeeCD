namespace Oh_gee_CD
{
    public class SoundEventArgs
    {
        public int SoundId { get; init; }
        public string TextToSpeech { get; init; }

        public SoundEventArgs(string textToSpeech, int soundId)
        {
            SoundId = soundId;
            TextToSpeech = textToSpeech;
        }
    }
}