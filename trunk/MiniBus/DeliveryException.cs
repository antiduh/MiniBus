using System;
using System.Runtime.Serialization;

namespace MiniBus
{
    /// <summary>
    /// Represents errors that occur while trying to transmit and receive messages. <see
    /// cref="DeliveryException"/> is the base class for all MiniBus communication exceptions.
    /// </summary>
    public class DeliveryException : Exception
    {
        public DeliveryException()
        {
        }

        public DeliveryException( string message ) : base( message )
        {
        }

        public DeliveryException( string message, Exception innerException ) : base( message, innerException )
        {
        }

        protected DeliveryException( SerializationInfo info, StreamingContext context ) : base( info, context )
        {
        }
    }
}