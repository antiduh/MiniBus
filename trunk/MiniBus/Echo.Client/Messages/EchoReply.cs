using MiniBus;

namespace Echo.Client.Messages
{
    [MsgName( "voren.echo.EchoReply" )]
    [Exchange( ExchangeType.Topic, "voren-core" )]
    public class EchoReply : IMessage
    {
        public EchoReply()
        {
        }

        public EchoReply( string echoMsg )
        {
            EchoMsg = echoMsg;
        }

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