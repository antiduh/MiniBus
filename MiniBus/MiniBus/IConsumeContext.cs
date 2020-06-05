namespace MiniBus
{
    public interface IConsumeContext
    {
        void Reply( IMessage msg );
    }
}