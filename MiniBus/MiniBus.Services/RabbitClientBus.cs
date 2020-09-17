using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private ObjectPool<RabbitRequestContext> requestContextPool;

        private Dictionary<Guid, RabbitRequestContext> pendingConversations;
        private readonly Dictionary<string, IEventRegistration> eventHandlers;

        private TlvBufferWriter tlvWriter;
        private TlvBufferReader tlvReader;

        public RabbitClientBus( IModel channel )
        {
            this.channel = channel;

            this.knownExchanges = new HashSet<string>();
            this.eventHandlers = new Dictionary<string, IEventRegistration>();

            this.requestContextPool = new ObjectPool<RabbitRequestContext>( () => new RabbitRequestContext( this ) );
            this.pendingConversations = new Dictionary<Guid, RabbitRequestContext>();
            this.msgReg = new MsgDefRegistry();

            this.tlvWriter = new TlvBufferWriter();
            this.tlvReader = new TlvBufferReader();

            this.rabbitConsumer = new EventingBasicConsumer( this.channel );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;

            // Listen on an anonymous queue.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
        }

        public void SendMessage( Envelope env, IMessage msg )
        {
            MessageDef msgDef = this.msgReg.Get( msg );

            SendMessageInternal( env, msg, msgDef, msgDef.Exchange, msgDef.Name );
        }

        public void SendMessage( Envelope env, IMessage msg, string exchange, string routingKey )
        {
            MessageDef msgDef = this.msgReg.Get( msg );

            SendMessageInternal( env, msg, msgDef, exchange, routingKey );
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
            var context = this.requestContextPool.Get();

            context.Initialize();

            this.pendingConversations.Add( context.ConversationId, context );

            return context;
        }

        private void SendMessageInternal( Envelope envelope, IMessage msg, MessageDef msgDef, string exchange, string routingKey )
        {
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
            IMessage msg;

            string msgName = e.BasicProperties.MessageId;

            lock( this.tlvReader )
            {
                this.tlvReader.LoadBuffer( e.Body.ToArray() );
                msg = (IMessage)this.tlvReader.ReadContract();
                this.tlvReader.UnloadBuffer();
            }

            Envelope env = new Envelope()
            {
                CorrelationId = e.BasicProperties.CorrelationId,
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

            if( env.CorrelationId != null )
            {
                Guid convo = new Guid( env.CorrelationId );

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

            private string redirectQueue;

            public RabbitRequestContext( RabbitClientBus bus )
            {
                this.bus = bus;

                this.inQueue = new BlockingCollection<Dispatch>();
            }

            public Guid ConversationId { get; private set; }

            public void Initialize()
            {
                this.ConversationId = Guid.NewGuid();
                this.redirectQueue = null;
            }

            public void Dispose()
            {
                this.bus.pendingConversations.Remove( this.ConversationId );

                this.ConversationId = Guid.Empty;
                this.redirectQueue = null;

                while( this.inQueue.Count > 0 )
                {
                    this.inQueue.Take();
                }
            }

            public void SendRequest( IMessage msg )
            {
                Envelope env = new Envelope()
                {
                    SendRepliesTo = bus.privateQueueName,
                    CorrelationId = this.ConversationId.ToString( "B" )
                };

                if( this.redirectQueue == null )
                {
                    bus.SendMessage( env, msg );
                }
                else
                {
                    bus.SendMessage( env, msg, "", this.redirectQueue );
                }
            }

            public IMessage WaitResponse( TimeSpan timeout )
            {
                return WaitResponseInternal( timeout );
            }

            public T WaitResponse<T>( TimeSpan timeout ) where T : IMessage, new()
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

            internal void DispatchMessage( Envelope env, IMessage msg )
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