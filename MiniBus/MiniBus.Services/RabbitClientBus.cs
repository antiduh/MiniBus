using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MiniBus.Services
{
    public class RabbitClientBus : IClientBus
    {
        private readonly ModelWithRecovery remodel;
        private readonly IModel channel;

        private readonly HashSet<string> knownExchanges;
        
        private EventingBasicConsumer rabbitConsumer;
        
        private string privateQueueName;

        private MsgDefRegistry msgReg;

        private ObjectPool<RabbitRequestContext> requestPool;

        private Dictionary<Guid, RabbitRequestContext> activeRequests;

        private TlvBufferWriter tlvWriter;
        private TlvBufferReader tlvReader;

        public RabbitClientBus( ModelWithRecovery remodel )
        {
            this.remodel = remodel;
            this.channel = remodel.Model;

            this.knownExchanges = new HashSet<string>();

            this.requestPool = new ObjectPool<RabbitRequestContext>( () => new RabbitRequestContext( this ) );
            this.activeRequests = new Dictionary<Guid, RabbitRequestContext>();
            this.msgReg = new MsgDefRegistry();

            this.tlvWriter = new TlvBufferWriter();
            this.tlvReader = new TlvBufferReader();

            this.rabbitConsumer = new EventingBasicConsumer( this.channel );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;

            this.remodel.RecoverySucceeded += Remodel_RecoverySucceeded;

            ListenOnPrivateQueue();
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

        public void DeclareMessage<T>() where T : IMessage, new()
        {
            this.msgReg.Add<T>();

            this.tlvReader.RegisterContract<T>();
        }

        public IRequestContext StartRequest()
        {
            var context = this.requestPool.Get();

            context.Initialize();

            this.activeRequests.Add( context.ConversationId, context );

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

            // This lock serves to protect both the tlvWriter and the RabbitMQ IModel, since both are not thread-safe.
            lock( this.tlvWriter )
            {
                try
                {
                    this.tlvWriter.Write( msg );
                    this.channel.BasicPublish( exchange, routingKey, props, this.tlvWriter.GetBuffer() );
                }
                catch( Exception e )
                {
                    Console.WriteLine( "Channel crash" );
                }
                finally
                {
                   this.tlvWriter.Reset();
                }
            }
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            IMessage msg;

            string msgName = e.BasicProperties.MessageId;

            lock( this.tlvReader )
            {
                try
                {
                    this.tlvReader.LoadBuffer( e.Body.ToArray() );
                    msg = (IMessage)this.tlvReader.ReadContract();
                }
                finally
                {
                    this.tlvReader.UnloadBuffer();
                }
            }

            Envelope env = new Envelope()
            {
                CorrelationId = e.BasicProperties.CorrelationId,
                SendRepliesTo = e.BasicProperties.ReplyTo,
            };

            if( TryDispatchConversation( env, msg ) == false )
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
                RabbitRequestContext requestContext;

                // Have to lock when inspecting our conversation map, but once the lookup is
                // complete we can dispatch to the receive queue without locking since the receive
                // queue is a concurrent queue.
                lock( this.activeRequests )
                {
                    result = this.activeRequests.TryGetValue( convo, out requestContext );
                }

                if( result )
                {
                    requestContext.DispatchMessage( env, msg );
                }
            }

            return result;
        }

        private void ListenOnPrivateQueue()
        {
            // Listen on an anonymous queue.
            try
            {
                this.privateQueueName = this.channel.QueueDeclare().QueueName;
                this.channel.BasicConsume( this.privateQueueName, true, this.rabbitConsumer );
            }
            catch( Exception e )
            {
                Console.WriteLine( "RabbitClientBus: Recovery failed." );
            }
        }

        private void Remodel_RecoverySucceeded( object sender, EventArgs e )
        {
            Console.WriteLine( "RabbitClientBus: Reconnecting..." );
            ListenOnPrivateQueue();
            Console.WriteLine( "RabbitClientBus: Reconnecting... done." );
        }

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
                this.bus.activeRequests.Remove( this.ConversationId );

                this.ConversationId = Guid.Empty;
                this.redirectQueue = null;

                while( this.inQueue.Count > 0 )
                {
                    this.inQueue.TryTake( out _ );
                }

                this.bus.requestPool.Return( this );
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