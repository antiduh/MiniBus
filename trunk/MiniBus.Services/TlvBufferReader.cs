using System;
using PocketTlv;

namespace MiniBus.Services
{
    public class TlvBufferReader : ITlvReader
    {
        private readonly BufferReaderStream view;
        private readonly TlvStreamReader tlvReader;
        private readonly ContractRegistry contractReg;

        public TlvBufferReader()
        {
            this.view = new BufferReaderStream();

            this.contractReg = new ContractRegistry();
            
            this.tlvReader = new TlvStreamReader( this.contractReg );
            this.tlvReader.Connect( this.view );
        }

        public void LoadBuffer( byte[] buffer )
        {
            this.view.Load( buffer, 0, buffer.Length );
        }

        public void LoadBuffer( byte[] buffer, int start, int length )
        {
            this.view.Load( buffer, start, length );
        }

        public void UnloadBuffer()
        {
            this.view.Unload();
        }

        public ITlvContract ReadContract()
        {
            return this.tlvReader.ReadContract();
        }

        public T ReadContract<T>() where T : ITlvContract, new()
        {
            return this.tlvReader.ReadContract<T>();
        }

        public ITag ReadTag()
        {
            return this.tlvReader.ReadTag();
        }

        public T ReadTag<T>() where T : ITag
        {
            return this.tlvReader.ReadTag<T>();
        }

        public void RegisterContract<T>() where T : ITlvContract, new()
        {
            this.contractReg.Register<T>();
        }
    }
}