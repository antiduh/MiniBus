using System.IO;
using System.Text;

namespace MiniBus
{
    // This sucks, but it's a demo.
    public static class Serializer
    {
        public static void ReadBody( byte[] body, out string msgName, out string payload )
        {
            var reader = new StreamReader( new MemoryStream( body ) );

            msgName = reader.ReadLine();
            payload = reader.ReadToEnd();
        }

        public static byte[] MakeBody( MessageDef msgDef, IMessage message )
        {
            string payload = message.Write();

            MemoryStream outStream = new MemoryStream();
            StreamWriter writer = new StreamWriter( outStream, Encoding.UTF8 );

            writer.WriteLine( msgDef.Name );
            writer.Write( payload );
            writer.Flush();

            return outStream.ToArray();
        }
    }
}