using System;
using System.Runtime.Serialization;

namespace MiniBus
{
    /// <summary>
    /// Occurs when a timeout has occurred while waiting to recieve a message.
    /// </summary>
    public class TimeoutException : DeliveryException
    {
        public TimeoutException() : this( "A timeout occurred while waiting for a message." )
        {
        }

        public TimeoutException( string message ) : base( message )
        {
        }

        public TimeoutException( string message, Exception innerException ) : base( message, innerException )
        {
        }

        protected TimeoutException( SerializationInfo info, StreamingContext context ) : base( info, context )
        {
        }
    }
}