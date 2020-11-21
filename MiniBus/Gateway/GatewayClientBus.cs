﻿using System;
using System.Collections.Generic;
using System.Threading;
using PocketTlv;

namespace MiniBus.Gateway
{
    public partial class GatewayClientBus : IClientBus
    {
        private readonly GatewayConnectionProvider connSource;

        private ContractRegistry contractReg;

        private Thread receiveThread;

        private ClientTlvStream tlvStream;

        private MsgDefRegistry msgDefs;

        private Dictionary<string, GatewayRequestContext> pendingConversations;

        public GatewayClientBus( GatewayConnectionProvider connSource )
        {
            if( connSource == null )
            {
                throw new ArgumentNullException( nameof( connSource ) );
            }

            this.connSource = connSource;

            this.msgDefs = new MsgDefRegistry();
            this.pendingConversations = new Dictionary<string, GatewayRequestContext>();

            this.contractReg = new ContractRegistry();
            this.contractReg.Register<GatewayResponseMsg>();
            this.contractReg.Register<GatewayHeartbeatResponse>();

            this.tlvStream = new ClientTlvStream( this.connSource, this.contractReg );
        }

        public event Action Connected;

        public event Action ConnectionLost;

        public void Start()
        {
            this.receiveThread = new Thread( ReceiveThreadEntry );
            this.receiveThread.Start();
            
            this.tlvStream.Connect();
        }

        public void DeclareMessage<T>() where T : ITlvContract, new()
        {
            this.msgDefs.Add<T>();
            this.contractReg.Register<T>();
        }

        public IRequestContext StartRequest()
        {
            return StartRequest( null );
        }

        public IRequestContext StartRequest( string corrId )
        {
            var context = new GatewayRequestContext( this, corrId );

            lock( this.pendingConversations )
            {
                this.pendingConversations.Add( context.ConversationId, context );
            }

            return context;
        }

        public void SendMessage( string corrId, ITlvContract msg )
        {
            var env = new ClientEnvelope()
            {
                CorrelationId = corrId
            };

            SendMessageInternal( env, msg );
        }

        private void SendMessageInternal( ClientEnvelope env, ITlvContract msg )
        {
            MessageDef def = this.msgDefs.Get( msg );

            SendMessageInternal( env, msg, def.Exchange, def.Name );
        }

        private void SendMessageInternal( ClientEnvelope env, ITlvContract msg, string exchange, string routingKey )
        {
            MessageDef def = this.msgDefs.Get( msg );

            var gatewayMsg = new GatewayRequestMsg()
            {
                CorrelationId = env.CorrelationId,
                Exchange = exchange,
                RoutingKey = routingKey,
                MessageName = def.Name,
                Message = msg
            };

            this.tlvStream.Write( gatewayMsg );
        }

        private void ReceiveThreadEntry()
        {
            try
            {
                ConnectionLoop();
            }
            catch( Exception e )
            {
                Console.WriteLine( "GatewayClientBus: Receiver stopped due to exception:\r\n" + e );
            }
        }

        private void ConnectionLoop()
        {
            while( true )
            {
                this.tlvStream.Connect();

                try
                {
                    ReadLoop();
                }
                catch( ChannelDownException ) { }
            }
        }

        private void ReadLoop()
        {
            ITlvContract contract;

            while( true )
            {
                contract = this.tlvStream.Read();

                if( contract == null )
                {
                    break;
                }

                ProcessReceived( contract );
            }
        }

        private void ProcessReceived( ITlvContract msg )
        {
            if( msg.TryResolve( out GatewayResponseMsg response ) )
            {
                DispatchReceived( response );
            }
        }

        private void DispatchReceived( GatewayResponseMsg response )
        {
            var env = new ClientEnvelope()
            {
                CorrelationId = response.CorrelationId,
                SendRepliesTo = response.SendRepliesTo,
            };

            bool foundConvo = false;
            GatewayRequestContext context;

            lock( this.pendingConversations )
            {
                foundConvo = this.pendingConversations.TryGetValue( response.CorrelationId, out context );
            }

            if( foundConvo )
            {
                context.DispatchMessage( env, response.Message );
            }
            else
            {
                Console.WriteLine( $"Client Failure: No handler registered for message {response.MessageName}." );
            }
        }
    }
}