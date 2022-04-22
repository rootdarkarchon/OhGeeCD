using OhGeeCD.Util;
using System;

namespace OhGeeCD.Interfaces
{
    public interface ISoundSource
    {
        event EventHandler<SoundEventArgs>? SoundEvent;
    }
}