using MiniBus;

namespace Echo.Client.Messages
{
    [Exchange( ExchangeType.Topic, "voren-core" )]
    [MsgName( "voren.echo.EchoReply" )]
    public class EchoReply : IMessage
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