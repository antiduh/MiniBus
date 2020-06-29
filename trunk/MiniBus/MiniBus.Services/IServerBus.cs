using System;

namespace MiniBus.Services
{
    public interface IServerBus
    {
        void SendMessage( Envelope msg );

        void RegisterHandler<T>( Action<T, IConsumeContext> handler, string queueName ) where T : IMessage, new();

        //void UnregisterHandler<T>();
    }
}