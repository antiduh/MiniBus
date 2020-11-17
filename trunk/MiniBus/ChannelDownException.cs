using System;
using System.Runtime.Serialization;

namespace MiniBus
{
    /// <summary>
    /// Occurs when an attempt is made to send a message into a channel that is currently disconnected.
    /// </summary>
    /// <remarks>
    /// Bus implementations may temporarily be disconnected if a remote server fails and the bus
    /// needs to reconnect or connect to a different remote server. If messages are sent while the
    /// bus is reconnecting, <see cref="ChannelDownException"/> may occur.
    /// </remarks>
    public class ChannelDownException : DeliveryException
    {
        public ChannelDownException()
        {
        }

        public ChannelDownException( string message ) : base( message )
        {
        }

        public ChannelDownException( string message, Exception innerException ) : base( message, innerException )
        {
        }

        protected ChannelDownException( SerializationInfo info, StreamingContext context ) : base( info, context )
        {
        }
    }
}