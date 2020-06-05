using MiniBus;

namespace Echo.Client.Messages
{
    [Exchange( ExchangeType.Topic, "voren-core" )]
    [MsgName( "voren.echo.EchoRequest" )]
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