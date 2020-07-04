using System;

namespace MiniBus
{
    public interface IClientBus
    {
        void DeclareMessage<T>() where T : IMessage, new();

        IRequestContext StartRequest();

        void SendMessage( Envelope msg );

        void EventHandler<T>( Action<T> handler ) where T : IMessage, new();
    }

    public interface IRequestContext
    {
        void SendMessage( IMessage msg );

        IMessage WaitResponse( TimeSpan timeout );

        T WaitResponse<T>( TimeSpan timeout ) where T : IMessage;
    }
}