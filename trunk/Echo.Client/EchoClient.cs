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
            this.bus.DeclareMessage<EchoReply>();
        }

        public void DoEcho( string text )
        {
            using( IRequestContext request = this.bus.StartRequest() )
            {
                try
                {
                    request.WithRetry( () =>
                    {
                        request.SendRequest( new EchoRequest( text ) );
                        var response = request.WaitResponse<EchoReply>( TimeSpan.FromSeconds( 5.0 ) );
                    } );
                }
                catch
                {
                    Console.WriteLine( "Echo command failed" );
                    throw;
                }
            }
        }

        public void DoMultiEcho( string text )
        {
            using( IRequestContext request = this.bus.StartRequest() )
            {
                for( int i = 0; i < 5; i++ )
                {
                    request.SendRequest( new EchoRequest( text ) );
                    var response = request.WaitResponse<EchoReply>( TimeSpan.FromSeconds( 5.0 ) );

                    if( response.EchoMsg != text )
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }
    }
}