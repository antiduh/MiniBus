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
            this.bus.KnownMessage<EchoReply>();
            this.bus.EventHandler<CallingVoidEvent>( HandleCallingVoid );
        }

        public event Action<string> CallOfTheVoid;

        public void DoEcho( string text )
        {
            var request = this.bus.StartRequest();
            request.SendMessage( new EchoRequest( text ) );

            var response = request.WaitResponse<EchoReply>( TimeSpan.FromSeconds( 5.0 ) );

            if( response.EchoMsg != text )
            {
                throw new InvalidOperationException();
            }
        }

        public void DoMultiEcho( string text )
        {
            var request = this.bus.StartRequest();

            for( int i = 0; i < 5; i++ )
            {
                request.SendMessage( new EchoRequest( text ) );
                var response = request.WaitResponse<EchoReply>( TimeSpan.FromSeconds( 5.0 ) );

                if( response.EchoMsg != text )
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private void HandleCallingVoid( CallingVoidEvent callingVoid )
        {
            this.CallOfTheVoid?.Invoke( callingVoid.Message );
        }
    }
}