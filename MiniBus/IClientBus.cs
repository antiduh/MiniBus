using System;
using PocketTlv;

namespace MiniBus
{
    public interface IClientBus
    {
        void DeclareMessage<T>() where T : ITlvContract, new();

        // TODO antiduh
        void SendMessage( string corrId, ITlvContract msg );

        IRequestContext StartRequest();

        // TODO antiduh
        IRequestContext StartRequest( string corrId );
    }

    public interface IRequestContext : IDisposable
    {
        void SendRequest( ITlvContract msg );

        ITlvContract WaitResponse( TimeSpan timeout );

        T WaitResponse<T>( TimeSpan timeout ) where T : ITlvContract, new();

        void WithRetry( Action action );
    }

    public class ClientEnvelope
    {
        public ClientEnvelope()
        {
        }

        public ClientEnvelope( string correlationId, string sendRepliesTo )
        {
            this.CorrelationId = correlationId;
            this.SendRepliesTo = sendRepliesTo;
        }

        public string CorrelationId { get; set; }

        public string SendRepliesTo { get; set; }
    }
}