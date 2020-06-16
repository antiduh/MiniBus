using System;
using Echo.Client.Messages;
using MiniBus;
using MiniBus.Services;

namespace Echo.Service
{
    public class EchoService
    {
        private readonly int index;
        private IServerBus bus;

        public EchoService( int index )
        {
            this.index = index;
        }

        public void Connect( IServerBus bus )
        {
            this.bus = bus;

            this.bus.RegisterHandler<EchoRequest>( HandleEchoRequest );
        }

        private void HandleEchoRequest( IConsumeContext consumeContext, EchoRequest request )
        {
            Console.WriteLine( $"Service {index} handling echo request." );

            var opts = new ReplyOptions() { RedirectReplies = true };

            consumeContext.Reply( new EchoReply( request.EchoMsg ), opts ) ;
        }
    }
}