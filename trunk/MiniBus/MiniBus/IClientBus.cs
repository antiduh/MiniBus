using System;

namespace MiniBus
{
    public interface IClientBus
    {
        void KnownMessage<T>() where T : IMessage, new();

        IRequestContext StartRequest();

        void SendMessage( Envelope msg );
    }

    public interface IRequestContext
    {
        void SendMessage( IMessage msg );

        IMessage WaitResponse( TimeSpan timeout );

        T WaitResponse<T>( TimeSpan timeout ) where T : IMessage;
    }
}