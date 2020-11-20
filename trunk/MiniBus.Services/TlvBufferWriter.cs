using System;
using System.IO;
using PocketTlv;

namespace MiniBus.Services
{
    public class TlvBufferWriter : ITlvWriter
    {
        private readonly MemoryStream stream;
        private readonly TlvStreamWriter tlvWriter;

        public TlvBufferWriter()
        {
            this.stream = new MemoryStream( 1024 );
            this.tlvWriter = new TlvStreamWriter();
            this.tlvWriter.Connect( this.stream );
        }

        public ReadOnlyMemory<byte> GetBuffer()
        {
            return new ReadOnlyMemory<byte>( this.stream.GetBuffer(), 0, (int)this.stream.Length );
        }

        public void Reset()
        {
            this.stream.Position = 0L;
        }

        public void Write( ITag tag )
        {
            this.tlvWriter.Write( tag );
        }

        public void Write( ITlvContract contract )
        {
            this.tlvWriter.Write( contract );
        }
    }
}