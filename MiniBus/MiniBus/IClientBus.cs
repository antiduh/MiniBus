using System;

namespace MiniBus
{
    public interface IClientBus
    {
        void DeclareMessage<T>() where T : IMessage, new();

        IRequestContext StartRequest();

        void SendMessage( Envelope env, IMessage msg );

        void EventHandler<T>( Action<T> handler ) where T : IMessage, new();
    }

    public interface IRequestContext : IDisposable
    {
        void SendRequest( IMessage msg );

        IMessage WaitResponse( TimeSpan timeout );

        T WaitResponse<T>( TimeSpan timeout ) where T : IMessage, new();
    }
}