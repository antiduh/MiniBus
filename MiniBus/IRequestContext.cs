using System;
using PocketTlv;

namespace MiniBus
{
    public interface IRequestContext : IDisposable
    {
        void SendRequest( ITlvContract msg );

        ITlvContract WaitResponse( TimeSpan timeout );

        T WaitResponse<T>( TimeSpan timeout ) where T : ITlvContract, new();

        void WithRetry( Action action );
    }
}