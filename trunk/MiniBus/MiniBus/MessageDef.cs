namespace MiniBus
{
    public class MessageDef
    {
        public MessageDef( string name, string exchange )
        {
            this.Name = name;
            this.Exchange = exchange;
        }

        public string Name { get; private set; }

        public string Exchange { get; private set; }
    }
}