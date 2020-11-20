using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MiniBus;
using MiniBus.Gateway;
using MiniBus.Services;
using PocketTlv;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Gateway.Service
{
    public class GatewayService
    {
        private readonly int port;

        private TcpListener listenSocket;
        private IModel channel;
        private ModelWithRecovery remodel;
        private EventingBasicConsumer rabbitConsumer;
        private Dictionary<string, GatewayClient> clientMap;

        private Thread listenThread;

        private TlvBufferWriter tlvWriter;
        private TlvBufferReader tlvReader;

        private string privateQueueName;

        public GatewayService( int port )
        {
            this.port = port;

            this.tlvReader = new TlvBufferReader();
            this.tlvWriter = new TlvBufferWriter();

            this.clientMap = new Dictionary<string, GatewayClient>();
        }

        public void Connect( ModelWithRecovery remodel )
        {
            this.remodel = remodel;
            this.channel = remodel.Model;

            this.rabbitConsumer = new EventingBasicConsumer( this.channel );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;
            
            this.remodel.RecoverySucceeded += Remodel_RecoverySucceeded;

            ListenOnPrivateQueue();

            this.listenThread = new Thread( ListenThreadEntry );
            this.listenThread.Start();
        }

        private void ListenOnPrivateQueue()
        {
            // Listen on a queue that's specific to this service instance.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
        }

        private void Remodel_RecoverySucceeded( object sender, EventArgs e )
        {
            Console.Write( "GatewayService: Reconnecting..." );
            ListenOnPrivateQueue();
            Console.Write( "GatewayService: Reconnecting... done" );
        }

        private void PublishRabbit( GatewayRequestMsg msg, GatewayClient client )
        {
            // To forward to rabbit, we need to know the message parameters.
            // - What is the message name (routing key)?
            // - What is the exchange it should be sent to?
            // - Does the message have a correlation ID?
            // - We have to tag the message on rabbit with our queue so that the bus replies to us.
            // - We have to tag the message on rabbit with the ID of our client so we know which socket to write it to when we receive it.

            IBasicProperties props = channel.CreateBasicProperties();

            props.MessageId = msg.MessageName;

            if( msg.CorrelationId.Length > 0 )
            {
                props.CorrelationId = msg.CorrelationId;
            }

            props.ReplyTo = this.privateQueueName;
            props.Headers = new Dictionary<string, object>();
            props.Headers["clientId"] = client.ClientId;

            lock( this.tlvWriter )
            {
                this.tlvWriter.Write( msg.Message );
                this.channel.BasicPublish( msg.Exchange, msg.RoutingKey, props, tlvWriter.GetBuffer() );
                this.tlvWriter.Reset();
            }
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            string clientId = Encoding.UTF8.GetString( (byte[])e.BasicProperties.Headers["clientId"] );

            GatewayClient client = this.clientMap[clientId];
            ITlvContract message;

            lock( this.tlvReader )
            {
                this.tlvReader.LoadBuffer( e.Body.ToArray() );
                message = this.tlvReader.ReadContract();
                this.tlvReader.UnloadBuffer();
            }

            var outboundMsg = new GatewayResponseMsg()
            {
                CorrelationId = e.BasicProperties.CorrelationId,
                MessageName = e.BasicProperties.MessageId,
                Message = message,
                SendRepliesTo = e.BasicProperties.ReplyTo
            };

            client.Write( outboundMsg );
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

                    var client = new GatewayClient( clientSocket, this );
                    this.clientMap.Add( client.ClientId, client );

                    client.Start();
                }
                catch( SocketException e )
                {
                    break;
                }
            }
        }

        private class GatewayClient
        {
            private readonly TcpClient client;
            private readonly GatewayService parent;

            private MiniBusTlvClient tlvSocket;

            public GatewayClient( TcpClient client, GatewayService parent )
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