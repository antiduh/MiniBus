using System;

namespace MiniBus
{
    public interface IClientBus
    {
        void AddMessage<T>() where T : IMessage, new();

        IRequestContext StartRequest();

        void SendMessage( Envelope msg );

    }

    public interface IRequestContext
    {
        void SendMessage( IMessage msg );

        Envelope WaitResponse( TimeSpan timeout );
    }
}