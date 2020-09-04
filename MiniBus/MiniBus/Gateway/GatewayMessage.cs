using System;
using MiniBus.ClientApi.Gateway;
using PocketTlv;

namespace MiniBus.Gateway
{
    public class GatewayMessage : ITlvContract
    {
        public string Exchange { get; set; }

        public string RoutingKey { get; set; }

        public string MessageName { get; set; }

        public string CorrelationId { get; set; }

        // Why did I have a Guid?
        //public string Guid { get; set; }

        public int ContractId => GatewayTlvs.GatewayMessage;

        public ITlvContract Message { get; set; }

        void ITlvContract.Save( ITlvSaveContext save )
        {
            save.Tag( 0, new StringTag( this.Exchange ) );
            save.Tag( 1, new StringTag( this.RoutingKey ) );
            save.Tag( 2, new StringTag( this.MessageName ) );

            if( this.CorrelationId != null )
            {
                save.Tag( 3, new StringTag( this.CorrelationId ) );
            }

            save.Contract( 4, this.Message );
        }

        void ITlvContract.Parse( ITlvParseContext parse )
        {
            this.Exchange = parse.Tag<StringTag>( 0 );
            this.RoutingKey = parse.Tag<StringTag>( 1 );
            this.MessageName = parse.Tag<StringTag>( 2 );

            if( parse.TryTag<StringTag>( 3, out StringTag corrIdTag ) )
            {
                this.CorrelationId = corrIdTag;
            }

            this.Message = parse.Contract( 4 );
        }
    }
}