using PocketTlv;

namespace MiniBus
{
    public interface IClientBus
    {
        void DeclareMessage<T>() where T : ITlvContract, new();

        void SendMessage( string corrId, ITlvContract msg );

        IRequestContext StartRequest();

        IRequestContext StartRequest( string corrId );
    }
}