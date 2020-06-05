using Echo.Client.Messages;
using MiniBus;
using MiniBus.Services;
using RabbitMQ.Client;

namespace Echo.Service
{
    public class EchoService
    {
        private IModel channel;
        private RabbitServerBus bus;

        public EchoService()
        {
        }

        public void Connect( IModel channel )
        {
            this.channel = channel;
            this.bus = new RabbitServerBus( this.channel );

            this.bus.RegisterHandler<EchoRequest>( HandleEchoRequest );
        }

        private void HandleEchoRequest( IConsumeContext consumeContext, EchoRequest request )
        {
            var reply = new EchoReply()
            {
                EchoMsg = request.EchoMsg,
            };

            consumeContext.Reply( reply );
        }
    }
}