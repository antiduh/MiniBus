using PocketTlv;

namespace MiniBus
{
    public interface IConsumeContext
    {
        void Reply( ITlvContract msg );

        void Reply( ITlvContract msg, ReplyOptions options );
    }
}