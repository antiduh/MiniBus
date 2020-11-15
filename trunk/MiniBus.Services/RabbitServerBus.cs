using System;
using System.Collections.Generic;
using PocketTlv;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MiniBus.Services
{
    public class RabbitServerBus : IServerBus
    {
        private readonly IModel channel;
        private readonly ModelWithRecovery remodel;

        private EventingBasicConsumer rabbitConsumer;

        private HashSet<string> knownExchanges;

        private Dictionary<string, IHandlerRegistration> handlers;

        private string privateQueueName;

        private MsgDefRegistry msgReg;

        private TlvBufferReader tlvReader;

        private TlvBufferWriter tlvWriter;

        private ConcurrentObjectPool<RabbitConsumeContext> consumeContextPool;

        public RabbitServerBus( ModelWithRecovery remodel )
        {
            this.remodel = remodel;
            this.channel = remodel.Model;

            this.knownExchanges = new HashSet<string>();
            this.handlers = new Dictionary<string, IHandlerRegistration>();
            this.msgReg = new MsgDefRegistry();

            this.tlvReader = new TlvBufferReader();
            this.tlvWriter = new TlvBufferWriter();

            this.consumeContextPool = new ConcurrentObjectPool<RabbitConsumeContext>( () => new RabbitConsumeContext( this ) );

            this.rabbitConsumer = new EventingBasicConsumer( this.channel );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;
            this.rabbitConsumer.Shutdown += RabbitConsumer_Shutdown;

            this.remodel.RecoverySucceeded += Remodel_RecoverySucceeded;

            ListenOnPrivateQueue();
        }

        public void RegisterHandler<T>( Action<T, IConsumeContext> handler, string queueName ) where T : ITlvContract, new()
        {
            MessageDef def = this.msgReg.Get<T>();

            this.handlers.Add( def.Name, new HandlerRegistration<T>( this, handler ) );

            this.tlvReader.RegisterContract<T>();

            ProvisionRabbitForMessageDef( def, queueName );
        }

        public void SendMessage( ServerEnvelope envelope, ITlvContract msg )
        {
            SendMessage( envelope, msg, null, null );
        }

        public void SendMessage( ServerEnvelope envelope, ITlvContract msg, string exchange, string routingKey )
        {
            MessageDef msgDef = this.msgReg.Get( msg );

            var props = this.channel.CreateBasicProperties();

            envelope.ToRabbit( props );

            if( exchange == null )
            {
                exchange = msgDef.Exchange;
            }

            if( routingKey == null )
            {
                routingKey = msgDef.Name;
            }

            props.MessageId = msgDef.Name;

            lock( this.tlvWriter )
            {
                this.tlvWriter.Write( msg );
                this.channel.BasicPublish( exchange, routingKey, props, this.tlvWriter.GetBuffer() );
                this.tlvWriter.Reset();
            }
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            string msgName = e.BasicProperties.MessageId;

            var env = ServerEnvelope.FromRabbit( e.BasicProperties );

            if( this.handlers.TryGetValue( msgName, out IHandlerRegistration handler ) )
            {
                ITlvContract msg;

                lock( this.tlvReader )
                {
                    this.tlvReader.LoadBuffer( e.Body.ToArray() );
                    msg = this.tlvReader.ReadContract();
                    this.tlvReader.UnloadBuffer();
                }

                handler.Deliver( msg, env );
            }
            else
            {
                Console.WriteLine( $"Server Failure: No handler registered for message: {msgName}." );
            }
        }

        private void ProvisionRabbitForMessageDef( MessageDef msgDef, string queueName )
        {
            // Note that it's OK to tell rabbit to declare an exchange that already exists; that's
            // not what this method tries to prevent. The purpose here is to prevent us from wasting
            // time doing it again and again.
            if( knownExchanges.Contains( msgDef.Exchange ) == false )
            {
                this.channel.ExchangeDeclare( msgDef.Exchange, "topic", true, false );
                this.knownExchanges.Add( msgDef.Exchange );
            }

            // Declare and listen on the well-known queue.
            this.channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            this.channel.QueueBind(
                queueName,
                msgDef.Exchange,
                msgDef.Name
            );

            this.channel.BasicConsume( queueName, true, this.rabbitConsumer );
        }

        private void ListenOnPrivateQueue()
        {
            // Listen on a queue that's specific to this service instance.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
        }

        private void Remodel_RecoverySucceeded( object sender, EventArgs e )
        {
            Console.Write( "RabbitServerBus: Reconnecting..." );
            ListenOnPrivateQueue();
            Console.Write( "RabbitServerBus: Reconnecting... done." );
        }

        private void RabbitConsumer_Shutdown( object sender, ShutdownEventArgs e )
        {
            Console.WriteLine( $"ServerBus: Consumer shutdown." );
        }

        private interface IHandlerRegistration
        {
            void Deliver( ITlvContract msg, ServerEnvelope env );
        }

        private class HandlerRegistration<T> : IHandlerRegistration where T : ITlvContract, new()
        {
            private readonly RabbitServerBus parent;
            private readonly Action<T, IConsumeContext> handler;

            public HandlerRegistration( RabbitServerBus parent, Action<T, IConsumeContext> handler )
            {
                this.parent = parent;
                this.handler = handler;
            }

            public void Deliver( ITlvContract msg, ServerEnvelope env )
            {
                RabbitConsumeContext consumeContext;

                consumeContext = this.parent.consumeContextPool.Get();

                consumeContext.Load( env );
                this.handler.Invoke( (T)msg, consumeContext );
                consumeContext.Unload();

                this.parent.consumeContextPool.Return( consumeContext );
            }
        }

        private class RabbitConsumeContext : IConsumeContext
        {
            private readonly RabbitServerBus parent;
            private string senderCorrId;
            private string senderReplyTo;
            private string clientId;

            public RabbitConsumeContext( RabbitServerBus parent )
            {
                this.parent = parent;
            }

            public void Load( ServerEnvelope env )
            {
                this.senderCorrId = env.CorrelationId;
                this.senderReplyTo = env.SendRepliesTo;
                this.clientId = env.ClientId;
            }

            public void Unload()
            {
                this.senderCorrId = null;
                this.senderReplyTo = null;
                this.clientId = null;
            }

            public void Reply( ITlvContract msg )
            {
                Reply( msg, null );
            }

            public void Reply( ITlvContract msg, ReplyOptions options )
            {
                var replyEnv = new ServerEnvelope()
                {
                    CorrelationId = this.senderCorrId,
                    ClientId = this.clientId,
                };

                if( options?.RedirectReplies == true )
                {
                    replyEnv.SendRepliesTo = this.parent.privateQueueName;
                }

                if( this.senderReplyTo == null )
                {
                    this.parent.SendMessage( replyEnv, msg );
                }
                else
                {
                    this.parent.SendMessage( replyEnv, msg, "", this.senderReplyTo );
                }
            }
        }
    }
}