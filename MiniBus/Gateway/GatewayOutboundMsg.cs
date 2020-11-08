using System;
using PocketTlv;

namespace MiniBus.ClientApi.Gateway
{
    /// <summary>
    /// Represents a message being sent from the gateway service to a client.
    /// </summary>
    public class GatewayOutboundMsg : IMessage
    {
        public string CorrelationId { get; set; }

        public string SendRepliesTo { get; set; }

        public string MessageName { get; set; }

        public ITlvContract Message { get; set; }

        int ITlvContract.ContractId => GatewayTlvs.OutboundMsg;

        void ITlvContract.Parse( ITlvParseContext parse )
        {
            this.Message = parse.Contract( 0 );
            this.MessageName = parse.Tag<StringTag>( 1 );

            if( parse.TryTag<StringTag>( 2, out StringTag correlationTag ) )
            {
                this.CorrelationId = correlationTag;
            }

            if( parse.TryTag<StringTag>( 3, out StringTag sendRepliesTag ) )
            {
                this.SendRepliesTo = sendRepliesTag;
            }
        }

        void ITlvContract.Save( ITlvSaveContext save )
        {
            save.Contract( 0, this.Message );
            save.Tag( 1, new StringTag( this.MessageName ) );

            if( this.CorrelationId != null )
            {
                save.Tag( 2, new StringTag( this.CorrelationId ) );
            }

            if( this.SendRepliesTo != null )
            {
                save.Tag( 3, new StringTag( this.SendRepliesTo ) );
            }
        }
    }
}