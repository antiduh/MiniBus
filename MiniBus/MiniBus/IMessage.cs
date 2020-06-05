namespace MiniBus
{
    public interface IMessage
    {
        void Read( string payload );

        string Write();
    }
}