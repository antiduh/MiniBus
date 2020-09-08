using System;
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

        public GatewayClientBus( string hostname, int port )
        {
            this.hostname = hostname ?? throw new ArgumentNullException( nameof( hostname ) );
            this.port = port;

            this.msgDefs = new MsgDefRegistry();
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
            // TODO Wait until events are implemented
            throw new NotImplementedException();
        }

        public void EventHandler<T>( Action<T> handler ) where T : IMessage, new()
        {
            // TODO Wait until events are implemented
            throw new NotImplementedException();
        }

        public void SendMessage( Envelope env )
        {
            MessageDef def = this.msgDefs.Get( env.Message );

            var gatewayMsg = new GatewayInboundMsg()
            {
                CorrelationId = env.CorrId,
                Exchange = def.Exchange,
                RoutingKey = def.Name,
                MessageName = def.Name,
                Message = env.Message
            };

            this.tlvClient.SendMessage( gatewayMsg );
        }

        public IRequestContext StartRequest()
        {
            throw new NotImplementedException();
        }

        private void TlvClient_Received( ITlvContract msg )
        {
            Console.WriteLine( "Gateway client received: " + msg );
        }

        private class GatewayRequestContext : IRequestContext
        {
            private readonly GatewayClientBus parent;

            public GatewayRequestContext( GatewayClientBus parent )
            {
                this.parent = parent;
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public void SendRequest( IMessage msg )
            {
                throw new NotImplementedException();
            }

            public IMessage WaitResponse( TimeSpan timeout )
            {
                throw new NotImplementedException();
            }

            public T WaitResponse<T>( TimeSpan timeout ) where T : IMessage
            {
                throw new NotImplementedException();
            }
        }

    }
}
