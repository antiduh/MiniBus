using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using MiniBus;
using PocketTlv;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MiniBus.Services
{
    public class RabbitClientBus : IClientBus
    {
        private readonly IModel channel;
        private readonly HashSet<string> knownExchanges;
        private EventingBasicConsumer rabbitConsumer;
        private string privateQueueName;

        private MsgDefRegistry msgReg;

        private Dictionary<Guid, RabbitRequestContext> pendingConversations;
        private readonly Dictionary<string, IEventRegistration> eventHandlers;

        private MemoryStream tlvStream;
        private TlvStreamReader tlvReader;

        public RabbitClientBus( IModel channel )
        {
            this.channel = channel;

            this.knownExchanges = new HashSet<string>();
            this.eventHandlers = new Dictionary<string, IEventRegistration>();

            this.pendingConversations = new Dictionary<Guid, RabbitRequestContext>();
            this.msgReg = new MsgDefRegistry();

            this.tlvStream = new MemoryStream();
            this.tlvReader = new TlvStreamReader( this.tlvStream );

            this.rabbitConsumer = new EventingBasicConsumer( this.channel );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;

            // Listen on an anonymous queue.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
        }

        public void SendMessage( Envelope envelope )
        {
            MessageDef msgDef = this.msgReg.Get( envelope.Message );

            SendMessageInternal( envelope, msgDef, msgDef.Exchange, msgDef.Name );
        }

        public void SendMessage( Envelope envelope, string exchange, string routingKey )
        {
            MessageDef msgDef = this.msgReg.Get( envelope.Message );

            SendMessageInternal( envelope, msgDef, exchange, routingKey );
        }

        public void EventHandler<T>( Action<T> handler ) where T : IMessage, new()
        {
            MessageDef msgDef = this.msgReg.Get<T>();

            this.tlvReader.RegisterContract<T>();

            // - Make sure the exchange exists
            // - Bind the routing key to our private queue.

            this.eventHandlers.Add( msgDef.Name, new EventRegistration<T>( handler ) );

            if( knownExchanges.Contains( msgDef.Exchange ) == false )
            {
                this.channel.ExchangeDeclare( msgDef.Exchange, "topic", true, false );
                this.knownExchanges.Add( msgDef.Exchange );
            }

            this.channel.QueueBind( this.privateQueueName, msgDef.Exchange, msgDef.Name );
        }

        public void DeclareMessage<T>() where T : IMessage, new()
        {
            this.msgReg.Add<T>();

            this.tlvReader.RegisterContract<T>();
        }

        public IRequestContext StartRequest()
        {
            var context = new RabbitRequestContext( this );

            this.pendingConversations.Add( context.ConversationId, context );

            return context;
        }

        private void SendMessageInternal( Envelope envelope, MessageDef msgDef, string exchange, string routingKey )
        {
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

            props.MessageId = msgDef.Name;

            // TODO improve efficiency.
            var stream = new MemoryStream();
            var writer = new TlvStreamWriter( stream );

            writer.Write( envelope.Message );

            ReadOnlyMemory<byte> body = stream.GetBuffer();
            this.channel.BasicPublish( exchange, routingKey, props, body );
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            IMessage msg;

            string msgName = e.BasicProperties.MessageId;

            byte[] body = e.Body.ToArray();
            this.tlvStream.Position = 0L;
            this.tlvStream.Write( body, 0, body.Length );
            this.tlvStream.Position = 0L;

            msg = (IMessage)this.tlvReader.ReadContract();

            Envelope env = new Envelope()
            {
                CorrId = e.BasicProperties.CorrelationId,
                SendRepliesTo = e.BasicProperties.ReplyTo,
            };
           
            if( TryDispatchConversation( env, msg ) == false &&
                TryDispatchEvent( msgName, msg ) == false )
            {
                Console.WriteLine( $"Client Failure: No handler registered for message {msgName}." );
            }
        }

        private bool TryDispatchConversation( Envelope env, IMessage msg )
        {
            bool result = false;

            if( env.CorrId != null )
            {
                Guid convo = new Guid( env.CorrId );

                if( this.pendingConversations.TryGetValue( convo, out RabbitRequestContext requestContext ) )
                {
                    requestContext.DispatchMessage( env, msg );
                    result = true;
                }
            }

            return result;
        }

        private bool TryDispatchEvent( string msgName, IMessage msg )
        {
            bool result = false;

            if( this.eventHandlers.TryGetValue( msgName, out IEventRegistration eventReg ) )
            {
                eventReg.Deliver( msg );
                result = true;
            }

            return result;
        }

        private interface IEventRegistration
        {
            void Deliver( IMessage rawMsg );
        }

        private class EventRegistration<T> : IEventRegistration where T : IMessage, new()
        {
            private readonly Action<T> handler;

            public EventRegistration( Action<T> handler )
            {
                this.handler = handler;
            }

            public void Deliver( IMessage rawMsg )
            {
                this.handler.Invoke( (T)rawMsg );
            }
        }

        // TODO rename or synchronize usage with variable names (pendingConversations).
        private class RabbitRequestContext : IRequestContext
        {
            private readonly RabbitClientBus bus;
            private BlockingCollection<Dispatch> inQueue;

            private bool haveRedirect;
            private string redirectQueue;

            public RabbitRequestContext( RabbitClientBus bus )
            {
                this.bus = bus;

                this.redirectQueue = null;

                this.ConversationId = Guid.NewGuid();

                this.inQueue = new BlockingCollection<Dispatch>();

                this.haveRedirect = false;
                this.redirectQueue = null;
            }

            public Guid ConversationId { get; private set; }

            public void SendRequest( IMessage msg )
            {
                Envelope env = new Envelope()
                {
                    Message = msg,
                    SendRepliesTo = bus.privateQueueName,
                    CorrId = this.ConversationId.ToString( "B" )
                };

                if( haveRedirect == false )
                {
                    bus.SendMessage( env );
                }
                else
                {
                    bus.SendMessage( env, "", this.redirectQueue );
                }
            }

            public IMessage WaitResponse( TimeSpan timeout )
            {
                return WaitResponseInternal( timeout );
            }

            public T WaitResponse<T>( TimeSpan timeout ) where T : IMessage
            {
                IMessage msg = WaitResponseInternal( timeout );

                if( msg is T casted )
                {
                    return casted;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Received unexpected message '{msg.GetType()}'."
                    );
                }
            }

            public void DispatchMessage( Envelope env, IMessage msg )
            {
                this.inQueue.Add( new Dispatch( env, msg ) );
            }

            private IMessage WaitResponseInternal( TimeSpan timeout )
            {
                Dispatch dispatch;

                if( this.inQueue.TryTake( out dispatch, timeout ) == false )
                {
                    throw new TimeoutException();
                }

                if( dispatch.Envelope.SendRepliesTo != null )
                {
                    // The reply sent us a redirect to a private queue.
                    this.haveRedirect = true;
                    this.redirectQueue = dispatch.Envelope.SendRepliesTo;
                }

                return dispatch.Message;
            }

            private struct Dispatch
            {
                public Dispatch( Envelope envelope, IMessage message )
                {
                    Envelope = envelope;
                    Message = message;
                }

                public Envelope Envelope { get; private set; }

                public IMessage Message { get; private set; }
            }
        }
    }
}