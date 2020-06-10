using MiniBus;

namespace Echo.Client.Messages
{
    [MsgName( "voren.echo.EchoRequest" )]
    [Exchange( ExchangeType.Topic, "voren-core" )]
    public class EchoRequest : IMessage
    {
        public string EchoMsg { get; set; }

        void IMessage.Read( string payload )
        {
            this.EchoMsg = payload;
        }

        string IMessage.Write()
        {
            return this.EchoMsg;
        }
    }
}