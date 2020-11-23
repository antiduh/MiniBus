using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MiniBus.Gateway;
using MiniBus.Services;
using PocketTlv;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Gateway.Service
{
    public partial class GatewayService
    {
        private readonly int port;

        private TcpListener listenSocket;
        private IModel channel;
        private ModelWithRecovery remodel;
        private EventingBasicConsumer rabbitConsumer;
        private Dictionary<string, ClientSession> clientMap;

        private Thread listenThread;

        private TlvBufferWriter tlvWriter;
        private TlvBufferReader tlvReader;

        private string privateQueueName;

        public GatewayService( int port )
        {
            this.port = port;

            this.tlvReader = new TlvBufferReader();
            this.tlvWriter = new TlvBufferWriter();

            this.clientMap = new Dictionary<string, ClientSession>();
        }

        public void Connect( ModelWithRecovery remodel )
        {
            this.remodel = remodel;
            this.channel = remodel.Model;

            this.rabbitConsumer = new EventingBasicConsumer( this.channel );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;
            this.rabbitConsumer.Shutdown += RabbitConsumer_Shutdown;
            
            this.remodel.RecoverySucceeded += Remodel_RecoverySucceeded;

            ListenOnPrivateQueue();

            this.listenThread = new Thread( ListenThreadEntry );
            this.listenThread.Start();
        }

        private void RabbitConsumer_Shutdown( object sender, ShutdownEventArgs e )
        {
            Console.WriteLine( "GatewayService: Lost connection to rabbit." );
        }

        private void ListenOnPrivateQueue()
        {
            // Listen on a queue that's specific to this service instance.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
        }

        private void Remodel_RecoverySucceeded( object sender, EventArgs e )
        {
            Console.WriteLine( "GatewayService: Reconnecting rabbit ..." );
            ListenOnPrivateQueue();
            Console.WriteLine( "GatewayService: Reconnecting rabbit ... done" );
        }

        private void PublishRabbit( GatewayRequestMsg msg, ClientSession client )
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
                try
                {
                    this.tlvWriter.Write( msg.Message );
                    this.channel.BasicPublish( msg.Exchange, msg.RoutingKey, props, tlvWriter.GetBuffer() );
                }
                catch( RabbitMQClientException )
                {
                    Console.WriteLine( "GatewayService: Dropping message from client, no connection to rabbit." );
                }
                finally
                {
                    this.tlvWriter.Reset();
                }
            }
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            string clientId = Encoding.UTF8.GetString( (byte[])e.BasicProperties.Headers["clientId"] );

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

            ClientSession client;

            lock( this.clientMap )
            {
                 client = this.clientMap[clientId];
            }

            // TODO there's a threading hole here; what happens if we get a message for a client
            // riiight as they're in the process of crashing?
            client.Write( outboundMsg );
        }

        private void DisconnectClient( ClientSession session )
        {
            lock( this.clientMap )
            {
                this.clientMap.Remove( session.ClientId );
            }
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

                    Console.WriteLine( "GatewayClient: Client connected" );
                    var client = new ClientSession( clientSocket, this );

                    lock( this.clientMap )
                    {
                        this.clientMap.Add( client.ClientId, client );
                    }

                    client.Start();
                }
                catch( SocketException e )
                {
                    break;
                }
            }
        }
    }
}