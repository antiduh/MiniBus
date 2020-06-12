using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using MiniBus;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Demo
{
    public class RabbitClientBus : IClientBus
    {
        private readonly IModel channel;
        private EventingBasicConsumer rabbitConsumer;
        private string privateQueueName;

        private Dictionary<Guid, RabbitRequestContext> pendingConversations;

        private MessageDefRegistry msgReg;

        private Dictionary<string, IMsgReader> msgReaders;

        public RabbitClientBus( IModel channel )
        {
            this.channel = channel;

            this.pendingConversations = new Dictionary<Guid, RabbitRequestContext>();
            this.msgReg = new MessageDefRegistry();
            this.msgReaders = new Dictionary<string, IMsgReader>();

            this.rabbitConsumer = new EventingBasicConsumer( this.channel );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;

            // Listen on an anonymous queue.
            this.privateQueueName = this.channel.QueueDeclare().QueueName;
            this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
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

        public void AddMessage<T>() where T : IMessage, new()
        {
            MessageDef msgDef = this.msgReg.Get<T>();

            this.msgReaders.Add( msgDef.Name, new MsgReader<T>() );
        }

        public IRequestContext StartRequest()
        {
            var context = new RabbitRequestContext( this );

            this.pendingConversations.Add( context.ConversationId, context );

            return context;
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            RabbitRequestContext requestContext;

            if( e.BasicProperties.CorrelationId != null && 
                this.pendingConversations.TryGetValue( new Guid( e.BasicProperties.CorrelationId ), out requestContext ) )
            {
                
                string msgName;
                string payload;
                Serializer.ReadBody( e.Body.ToArray(), out msgName, out payload );

                if( this.msgReaders.ContainsKey( msgName ) == false )
                {
                    throw new InvalidOperationException(
                        $"Failed to deserialize unknown message '{msgName}'."
                    );
                }

                IMessage msg = this.msgReaders[msgName].Read( payload );

                Envelope env = new Envelope()
                {
                    CorrId = e.BasicProperties.CorrelationId,
                    SendRepliesTo = e.BasicProperties.ReplyTo,
                    Message = msg
                };

                requestContext.DispatchMessage( env );
            }
        }

        private interface IMsgReader
        {
            IMessage Read( string payload );
        }

        private class MsgReader<T> 
            : IMsgReader 
            where T : IMessage, new()
        {
            public IMessage Read( string payload )
            {
                var msg = new T();
                
                msg.Read( payload );

                return msg;

            }
        }

        private class RabbitRequestContext : IRequestContext
        {
            private readonly RabbitClientBus bus;
            private string exchange;
            private BlockingCollection<Envelope> inQueue;

            private string destQueue;

            public RabbitRequestContext( RabbitClientBus bus )
            {
                this.bus = bus;

                this.destQueue = null;

                this.ConversationId = Guid.NewGuid();

                this.inQueue = new BlockingCollection<Envelope>();
            }

            public Guid ConversationId { get; private set; }

            public void SendMessage( IMessage msg )
            {
                Envelope env = new Envelope()
                {
                    Message = msg,
                    SendRepliesTo = bus.privateQueueName,
                    CorrId = this.ConversationId.ToString( "B" )
                };
                
                bus.SendMessage( env );
            }

            public IMessage WaitResponse( TimeSpan timeout )
            {
                return WaitResponseInternal( timeout ).Message;
            }

            public T WaitResponse<T>( TimeSpan timeout ) where T : IMessage
            {
                Envelope envelope = WaitResponseInternal( timeout );

                if( envelope.Message is T casted )
                {
                    return casted;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Received unexpected message '{envelope.Message.GetType()}'."
                    );
                }
            }

            public void DispatchMessage( Envelope env )
            {
                this.inQueue.Add( env );
            }

            private Envelope WaitResponseInternal( TimeSpan timeout )
            {
                Envelope env;

                if( this.inQueue.TryTake( out env, timeout ) == false )
                {
                    throw new TimeoutException();
                }

                if( env.SendRepliesTo != null )
                {
                    throw new NotImplementedException();

                    // The reply sent us a redirect to a private queue.
                    this.exchange = "";
                    this.destQueue = env.SendRepliesTo;
                }

                return env;
            }

        }
    }
}