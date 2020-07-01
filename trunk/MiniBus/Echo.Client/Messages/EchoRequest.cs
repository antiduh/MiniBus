using MiniBus;

namespace Echo.Client.Messages
{
    [MsgName( "voren.echo.EchoRequest", "voren-core" )]
    public class EchoRequest : IMessage
    {
        public EchoRequest()
        {
        }

        public EchoRequest( string echoMsg )
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