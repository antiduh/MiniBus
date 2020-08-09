using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MiniBus.Services
{
    public class RabbitServerBus : IServerBus
    {
        private readonly IModel channel;

        private EventingBasicConsumer rabbitConsumer;

        private HashSet<string> knownExchanges;

        private Dictionary<string, IRegistrationContainer> handlers;

        private string privateQueueName;

        private MsgDefRegistry msgReg;

        public RabbitServerBus( IModel rabbit )
        {
            this.channel = rabbit;

            this.knownExchanges = new HashSet<string>();
            this.handlers = new Dictionary<string, IRegistrationContainer>();
            this.msgReg = new MsgDefRegistry();

            this.rabbitConsumer = new EventingBasicConsumer( rabbit );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;
            this.rabbitConsumer.Shutdown += RabbitConsumer_Shutdown;
        }

        public void RegisterHandler<T>( Action<T, IConsumeContext> handler, string queueName ) where T : IMessage, new()
        {
            MessageDef def = this.msgReg.Get<T>();
         
            this.handlers.Add( def.Name, new RegistrationContainer<T>( this, handler ) );

            ProvisionRabbit( def, queueName );
        }

        public void SendMessage( Envelope envelope )
        {
            SendMessage( envelope, null, null );
        }

        public void SendMessage( Envelope envelope, string exchange, string routingKey )
        {
            MessageDef msgDef = this.msgReg.Get( envelope.Message );

            var props = this.channel.CreateBasicProperties();

            // Don't assign values to properties if they're null. Rabbit pays attention to whether or
            // not a field was assigned. If it's been assigned, it'll try to serialize it, causing it
            // to serialize a null field.
            if( envelope.CorrId != null )
            {
                props.CorrelationId = envelope.CorrId;
            }

            if( envelope.SendRepliesTo != null )
            {
                props.ReplyTo = envelope.SendRepliesTo;
            }

            if( exchange == null )
            {
                exchange = msgDef.Exchange;
            }

            if( routingKey == null )
            {
                routingKey = msgDef.Name;
            }

            props.MessageId = msgDef.Name;

            ReadOnlyMemory<byte> body = Serializer.MakeBody( envelope.Message );
            this.channel.BasicPublish( exchange, routingKey, props, body );
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            string msgName = e.BasicProperties.MessageId;
            
            string payload = Serializer.ReadBody( e.Body.ToArray() );

            IRegistrationContainer handler;

            if( this.handlers.TryGetValue( msgName, out handler ) )
            {
                handler.Deliver( payload, e.BasicProperties.CorrelationId, e.BasicProperties.ReplyTo );
            }
            else
            {
                Console.WriteLine( $"Server Failure: No handler registered for message: {msgName}." );
            }
        }

        private void ProvisionRabbit( MessageDef msgDef, string queueName )
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

            // Listen on a queue that's specific to this service instance.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
        }

        private void RabbitConsumer_Shutdown( object sender, ShutdownEventArgs e )
        {
            Console.WriteLine( $"ServerBus: Consumer shutdown." );
        }

        private interface IRegistrationContainer
        {
            void Deliver( string payload, string senderCorrId, string senderReplyTo );
        }

        private class RegistrationContainer<T> : IRegistrationContainer where T : IMessage, new()
        {
            private readonly RabbitServerBus parent;
            private readonly Action<T, IConsumeContext> handler;

            public RegistrationContainer( RabbitServerBus parent, Action<T, IConsumeContext> handler )
            {
                this.parent = parent;
                this.handler = handler;
            }

            public void Deliver( string payload, string senderCorrId, string senderReplyTo )
            {
                T msg = new T();
                msg.Read( payload );

                var consumeContext = new RabbitConsumeContext( this.parent, senderCorrId, senderReplyTo );

                this.handler.Invoke( msg, consumeContext );
            }
        }

        private class RabbitConsumeContext : IConsumeContext
        {
            private readonly RabbitServerBus parent;
            private readonly string senderCorrId;
            private readonly string senderReplyTo;

            public RabbitConsumeContext( RabbitServerBus parent, string senderCorrId, string senderReplyTo )
            {
                this.parent = parent;
                this.senderCorrId = senderCorrId;
                this.senderReplyTo = senderReplyTo;
            }

            public void Reply( IMessage msg )
            {
                Reply( msg, null );
            }

            public void Reply( IMessage msg, ReplyOptions options )
            {
                Envelope replyEnv = new Envelope()
                {
                    Message = msg,
                    CorrId = this.senderCorrId,
                };

                if( options?.RedirectReplies == true )
                {
                    replyEnv.SendRepliesTo = this.parent.privateQueueName;
                }

                if( this.senderReplyTo == null )
                {
                    this.parent.SendMessage( replyEnv );
                }
                else
                {
                    this.parent.SendMessage( replyEnv, "", this.senderReplyTo );
                }
            }
        }
    }

}