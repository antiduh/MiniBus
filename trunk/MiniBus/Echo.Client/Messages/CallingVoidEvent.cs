using System;
using ClientDemo.Messages;
using MiniBus;
using PocketTLV;
using PocketTLV.Primitives;

namespace Echo.Client.Messages
{
    /// <summary>
    /// Occurs when the void calls out to you, instead of you calling out to the void.
    /// </summary>
    [MsgName( "voren.echo.CallingVoid", "voren-core" )]
    public class CallingVoidEvent : IMessage
    {
        public CallingVoidEvent() { }

        public CallingVoidEvent( string message )
        {
            Message = message;
        }

        public string Message { get; set; }

        int ITlvContract.ContractId => EchoTlvs.VoidCalling;

        void ITlvContract.Parse( ITlvParseContext parseContext )
        {
            this.Message = parseContext.Tag<StringTag>( 0 );
        }

        void ITlvContract.Save( ITlvSaveContext saveContext )
        {
            saveContext.Save( 0, new StringTag( this.Message ) );
        }
    }
}