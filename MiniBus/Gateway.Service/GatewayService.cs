using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MiniBus;
using MiniBus.ClientApi.Gateway;
using MiniBus.Services;
using PocketTLV;
using RabbitMQ.Client;

namespace Gateway.Service
{
    public class GatewayService
    {
        private readonly int port;
        private IServerBus bus;

        private TcpListener listenSocket;
        private IModel channel;

        private Dictionary<Guid, GatewaySession> clientMap;

        private Thread listenThread;

        public GatewayService( int port )
        {
            this.port = port;

            this.clientMap = new Dictionary<Guid, GatewaySession>();

        }

        public void Start()
        {
            this.listenThread = new Thread( ListenThreadEntry );
            this.listenThread.Start();
        }

        public void Connect( IModel channel )
        {
            this.channel = channel;
           
            
        }

        public void SocketToRabbit()
        {
            // To forward to rabbit, we need to know the message parameters.
            // - What is the message name (routing key)? 
            // - What is the exchange it should be sent to?
            // - Does the message have a correlation ID?
            // - We have to tag the message on rabbit with our queue so that the bus replies to us.
            // - We have to tag the message on rabbit with the ID of our client so we know which socket to write it to when we receive it.
        }

        public void RabbitToSocket()
        {
        }

        private void ListenThreadEntry()
        {
            this.listenSocket = new TcpListener( IPAddress.Any, port );
            this.listenSocket.Start();

            while( true )
            {
                try
                {
                    TcpClient clientSocket = this.listenSocket.AcceptTcpClient();

                    var client = new GatewaySession( clientSocket, this );
                    this.clientMap.Add( client.Guid, client );

                    client.Start();
                }
                catch( SocketException e )
                {
                    break;
                }
            }

        }


        private class GatewaySession
        {
            private readonly TcpClient client;
            private readonly GatewayService parent;

            private TlvSocket tlvSocket;

            public GatewaySession( TcpClient client, GatewayService parent )
            {
                this.client = client;
                this.parent = parent;
                this.Guid = Guid.NewGuid();

                this.tlvSocket = new TlvSocket( client.GetStream() );
                this.tlvSocket.Register<GatewayMessage>();
                this.tlvSocket.Received += Socket_Received;
            }

            public Guid Guid { get; private set; }

            public void Start()
            {
                this.tlvSocket.Start();
            }

            public void Stop()
            {
            }

            private void Socket_Received( ITlvContract tlvContract )
            {
                GatewayMessage msg;
                if( tlvContract.TryResolve( out msg ) == false )
                {
                    return;
                }

                // We received a message from the client. Forward it to rabbit.

                Console.WriteLine( msg.RoutingKey );
            }

        }
    }

}
