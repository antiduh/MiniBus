using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using PocketTlv;

namespace MiniBus.Gateway
{
    public partial class GatewayClientBus
    {
        private class RequestContext : IRequestContext
        {
            private readonly GatewayClientBus parent;

            private BlockingCollection<Dispatch> inQueue;

            private string redirectQueue;

            public RequestContext( GatewayClientBus parent, string corrId = null )
            {
                this.parent = parent;

                if( corrId == null )
                {
                    this.ConversationId = CorrId.Create();
                }
                else
                {
                    this.ConversationId = corrId;
                }

                this.inQueue = new BlockingCollection<Dispatch>();
            }

            public string ConversationId { get; private set; }

            public void Dispose()
            {
            }

            public void SendRequest( ITlvContract msg )
            {
                var env = new ClientEnvelope()
                {
                    CorrelationId = this.ConversationId
                };

                if( this.redirectQueue == null )
                {
                    this.parent.SendMessageInternal( env, msg );
                }
                else
                {
                    this.parent.SendMessageInternal( env, msg, "", this.redirectQueue );  
                }
            }

            public ITlvContract WaitResponse( TimeSpan timeout )
            {
                return WaitResponseInternal( timeout ).Message;
            }

            public T WaitResponse<T>( TimeSpan timeout ) where T : ITlvContract, new()
            {
                Dispatch dispatch = WaitResponseInternal( timeout );

                return dispatch.Message.Resolve<T>();
            }

            private Dispatch WaitResponseInternal( TimeSpan timeout )
            {
                if( this.inQueue.TryTake( out Dispatch dispatch, timeout ) == false )
                {
                    throw new TimeoutException();
                }

                if( dispatch.Envelope.SendRepliesTo != null )
                {
                    this.redirectQueue = dispatch.Envelope.SendRepliesTo;
                }

                return dispatch;
            }

            public void WithRetry( Action action )
            {
                ExceptionDispatchInfo failure = null;

                for( int i = 0; i < 50000; i++ )
                {
                    try
                    {
                        action();
                        failure = null;

                        break;
                    }
                    catch( DeliveryException e )
                    {
                        failure = ExceptionDispatchInfo.Capture( e );
                        Thread.Sleep( 1000 );
                    }
                }

                if( failure != null )
                {
                    failure.Throw();
                }
            }

            internal void DispatchMessage( ClientEnvelope env, ITlvContract msg )
            {
                this.inQueue.Add( new Dispatch( env, msg ) );
            }

            private struct Dispatch
            {
                public Dispatch( ClientEnvelope env, ITlvContract message )
                {
                    Envelope = env;
                    Message = message;
                }

                public ClientEnvelope Envelope { get; private set; }

                public ITlvContract Message { get; private set; }
            }
        }
    }
}