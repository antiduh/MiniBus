using System;

namespace MiniBus
{
    /// <summary>
    /// Stores information used for identifying and routing messages through RabbitMQ.
    /// </summary>
    public class MessageDef
    {
        /// <summary>
        /// Initializes a new instance of the MessageDef class.
        /// </summary>
        /// <param name="name">The canonical name of the message.</param>
        /// <param name="exchange">The name of the exchange that the message should be routed to.</param>
        public MessageDef( string name, string exchange )
        {
            this.Name = name;
            this.Exchange = exchange;
        }

        /// <summary>
        /// Gets the canonical name of the message.
        /// </summary>
        /// <remarks>
        /// The canonical name of the message is the name used to identify the message while in
        /// transit. This is separate from the name of the class(s) that may represent the message.
        ///
        /// Clients and services must agree on the names of messages in order for the system to
        /// function consistently.
        /// </remarks>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the name of the exchange that the message should be routed to.
        /// </summary>
        public string Exchange { get; private set; }
    }
}