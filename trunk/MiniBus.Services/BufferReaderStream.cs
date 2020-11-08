using System;
using System.IO;

namespace MiniBus.Services
{
    /// <summary>
    /// Represents byte arrays (or slices thereof) as a read-only <see cref="Stream"/>, while
    /// allowing re-use of the <see cref="BufferReaderStream"/> to minimize memory churn.
    /// </summary>
    /// <example>
    /// byte[] buffer; int start; int length;
    ///
    /// ReadRequest( out buffer, out start, out length);
    ///
    /// RequestHeader header = ReadBinaryHeader(buffer);
    ///
    /// MemoryStreamView view = streamViewPool.Get();
    ///
    /// // Load a limited view on the buffer, so that XElement tries to read only
    /// // the portion of the buffer that contains the XML data.
    ///
    /// view.Load( buffer, start + header.XmlStart, header.XmlLength );
    ///
    /// xmlData = XElement.Load( buffer )
    ///
    /// streamViewPoolReturn( view );
    /// </example>
    /// <remarks>
    /// This class calls virtual members in its constructor call chain, and thus is marked as sealed to
    /// ensure that it is the most-derived type in the inheritance chain.
    /// </remarks>
    public sealed class BufferReaderStream : Stream
    {
        private byte[] buffer;

        private int bufStart;

        private int bufCount;

        public BufferReaderStream()
        {
            this.buffer = null;
            this.bufStart = 0;
            this.bufCount = 0;
        }

        public BufferReaderStream( byte[] buffer, int start, int length )
        {
            Load( buffer, start, length );
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { return this.bufCount; }
        }

        public override long Position { get; set; }

        public void Load( byte[] buffer, int start, int count )
        {
            if( start < 0 || count < 0 || start + count > buffer.Length )
            {
                throw new ArgumentOutOfRangeException();
            }

            this.buffer = buffer;
            this.bufStart = start;
            this.bufCount = count;
            this.Position = 0;
        }

        public void Unload()
        {
            this.buffer = null;
            this.bufStart = 0;
            this.bufCount = 0;
            this.Position = 0;
        }

        public override int Read( byte[] dest, int offset, int count )
        {
            // Make sure they didn't call us with bad parameters.
            if( offset + count > dest.Length )
            {
                throw new ArgumentOutOfRangeException(
                    "The destination array is too small for the given parameters."
                );
            }

            // Calculate the first index we're going to read from.
            int bufIndex = (int)this.Position + this.bufStart;

            // Figure out how much we can service.
            int readLength = Math.Min( count, bufCount - (int)this.Position );

            if( readLength > 0 )
            {
                Array.Copy( this.buffer, bufIndex, dest, offset, readLength );
            }
            else
            {
                readLength = 0;
            }

            this.Position += readLength;

            return readLength;
        }

        public override long Seek( long offset, SeekOrigin origin )
        {
            switch( origin )
            {
                case SeekOrigin.Begin:
                    this.Position = offset;
                    break;

                case SeekOrigin.Current:
                    this.Position += offset;
                    break;

                case SeekOrigin.End:
                    this.Position = this.bufCount + offset;
                    break;
            }

            return this.Position;
        }

        public override void Flush()
        {
            // Do nothing.
        }

        public override void SetLength( long value )
        {
            throw new NotSupportedException( "Stream is read-only." );
        }

        public override void Write( byte[] buffer, int offset, int count )
        {
            throw new NotSupportedException( "Stream is read-only." );
        }
    }
}