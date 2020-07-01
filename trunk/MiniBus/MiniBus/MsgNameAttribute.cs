using System;

namespace MiniBus
{
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
    public class MsgNameAttribute : Attribute
    {
        public MsgNameAttribute( string name, string exchange )
        {
            this.Name = name;
            this.Exchange = exchange;
        }

        public string Name { get; private set; }

        public string Exchange { get; private set; }
    }
}