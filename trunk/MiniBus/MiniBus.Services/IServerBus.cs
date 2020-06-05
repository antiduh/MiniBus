﻿using System;

namespace MiniBus.Services
{
    public interface IServerBus
    {
        void SendMessage( Envelope msg );

        void RegisterHandler<T>( Action<IConsumeContext, T> handler ) where T : IMessage, new();

        //void UnregisterHandler<T>();
    }
}