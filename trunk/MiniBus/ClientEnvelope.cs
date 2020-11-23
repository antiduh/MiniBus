namespace MiniBus
{
    public class ClientEnvelope
    {
        public ClientEnvelope()
        {
        }

        public ClientEnvelope( string correlationId, string sendRepliesTo )
        {
            this.CorrelationId = correlationId;
            this.SendRepliesTo = sendRepliesTo;
        }

        public string CorrelationId { get; set; }

        public string SendRepliesTo { get; set; }
    }
}