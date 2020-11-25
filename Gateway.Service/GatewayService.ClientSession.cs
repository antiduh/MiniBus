using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using MiniBus;
using MiniBus.Gateway;
using PocketTlv;

namespace Gateway.Service
{
    public partial class GatewayService
    {
        private class ClientSession
        {
            private readonly GatewayService parent;

            private TcpClient client;

            private Thread receiveThread;

            private TlvStreamReader tlvReader;
            private TlvStreamWriter tlvWriter;
            private ContractRegistry contractReg;

            private object outboundLock;

            public ClientSession( TcpClient client, GatewayService parent )
            {
                this.client = client;
                this.parent = parent;

                this.ClientId = CorrId.Create();

                this.outboundLock = new object();

                this.contractReg = new ContractRegistry();
                this.contractReg.Register<GatewayHeartbeatRequest>();
                this.contractReg.Register<GatewayRequestMsg>();

                var tcpStream = client.GetStream();
                this.tlvReader = new TlvStreamReader( tcpStream, this.contractReg );
                this.tlvWriter = new TlvStreamWriter( tcpStream );
            }

            public string ClientId { get; private set; }

            public void Start()
            {
                this.receiveThread = new Thread( ReceiveThreadEntry );
                this.receiveThread.Start();
            }

            public void Stop()
            {
            }

            public void Write( ITlvContract message )
            {
                lock( this.outboundLock )
                {
                    this.tlvWriter.Write( message );
                }
            }

            private void ReceiveThreadEntry()
            {
                try
                {
                    ReadLoop();
                }
                catch( IOException ) { }
                catch( Exception e )
                {
                    Console.WriteLine( "GatewayService: Client session crashed: " + e.GetType() );
                }

                Console.WriteLine( "GatewayService: Client disconnected." );

                this.parent.DisconnectClient( this );

                this.tlvReader = null;
                this.tlvWriter = null;

                this.client?.Dispose();
                this.client = null;
            }

            private void ReadLoop()
            {
                ITlvContract contract;

                while( true )
                {
                    contract = this.tlvReader.ReadContract();

                    if( contract == null )
                    {
                        break;
                    }

                    ProcessReceived( contract );
                }
            }

            private void ProcessReceived( ITlvContract tlvContract )
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
                    parent.PublishRabbit( msg, this );
                }
            }
        }
    }
}