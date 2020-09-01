using System;
using PocketTlv;

namespace MiniBus.ServiceApi
{
    public class TlvBufferReader : ITlvReader
    {
        public ITlvContract ReadContract()
        {
            throw new NotImplementedException();
        }

        public T ReadContract<T>() where T : ITlvContract, new()
        {
            throw new NotImplementedException();
        }

        public ITag ReadTag()
        {
            throw new NotImplementedException();
        }

        public T ReadTag<T>() where T : ITag
        {
            throw new NotImplementedException();
        }

        public void RegisterContract<T>() where T : ITlvContract, new()
        {
            throw new NotImplementedException();
        }
    }
}