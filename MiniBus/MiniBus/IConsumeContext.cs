namespace MiniBus
{
    public interface IConsumeContext
    {
        void Reply( IMessage msg );

        void Reply( IMessage msg, ReplyOptions options );
    }
}