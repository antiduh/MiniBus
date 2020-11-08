namespace MiniBus
{
    public class Envelope
    {
        /// <summary>
        /// Specifies a correlation ID to associate with the message. All replies to the message will be tagged with the same correlation ID.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Optionally specifies a specific routing key to direct message replies to; ignored by client transports.
        /// </summary>
        public string SendRepliesTo { get; set; }
    }
}