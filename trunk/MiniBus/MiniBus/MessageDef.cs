namespace MiniBus
{
    public class MessageDef
    {
        public MessageDef( ExchangeAttribute exchangeInfo, MsgNameAttribute msgInfo )
        {
            this.Exchange = exchangeInfo.Exchange;
            this.ExchangeType = exchangeInfo.Type;

            this.Name = msgInfo.Name;
        }

        public string Exchange { get; private set; }

        public ExchangeType ExchangeType { get; }

        public string Name { get; private set; }
    }
}