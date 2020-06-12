namespace MiniBus
{
    public class MessageDef
    {
        public MessageDef( ExchangeAttribute exchangeInfo, MsgNameAttribute msgInfo )
        {
            this.Exchange = exchangeInfo.Exchange;
            this.ExchangeType = exchangeInfo.Type;

            this.Name = msgInfo.Name;
            this.Queue = msgInfo.Queue;
            this.RoutingKey = msgInfo.RoutingKey;
        }

        public string Exchange { get; private set; }

        public ExchangeType ExchangeType { get; }

        public string Name { get; private set; }

        public string Queue { get; private set; }

        public string RoutingKey { get; private set; }
    }
}