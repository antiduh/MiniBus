using System;

namespace MiniBus
{
    public interface IClientBus
    {
        IRequestContext StartRequest();

        //void ListenEvent<T>() where T : IMessage;

        void SendMessage( Envelope msg );
    }

    public interface IRequestContext
    {
        void SendMessage( IMessage msg );

        Envelope WaitResponse( TimeSpan timeout );
    }
}