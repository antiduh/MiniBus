using System;

namespace MiniBus
{
    public enum ExchangeType
    {
        Topic,
        Fanout
    }

    public static class ExchangeTypeEx
    {
        public static string ToText( this ExchangeType type )
        {
            switch( type )
            {
                case ExchangeType.Fanout: return "fanout";
                case ExchangeType.Topic: return "topic";
                default: throw new ArgumentOutOfRangeException( nameof( type ), "Unsupported value: " + type );
            }
        }
    }
}