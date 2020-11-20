using System;
using System.Net.Sockets;
using MiniBus;
using MiniBus.Gateway;
using PocketTlv;

namespace Gateway.Service
{
    public partial class GatewayService
    {
        private class ClientSession
        {
            private readonly TcpClient client;
            private readonly GatewayService parent;

            private MiniBusTlvClient tlvSocket;

            public ClientSession( TcpClient client, GatewayService parent )
            {
                this.client = client;
                this.parent = parent;
                this.ClientId = CorrId.Create();

                this.tlvSocket = new MiniBusTlvClient( client.GetStream() );
                this.tlvSocket.Register<GatewayRequestMsg>();
                this.tlvSocket.Register<GatewayHeartbeatRequest>();
                this.tlvSocket.Received += Socket_Received;
            }

            public string ClientId { get; private set; }

            public void Start()
            {
                this.tlvSocket.Start();
            }

            public void Stop()
            {
            }

            public void Write( ITlvContract message )
            {
                this.tlvSocket.SendMessage( message );
            }

            private void Socket_Received( ITlvContract tlvContract )
            {
                if( tlvContract.ContractId == GatewayTlvs.EchoRequest )
                {
                    Write( new GatewayHeartbeatResponse() );
                }
                else
                {
                    GatewayRequestMsg msg;
                    if( tlvContract.TryResolve( out msg ) == false )
                    {
                        return;
                    }

                    // We received a message from the client. Forward it to rabbit.

                    if( false )
                    {
                        Console.WriteLine( "Gateway received message: " );
                        Console.WriteLine( " - Client UID:  " + this.ClientId );
                        Console.WriteLine( " - RoutingKey:  " + msg.RoutingKey );
                        Console.WriteLine( " - MessageName: " + msg.MessageName );
                        Console.WriteLine();
                    }

                    parent.PublishRabbit( msg, this );
                }
            }
        }
    }
}