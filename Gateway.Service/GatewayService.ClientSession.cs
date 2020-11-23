﻿using System;
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

                this.tlvReader = new TlvStreamReader( this.contractReg );
                this.tlvReader.Connect( client.GetStream() );

                this.tlvWriter = new TlvStreamWriter();
                this.tlvWriter.Connect( client.GetStream() );
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
                catch( Exception e )
                {
                    // TODO
                    Console.WriteLine( "GatewayService: Client session crashed: " + e.GetType() );
                }

                Console.WriteLine( "GatewayService: Client stopping." );

                this.parent.DisconnectClient( this );

                this.tlvReader?.Disconnect();
                this.tlvReader = null;

                this.tlvWriter?.Disconnect();
                this.tlvWriter = null;

                this.client?.Dispose();
                this.client = null;
            }

            private void ReadLoop()
            {
                while( true )
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