using System;
using MiniBus;

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

        void IMessage.Read( string payload )
        {
            this.Message = payload;
        }

        string IMessage.Write()
        {
            return this.Message;
        }
    }
}