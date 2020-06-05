using System;

namespace MiniBus
{
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
    public class ExchangeAttribute : Attribute
    {
        public ExchangeAttribute( ExchangeType type, string exchange )
        {
            Type = type;
            Exchange = exchange;
        }

        public ExchangeType Type { get; private set; }

        public string Exchange { get; private set; }
    }
}