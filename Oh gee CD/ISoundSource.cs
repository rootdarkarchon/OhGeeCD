using System;

namespace Oh_gee_CD
{
    public interface ISoundSource
    {
        event EventHandler<SoundEventArgs>? SoundEvent;
    }
}