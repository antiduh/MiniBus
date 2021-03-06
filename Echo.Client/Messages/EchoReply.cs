﻿using ClientDemo.Messages;
using MiniBus;
using PocketTlv;

namespace Echo.Client.Messages
{
    [MsgName( "voren.echo.EchoReply", "voren-core" )]
    public class EchoReply : ITlvContract
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
            save.Tag( 0, new StringTag( this.EchoMsg ) );
        }
    }
}