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
        }

        public void DoEcho( string text )
        {
            var request = this.bus.StartRequest();

            request.SendMessage( new EchoRequest() { EchoMsg = text } );

            Envelope response = request.WaitResponse( TimeSpan.FromSeconds( 5.0 ) );

            if( response.Message is EchoRequest echoResponse )
            {
                if( echoResponse.EchoMsg != text )
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}