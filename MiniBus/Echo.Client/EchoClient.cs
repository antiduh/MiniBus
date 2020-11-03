using System;
using System.Threading;
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

                while( true )
                {
                    try
                    {
                        request.SendRequest( new EchoRequest( text ) );
                        var response = request.WaitResponse<EchoReply>( TimeSpan.FromSeconds( 5.0 ) );

                        if( response.EchoMsg != text )
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }
                    catch( TimeoutException e )
                    {
                        Console.WriteLine( "Timeout: retrying." );
                        Thread.Sleep( 1000 );
                    }
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