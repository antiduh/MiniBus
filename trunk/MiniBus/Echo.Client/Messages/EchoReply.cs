using ClientDemo.Messages;
using MiniBus;
using PocketTLV;
using PocketTLV.Primitives;

namespace Echo.Client.Messages
{
    [MsgName( "voren.echo.EchoReply", "voren-core" )]
    public class EchoReply : IMessage
    {
        public EchoReply() { }

        public EchoReply( string echoMsg )
        {
            EchoMsg = echoMsg;
        }

        public string EchoMsg { get; set; }

        int ITlvContract.ContractId => EchoTlvs.EchoReply;

        void ITlvContract.Parse( ITlvParseContext parse )
        {
            this.EchoMsg = parse.Tag<StringTag>( 0 );
        }

        void ITlvContract.Save( ITlvSaveContext save )
        {
            save.Save( 0, new StringTag( this.EchoMsg ) );
        }
    }
}