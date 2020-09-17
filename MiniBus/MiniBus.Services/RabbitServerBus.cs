using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MiniBus.Services
{
    public class RabbitServerBus : IServerBus
    {
        private readonly IModel channel;

        private EventingBasicConsumer rabbitConsumer;

        private HashSet<string> knownExchanges;

        private Dictionary<string, IHandlerRegistration> handlers;

        private string privateQueueName;

        private MsgDefRegistry msgReg;

        private TlvBufferReader tlvReader;

        private TlvBufferWriter tlvWriter;

        private ObjectPool<RabbitConsumeContext> consumeContextPool;

        public RabbitServerBus( IModel rabbit )
        {
            this.channel = rabbit;
            
            this.knownExchanges = new HashSet<string>();
            this.handlers = new Dictionary<string, IHandlerRegistration>();
            this.msgReg = new MsgDefRegistry();

            this.tlvReader = new TlvBufferReader();
            this.tlvWriter = new TlvBufferWriter();

            this.consumeContextPool = new ObjectPool<RabbitConsumeContext>( () => new RabbitConsumeContext( this ) );

            this.rabbitConsumer = new EventingBasicConsumer( rabbit );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;
            this.rabbitConsumer.Shutdown += RabbitConsumer_Shutdown;

            // Listen on a queue that's specific to this service instance.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
        }

        public void RegisterHandler<T>( Action<T, IConsumeContext> handler, string queueName ) where T : IMessage, new()
        {
            MessageDef def = this.msgReg.Get<T>();

            this.handlers.Add( def.Name, new HandlerRegistration<T>( this, handler ) );

            this.tlvReader.RegisterContract<T>();

            ProvisionRabbitForMessageDef( def, queueName );
        }

        public void SendMessage( Envelope envelope, IMessage msg )
        {
            SendMessage( envelope, msg, null, null, null );
        }

        public void SendMessage( Envelope envelope, IMessage msg, string exchange, string routingKey, string clientId )
        {
            MessageDef msgDef = this.msgReg.Get( msg );

            var props = this.channel.CreateBasicProperties();

            // Don't assign values to properties if they're null. Rabbit pays attention to whether or
            // not a field was assigned. If it's been assigned, it'll try to serialize it, causing it
            // to serialize a null field.
            if( envelope.CorrelationId != null )
            {
                props.CorrelationId = envelope.CorrelationId;
            }

            if( envelope.SendRepliesTo != null )
            {
                props.ReplyTo = envelope.SendRepliesTo;
            }

            if( clientId != null )
            {
                props.Headers = new Dictionary<string, object>();
                props.Headers["clientId"] = clientId;
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
            string clientId = null;

            if( e.BasicProperties.Headers != null && e.BasicProperties.Headers.ContainsKey( "clientId" ) )
            {
                clientId = Encoding.UTF8.GetString( (byte[])e.BasicProperties.Headers["clientId"] );
            }

            if( this.handlers.TryGetValue( msgName, out IHandlerRegistration handler ) )
            {
                IMessage msg;

                lock( this.tlvReader )
                {
                    this.tlvReader.LoadBuffer( e.Body.ToArray() );
                    msg = (IMessage)this.tlvReader.ReadContract();
                    this.tlvReader.UnloadBuffer();
                }

                handler.Deliver( msg, e.BasicProperties.CorrelationId, e.BasicProperties.ReplyTo, clientId );
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

        private void RabbitConsumer_Shutdown( object sender, ShutdownEventArgs e )
        {
            Console.WriteLine( $"ServerBus: Consumer shutdown." );
        }

        private interface IHandlerRegistration
        {
            void Deliver( IMessage msg, string senderCorrId, string senderReplyTo, string clientId );
        }

        private class HandlerRegistration<T> : IHandlerRegistration where T : IMessage, new()
        {
            private readonly RabbitServerBus parent;
            private readonly Action<T, IConsumeContext> handler;

            public HandlerRegistration( RabbitServerBus parent, Action<T, IConsumeContext> handler )
            {
                this.parent = parent;
                this.handler = handler;
            }

            public void Deliver( IMessage msg, string senderCorrId, string senderReplyTo, string clientId )
            {
                var consumeContext = this.parent.consumeContextPool.Get();
                
                consumeContext.Load( senderCorrId, senderReplyTo, clientId );
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

            public void Load( string senderCorrId, string senderReplyTo, string clientId )
            {
                this.senderCorrId = senderCorrId;
                this.senderReplyTo = senderReplyTo;
                this.clientId = clientId;
            }

            public void Unload()
            {
                this.senderCorrId = null;
                this.senderReplyTo = null;
                this.clientId = null;
            }

            public void Reply( IMessage msg )
            {
                Reply( msg, null );
            }

            public void Reply( IMessage msg, ReplyOptions options )
            {
                Envelope replyEnv = new Envelope()
                {
                    CorrelationId = this.senderCorrId,
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
                    this.parent.SendMessage( replyEnv, msg, "", this.senderReplyTo, this.clientId );
                }
            }
        }
    }
}