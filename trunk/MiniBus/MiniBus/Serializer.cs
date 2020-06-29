using System.IO;
using System.Text;

namespace MiniBus
{
    // This sucks, but it's a demo.
    public static class Serializer
    {
        public static string ReadBody( byte[] body )
        {
            var reader = new StreamReader( new MemoryStream( body ) );

            return reader.ReadToEnd();
        }

        public static byte[] MakeBody( IMessage message )
        {
            string payload = message.Write();

            return Encoding.UTF8.GetBytes( payload );
        }
    }
}