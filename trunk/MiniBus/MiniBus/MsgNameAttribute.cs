using System;
using System.Text.RegularExpressions;

namespace MiniBus
{
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
    public class MsgNameAttribute : Attribute
    {
        private static readonly Regex parser = new Regex( @"(?<prefix>.+)\.(?<name>[^.]+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture );

        public MsgNameAttribute( string name )
        {
            this.Name = name;

            Match match = parser.Match( name );

            if( match.Success == false )
            {
                throw new ArgumentException( "Not a valid message name: " + name );
            }

            this.Queue = match.Groups["prefix"].Value;
            this.RoutingKey = this.Queue + ".#";
        }

        public string Name { get; private set; }

        public string RoutingKey { get; private set; }

        public string Queue { get; private set; }
    }
}