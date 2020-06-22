using System;
using System.Diagnostics;
using System.Threading;
using Echo.Client.Messages;
using MiniBus;
using MiniBus.Services;

namespace Echo.Service
{
    public class EchoService
    {
        private readonly int index;
        private IServerBus bus;

        private Timer voidCallingTimer;

        public EchoService( int index )
        {
            this.index = index;
            this.voidCallingTimer = new Timer( VoidCallingHandler, null, Timeout.Infinite, Timeout.Infinite );
        }

        public void Connect( IServerBus bus )
        {
            this.bus = bus;

            this.bus.RegisterHandler<EchoRequest>( HandleEchoRequest, "voren.echo" );

            this.voidCallingTimer.Change( TimeSpan.FromSeconds( 2 ), TimeSpan.FromSeconds( 2 ) );
        }
                
        private void HandleEchoRequest( IConsumeContext consumeContext, EchoRequest request )
        {
            Console.WriteLine( $"Service {index} handling echo request." );

            var opts = new ReplyOptions() { RedirectReplies = true };

            consumeContext.Reply( new EchoReply( request.EchoMsg ), opts );
        }

        private void VoidCallingHandler( object state )
        {
            var env = new Envelope()
            {
                Message = new CallingVoidEvent() { Message = "Hello." }
            };

            this.bus.SendMessage( env );
        }
    }
}