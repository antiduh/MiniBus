﻿using System;
using System.IO;
using PocketTlv;

namespace MiniBus.ServiceApi
{
    public class TlvBufferWriter : ITlvWriter
    {
        private MemoryStream stream;
        private TlvStreamWriter tlvWriter;

        public TlvBufferWriter()
        {
            this.stream = new MemoryStream( 1024 );
            this.tlvWriter = new TlvStreamWriter( this.stream );
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