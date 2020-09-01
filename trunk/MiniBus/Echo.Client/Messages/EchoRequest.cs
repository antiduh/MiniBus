using ClientDemo.Messages;
using MiniBus;
using PocketTLV;
using PocketTLV.Primitives;

namespace Echo.Client.Messages
{
    [MsgName( "voren.echo.EchoRequest", "voren-core" )]
    public class EchoRequest : IMessage
    {
        public EchoRequest() { }

        public EchoRequest( string echoMsg )
        {
            EchoMsg = echoMsg;
        }

        public string EchoMsg { get; set; }

        int ITlvContract.ContractId => EchoTlvs.EchoRequest;

        void ITlvContract.Parse( ITlvParseContext parse )
        {
            this.EchoMsg = parse.Tag<StringTag>( 0 );
        }

        void ITlvContract.Save( ITlvSaveContext save )
        {
            save.Tag( 0, new StringTag( this.EchoMsg ) );
        }
    }
}