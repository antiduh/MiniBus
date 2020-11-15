using System;
using PocketTlv;

namespace MiniBus.Gateway
{
    /// <summary>
    /// Represents a message being sent from a client to the gateway service.
    /// </summary>
    public class GatewayRequestMsg : ITlvContract
    {
        public string CorrelationId { get; set; }

        public string Exchange { get; set; }

        public string RoutingKey { get; set; }

        public string MessageName { get; set; }

        public ITlvContract Message { get; set; }

        int ITlvContract.ContractId => GatewayTlvs.InboundMsg;

        void ITlvContract.Parse( ITlvParseContext parse )
        {
            if( parse.TryTag<StringTag>( 0, out StringTag corrTag ) )
            {
                this.CorrelationId = corrTag;
            }

            this.Exchange = parse.Tag<StringTag>( 1 );
            this.RoutingKey = parse.Tag<StringTag>( 2 );
            this.MessageName = parse.Tag<StringTag>( 3 );
            this.Message = parse.Contract( 4 );
        }

        void ITlvContract.Save( ITlvSaveContext save )
        {
            if( this.CorrelationId != null )
            {
                save.Tag( 0, new StringTag( this.CorrelationId ) );
            }

            save.Tag( 1, new StringTag( this.Exchange ) );
            save.Tag( 2, new StringTag( this.RoutingKey ) );
            save.Tag( 3, new StringTag( this.MessageName ) );
            save.Contract( 4, this.Message );
        }
    }
}