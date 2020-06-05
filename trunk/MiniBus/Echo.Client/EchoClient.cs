using System;
using Echo.Client.Messages;
using MiniBus;

namespace Echo.Client
{
    public class EchoClient
    {
        private readonly IClientBus bus;

        public EchoClient( IClientBus bus )
        {
            this.bus = bus;
            this.bus.AddMessage<EchoReply>();
        }

        public void DoEcho( string text )
        {
            var request = this.bus.StartRequest();

            request.SendMessage( new EchoRequest() { EchoMsg = text } );

            Envelope response = request.WaitResponse( TimeSpan.FromSeconds( 5.0 ) );

            if( response.Message is EchoReply echoReply )
            {
                if( echoReply.EchoMsg != text )
                {
                    throw new InvalidOperationException();
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}