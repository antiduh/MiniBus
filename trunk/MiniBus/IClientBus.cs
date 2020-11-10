using System;
using PocketTlv;

namespace MiniBus
{
    public interface IClientBus
    {
        void DeclareMessage<T>() where T : ITlvContract, new();

        IRequestContext StartRequest();

        void SendMessage( Envelope env, ITlvContract msg );
    }

    public interface IRequestContext : IDisposable
    {
        void SendRequest( ITlvContract msg );

        ITlvContract WaitResponse( TimeSpan timeout );

        T WaitResponse<T>( TimeSpan timeout ) where T : ITlvContract, new();
        
        void WithRetry( Action action );
    }
}