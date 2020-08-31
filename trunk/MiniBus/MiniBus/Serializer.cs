using System.IO;
using System.Text;

namespace MiniBus
{
    // This sucks, but it's a demo.
    public static class Serializer
    {
        public static string ReadBody( byte[] body )
        {
            new MemoryStream( body );

            return "";
            //return reader.ReadToEnd();
        }

        public static byte[] MakeBody( IMessage message )
        {
            return null;
            //string payload = message.Write();

            //return Encoding.UTF8.GetBytes( payload );
        }
    }
}