using System;

namespace MiniBus
{
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
    public class MsgNameAttribute : Attribute
    {
        public MsgNameAttribute( string name )
        {
            this.Name = name;
        }

        public string Name { get; private set; }
    }
}