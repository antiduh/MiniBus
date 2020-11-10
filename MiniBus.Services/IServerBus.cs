using System;
using PocketTlv;

namespace MiniBus.Services
{
    public interface IServerBus
    {
        void SendMessage( Envelope env, ITlvContract msg );

        void RegisterHandler<T>( Action<T, IConsumeContext> handler, string queueName ) where T : ITlvContract, new();

        //void UnregisterHandler<T>();
    }
}