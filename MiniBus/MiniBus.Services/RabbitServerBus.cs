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

        private MessageDefRegistry msgReg;

        public RabbitServerBus( IModel rabbit )
        {
            this.channel = rabbit;

            this.knownExchanges = new HashSet<string>();
            this.handlers = new Dictionary<string, IRegistrationContainer>();
            this.msgReg = new MessageDefRegistry();

            this.rabbitConsumer = new EventingBasicConsumer( rabbit );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;
        }

        public void RegisterHandler<T>( Action<IConsumeContext, T> handler ) where T : IMessage, new()
        {
            MessageDef def = this.msgReg.Get<T>();

            ProvisionRabbit( def );

            this.handlers.Add( def.Name, new RegistrationContainer<T>( handler ) );
        }

        public void SendMessage( Envelope envelope )
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

            ReadOnlyMemory<byte> body = Serializer.MakeBody( msgDef, envelope.Message );
            this.channel.BasicPublish( msgDef.Exchange, msgDef.RoutingKey, props, body );
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            // This sucks, but I don't care.
            string msgId, payload;
            Serializer.ReadBody( e.Body.ToArray(), out msgId, out payload );

            IRegistrationContainer handler;

            if( this.handlers.TryGetValue( msgId, out handler ) )
            {
                // TODO consumer context
                handler.Deliver( null, payload );
            }
            else
            {
                // TODO error reporting.
                Console.WriteLine( $"Failure: No handler registered for message: {msgId}." );
            }
        }

        private void ProvisionRabbit( MessageDef msgDef )
        {
            // Note that it's OK to tell rabbit to declare an exchange that already exists; that's
            // not what this method tries to prevent. The purpose here is to prevent us from wasting
            // time doing it again and again.
            if( knownExchanges.Contains( msgDef.Exchange ) == false )
            {
                this.channel.ExchangeDeclare( msgDef.Exchange, msgDef.ExchangeType.ToText(), true, false );
                this.knownExchanges.Add( msgDef.Exchange );
            }

            // Listen on the well-known queue.
            this.channel.QueueDeclare(
                queue: msgDef.Queue,
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            this.channel.QueueBind(
                msgDef.Queue,
                msgDef.Exchange,
                msgDef.RoutingKey
            );

            this.channel.BasicConsume( msgDef.Queue, true, this.rabbitConsumer );

            // Listen on a queue that's specific to this service instance.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
        }

        private interface IRegistrationContainer
        {
            void Deliver( IConsumeContext consumeContext, string payload );
        }

        private class RegistrationContainer<T> : IRegistrationContainer where T : IMessage, new()
        {
            private readonly Action<IConsumeContext, T> handler;

            public RegistrationContainer( Action<IConsumeContext, T> handler )
            {
                this.handler = handler;
            }

            public void Deliver( IConsumeContext consumeContext, string payload )
            {
                T msg = new T();
                msg.Read( payload );

                this.handler.Invoke( consumeContext, msg );
            }
        }

        public class RabbitConsumeContext : IConsumeContext
        {
            private readonly RabbitServerBus parent;
            private readonly Envelope replyContext;

            public RabbitConsumeContext( RabbitServerBus parent, Envelope replyContext )
            {
                this.parent = parent;
                this.replyContext = replyContext;
            }

            public void Reply( IMessage msg )
            {
                Envelope reply = new Envelope()
                {
                    Message = msg,
                    CorrId = this.replyContext.CorrId,
                };
            }
        }
    }
}