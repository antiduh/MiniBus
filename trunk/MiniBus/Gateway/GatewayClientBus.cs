using System;
using System.Collections.Generic;
using System.Net.Sockets;
using MiniBus.Gateway;
using PocketTlv;

namespace MiniBus.Gateway
{
    public partial class GatewayClientBus : IClientBus
    {
        private readonly GatewayConnectionProvider connSource;

        private TcpClient socket;

        private MiniBusTlvClient tlvClient;

        private MsgDefRegistry msgDefs;

        private Dictionary<string, GatewayRequestContext> pendingConversations;

        public GatewayClientBus( GatewayConnectionProvider connSource )
        {
            if( connSource == null )
            {
                throw new ArgumentNullException( nameof( connSource ) );
            }

            this.connSource = connSource;

            this.msgDefs = new MsgDefRegistry();
            this.pendingConversations = new Dictionary<string, GatewayRequestContext>();
        }

        public void Connect()
        {
            Hostname gatewayServer = this.connSource.GetConnection();

            this.socket = new TcpClient();
            this.socket.Connect( gatewayServer.Host, gatewayServer.Port );

            this.tlvClient = new MiniBusTlvClient( this.socket.GetStream() );
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

            lock( this.pendingConversations )
            {
                this.pendingConversations.Add( context.ConversationId, context );
            }

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

            SendMessageInternal( env, msg, def.Exchange, def.Name );
        }

        private void SendMessageInternal( ClientEnvelope env, ITlvContract msg, string exchange, string routingKey )
        {
            MessageDef def = this.msgDefs.Get( msg );

            var gatewayMsg = new GatewayRequestMsg()
            {
                CorrelationId = env.CorrelationId,
                Exchange = exchange,
                RoutingKey = routingKey,
                MessageName = def.Name,
                Message = msg
            };

            this.tlvClient.SendMessage( gatewayMsg );
        }

        private void TlvClient_Received( ITlvContract msg )
        {
            if( msg.TryResolve( out GatewayResponseMsg response ) )
            {
                DispatchReceived( response );
            }
        }

        private void DispatchReceived( GatewayResponseMsg response )
        {
            var env = new ClientEnvelope()
            {
                CorrelationId = response.CorrelationId,
                SendRepliesTo = response.SendRepliesTo,
            };

            bool foundConvo = false;
            GatewayRequestContext context;

            lock( this.pendingConversations )
            {
                foundConvo = this.pendingConversations.TryGetValue( response.CorrelationId, out context );
            }

            if( foundConvo )
            {
                context.DispatchMessage( env, response.Message );
            }
            else
            {
                Console.WriteLine( $"Client Failure: No handler registered for message {response.MessageName}." );
            }
        }
    }
}