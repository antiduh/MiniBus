using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using MiniBus.ClientApi.Gateway;
using MiniBus.Gateway;
using PocketTlv;

namespace MiniBus.ClientApi
{
    public class GatewayClientBus : IClientBus
    {
        private readonly string hostname;
        private readonly int port;

        private TcpClient socket;

        private TlvClient tlvClient;

        private MsgDefRegistry msgDefs;

        private Dictionary<Guid, GatewayRequestContext> pendingConversations;


        public GatewayClientBus( string hostname, int port )
        {
            this.hostname = hostname ?? throw new ArgumentNullException( nameof( hostname ) );
            this.port = port;

            this.msgDefs = new MsgDefRegistry();
            this.pendingConversations = new Dictionary<Guid, GatewayRequestContext>();
        }

        public void Connect()
        {
            this.socket = new TcpClient(); 
            this.socket.Connect( this.hostname, this.port );

            this.tlvClient = new TlvClient( this.socket.GetStream() );
            this.tlvClient.Register<GatewayOutboundMsg>();
            this.tlvClient.Register<GatewayHeartbeatResponse>();
            this.tlvClient.Received += TlvClient_Received;
            this.tlvClient.Start();
        }

        public void DeclareMessage<T>() where T : IMessage, new()
        {
            this.msgDefs.Add<T>();
            this.tlvClient.Register<T>();
        }

        public void EventHandler<T>( Action<T> handler ) where T : IMessage, new()
        {
            // TODO Wait until events are implemented
            //throw new NotImplementedException();
        }

        public void SendMessage( Envelope env, IMessage msg )
        {
            MessageDef def = this.msgDefs.Get( msg );

            var gatewayMsg = new GatewayInboundMsg()
            {
                CorrelationId = env.CorrelationId,
                Exchange = def.Exchange,
                RoutingKey = def.Name,
                MessageName = def.Name,
                Message = msg
            };

            this.tlvClient.SendMessage( gatewayMsg );
        }

        public IRequestContext StartRequest()
        {
            var context = new GatewayRequestContext( this );

            this.pendingConversations.Add( context.ConversationId, context );

            return context;
        }

        private void SendContextMsg( IMessage msg, GatewayRequestContext context )
        {
            var env = new Envelope()
            {
                CorrelationId = context.ConversationId.ToString( "B" ),
            };

            SendMessage( env, msg );
        }

        private void TlvClient_Received( ITlvContract msg )
        {
            var gatewayMsg = msg.Resolve<GatewayOutboundMsg>();

            Guid contextId = Guid.Parse( gatewayMsg.CorrelationId );

            Envelope env = new Envelope()
            {
                CorrelationId = gatewayMsg.CorrelationId,
                SendRepliesTo = gatewayMsg.SendRepliesTo,
            };

            if( this.pendingConversations.TryGetValue( contextId, out GatewayRequestContext context ) )
            {
                context.DispatchMessage( env, gatewayMsg.Message );
            }
            else
            {
                Console.WriteLine( $"Client Failure: No handler registered for message {gatewayMsg.MessageName}." );
            }

            Console.WriteLine( "Gateway client received: " + msg );
        }

        private class GatewayRequestContext : IRequestContext
        {
            private readonly GatewayClientBus parent;

            private BlockingCollection<Dispatch> inQueue;

            public GatewayRequestContext( GatewayClientBus parent )
            {
                this.parent = parent;

                this.ConversationId = Guid.NewGuid();
                this.inQueue = new BlockingCollection<Dispatch>();
            }

            public Guid ConversationId { get; private set; }

            public void Dispose()
            {
                
            }

            public void SendRequest( IMessage msg )
            {
                this.parent.SendContextMsg( msg, this );
            }

            public IMessage WaitResponse( TimeSpan timeout )
            {
                throw new NotImplementedException();
            }

            public T WaitResponse<T>( TimeSpan timeout ) where T : IMessage, new()
            {
                if( this.inQueue.TryTake( out Dispatch dispatch, timeout ) == false )
                {
                    throw new TimeoutException();
                }

                return dispatch.Message.Resolve<T>();
            }

            internal void DispatchMessage( Envelope env, ITlvContract msg )
            {
                this.inQueue.Add( new Dispatch( env, msg ) );
            }

            private struct Dispatch
            {
                public Dispatch( Envelope envelope, ITlvContract message )
                {
                    Envelope = envelope;
                    Message = message;
                }

                public Envelope Envelope { get; private set; }

                public ITlvContract Message { get; private set; }
            }
        }

    }
}
