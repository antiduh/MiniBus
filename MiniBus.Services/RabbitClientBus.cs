﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using PocketTlv;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace MiniBus.Services
{
    public class RabbitClientBus : IClientBus
    {
        private readonly ModelWithRecovery remodel;
        private readonly IModel channel;

        private EventingBasicConsumer rabbitConsumer;

        private string privateQueueName;

        private MsgDefRegistry msgReg;

        private ConcurrentObjectPool<RabbitRequestContext> requestPool;

        private Dictionary<string, RabbitRequestContext> activeRequests;

        private TlvBufferWriter tlvWriter;
        private TlvBufferReader tlvReader;

        public RabbitClientBus( ModelWithRecovery remodel )
        {
            this.remodel = remodel;
            this.channel = remodel.Model;

            this.requestPool = new ConcurrentObjectPool<RabbitRequestContext>( () => new RabbitRequestContext( this ) );
            this.activeRequests = new Dictionary<string, RabbitRequestContext>();
            this.msgReg = new MsgDefRegistry();

            this.tlvWriter = new TlvBufferWriter();
            this.tlvReader = new TlvBufferReader();

            this.rabbitConsumer = new EventingBasicConsumer( this.channel );
            this.rabbitConsumer.Received += DispatchReceivedRabbitMsg;
            this.rabbitConsumer.Shutdown += RabbitConsumer_Shutdown;

            this.remodel.RecoverySucceeded += Remodel_RecoverySucceeded;

            ListenOnPrivateQueue();
        }

        private void RabbitConsumer_Shutdown( object sender, ShutdownEventArgs e )
        {
            Console.WriteLine( "RabbitClientBus: Connection to rabbit lost." );
        }

        public void DeclareMessage<T>() where T : ITlvContract, new()
        {
            this.msgReg.Add<T>();

            this.tlvReader.RegisterContract<T>();
        }

        public IRequestContext StartRequest()
        {
            return StartRequest( null );
        }

        public IRequestContext StartRequest( string corrId )
        {
            RabbitRequestContext context;

            context = this.requestPool.Get();

            context.Initialize( corrId );

            lock( this.activeRequests )
            {
                this.activeRequests.Add( context.CorrelationId, context );
            }

            return context;
        }

        public void SendMessage( string correlationId, ITlvContract msg )
        {
            MessageDef msgDef = this.msgReg.Get( msg );
            var envelope = new ClientEnvelope() { CorrelationId = correlationId };

            SendMessageInternal( envelope, msg, msgDef, msgDef.Exchange, msgDef.Name );
        }

        private void SendMessage( ClientEnvelope env, ITlvContract msg )
        {
            MessageDef msgDef = this.msgReg.Get( msg );

            SendMessageInternal( env, msg, msgDef, msgDef.Exchange, msgDef.Name );
        }

        private void SendMessage( ClientEnvelope env, ITlvContract msg, string exchange, string routingKey )
        {
            MessageDef msgDef = this.msgReg.Get( msg );

            SendMessageInternal( env, msg, msgDef, exchange, routingKey );
        }

        private void SendMessageInternal( ClientEnvelope env, ITlvContract msg, MessageDef msgDef, string exchange, string routingKey )
        {
            var props = this.channel.CreateBasicProperties();

            // Don't assign values to properties if they're null. Rabbit pays attention to whether or
            // not a field was assigned. If it's been assigned, it'll try to serialize it, causing it
            // to serialize a null field.
            if( env.CorrelationId != null )
            {
                props.CorrelationId = env.CorrelationId;
            }

            if( env.SendRepliesTo != null )
            {
                props.ReplyTo = env.SendRepliesTo;
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
                catch( AlreadyClosedException )
                {
                    throw new ChannelDownException();
                }
                catch( RabbitMQClientException e )
                {
                    throw new DeliveryException( "Failed to send message due to exception from RabbitMQ.Client", e );
                }
                finally
                {
                    this.tlvWriter.Reset();
                }
            }
        }

        private void DispatchReceivedRabbitMsg( object sender, BasicDeliverEventArgs e )
        {
            ITlvContract msg;

            string msgName = e.BasicProperties.MessageId;

            lock( this.tlvReader )
            {
                try
                {
                    this.tlvReader.LoadBuffer( e.Body.ToArray() );
                    msg = this.tlvReader.ReadContract();
                }
                finally
                {
                    this.tlvReader.UnloadBuffer();
                }
            }

            var env = new ClientEnvelope()
            {
                CorrelationId = e.BasicProperties.CorrelationId,
                SendRepliesTo = e.BasicProperties.ReplyTo,
            };

            if( TryDispatchConversation( env, msg ) == false )
            {
                Console.WriteLine( $"Client Failure: No handler registered for message {msgName}." );
            }
        }

        private bool TryDispatchConversation( ClientEnvelope env, ITlvContract msg )
        {
            if( env.CorrelationId == null )
            {
                throw new InvalidOperationException();
            }

            RabbitRequestContext requestContext;
            bool result;

            // Have to lock when inspecting our conversation map, but once the lookup is
            // complete we can dispatch to the receive queue without locking since the receive
            // queue is a concurrent queue.
            lock( this.activeRequests )
            {
                result = this.activeRequests.TryGetValue( env.CorrelationId, out requestContext );
            }

            if( result )
            {
                requestContext.DispatchMessage( env, msg );
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
            Console.WriteLine( "RabbitClientBus: Reconnecting rabbit ..." );
            ListenOnPrivateQueue();
            Console.WriteLine( "RabbitClientBus: Reconnecting rabbit ... done." );
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

            public string CorrelationId { get; private set; }

            public void Initialize( string corrId = null )
            {
                if( corrId == null )
                {
                    this.CorrelationId = CorrId.Create();
                }
                else
                {
                    this.CorrelationId = corrId;
                }

                this.redirectQueue = null;
            }

            public void Dispose()
            {
                lock( this.bus.activeRequests )
                {
                    this.bus.activeRequests.Remove( this.CorrelationId );
                }

                this.CorrelationId = null;
                this.redirectQueue = null;

                while( this.inQueue.Count > 0 )
                {
                    this.inQueue.TryTake( out _ );
                }

                this.bus.requestPool.Return( this );
            }

            public void SendRequest( ITlvContract msg )
            {
                var env = new ClientEnvelope()
                {
                    SendRepliesTo = bus.privateQueueName,
                    CorrelationId = this.CorrelationId
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

            public ITlvContract WaitResponse( TimeSpan timeout )
            {
                return WaitResponseInternal( timeout );
            }

            public T WaitResponse<T>( TimeSpan timeout ) where T : ITlvContract, new()
            {
                ITlvContract msg = WaitResponseInternal( timeout );

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

            public void WithRetry( Action action )
            {
                ExceptionDispatchInfo failure = null;

                for( int i = 0; i < 5; i++ )
                {
                    try
                    {
                        action();
                        failure = null;

                        break;
                    }
                    catch( DeliveryException e )
                    {
                        failure = ExceptionDispatchInfo.Capture( e );
                        Thread.Sleep( 1000 );
                    }
                }

                if( failure != null )
                {
                    failure.Throw();
                }
            }

            internal void DispatchMessage( ClientEnvelope env, ITlvContract msg )
            {
                this.inQueue.Add( new Dispatch( env, msg ) );
            }

            private ITlvContract WaitResponseInternal( TimeSpan timeout )
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
                public Dispatch( ClientEnvelope env, ITlvContract message )
                {
                    Envelope = env;
                    Message = message;
                }

                public ClientEnvelope Envelope { get; private set; }

                public ITlvContract Message { get; private set; }
            }
        }
    }
}