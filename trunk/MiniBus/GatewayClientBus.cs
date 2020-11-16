using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using MiniBus.Gateway;
using PocketTlv;

namespace MiniBus
{
    public class GatewayClientBus : IClientBus
    {
        private readonly string hostname;
        private readonly int port;

        private TcpClient socket;

        private TlvClient tlvClient;

        private MsgDefRegistry msgDefs;

        private Dictionary<string, GatewayRequestContext> pendingConversations;

        public GatewayClientBus( string hostname, int port )
        {
            this.hostname = hostname ?? throw new ArgumentNullException( nameof( hostname ) );
            this.port = port;

            this.msgDefs = new MsgDefRegistry();
            this.pendingConversations = new Dictionary<string, GatewayRequestContext>();
        }

        public void Connect()
        {
            this.socket = new TcpClient();
            this.socket.Connect( this.hostname, this.port );

            this.tlvClient = new TlvClient( this.socket.GetStream() );
            this.tlvClient.Register<GatewayResponseMsg>();
            this.tlvClient.Register<GatewayHeartbeatResponse>();
            this.tlvClient.Received += TlvClient_Received;
            this.tlvClient.Start();
        }

        public void DeclareMessage<T>() where T : ITlvContract, new()
        {
            this.msgDefs.Add<T>();
            this.tlvClient.Register<T>();
        }

        public IRequestContext StartRequest()
        {
            return StartRequest( null );
        }

        public IRequestContext StartRequest( string corrId )
        {
            var context = new GatewayRequestContext( this, corrId );

            this.pendingConversations.Add( context.ConversationId, context );

            return context;
        }

        public void SendMessage( string corrId, ITlvContract msg )
        {
            var env = new ClientEnvelope()
            {
                CorrelationId = corrId
            };

            SendMessageInternal( env, msg );
        }

        private void SendMessageInternal( ClientEnvelope env, ITlvContract msg )
        {
            MessageDef def = this.msgDefs.Get( msg );

            var gatewayMsg = new GatewayRequestMsg()
            {
                CorrelationId = env.CorrelationId,
                Exchange = def.Exchange,
                RoutingKey = def.Name,
                MessageName = def.Name,
                Message = msg
            };

            this.tlvClient.SendMessage( gatewayMsg );
        }

        private void TlvClient_Received( ITlvContract msg )
        {
            var gatewayMsg = msg.Resolve<GatewayResponseMsg>();

            var env = new ClientEnvelope()
            {
                CorrelationId = gatewayMsg.CorrelationId,
                SendRepliesTo = gatewayMsg.SendRepliesTo,
            };

            if( this.pendingConversations.TryGetValue( gatewayMsg.CorrelationId, out GatewayRequestContext context ) )
            {
                context.DispatchMessage( env, gatewayMsg.Message );
            }
            else
            {
                Console.WriteLine( $"Client Failure: No handler registered for message {gatewayMsg.MessageName}." );
            }
        }

        private class GatewayRequestContext : IRequestContext
        {
            private readonly GatewayClientBus parent;

            private BlockingCollection<Dispatch> inQueue;

            private string redirectQueue;

            public GatewayRequestContext( GatewayClientBus parent, string corrId = null )
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

                this.parent.SendMessageInternal( env, msg );
            }

            public ITlvContract WaitResponse( TimeSpan timeout )
            {
                throw new NotImplementedException();
            }

            public T WaitResponse<T>( TimeSpan timeout ) where T : ITlvContract, new()
            {
                if( this.inQueue.TryTake( out Dispatch dispatch, timeout ) == false )
                {
                    throw new TimeoutException();
                }

                if( dispatch.Envelope.SendRepliesTo != null )
                {
                    this.redirectQueue = dispatch.Envelope.SendRepliesTo;
                }

                return dispatch.Message.Resolve<T>();
            }

            public void WithRetry( Action action )
            {
                ExceptionDispatchInfo failure = null;

                for( int i = 0; i < 5; i++ )
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
                public Dispatch( ClientEnvelope envelope, ITlvContract message )
                {
                    Envelope = envelope;
                    Message = message;
                }

                public ClientEnvelope Envelope { get; private set; }

                public ITlvContract Message { get; private set; }
            }
        }
    }
}