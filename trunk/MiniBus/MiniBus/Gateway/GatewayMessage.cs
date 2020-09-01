using System;
using PocketTlv;

namespace MiniBus.Gateway
{
    public class GatewayMessage : ITlvContract
    {
        public string Exchange { get; set; }

        public string RoutingKey { get; set; }

        public string MessageName { get; set; }

        // Why did I have a Guid?
        //public string Guid { get; set; }

        public int ContractId => 0;

        public ITlvContract Message { get; set; }

        void ITlvContract.Save( ITlvSaveContext save )
        {
            save.Tag( 0, new StringTag( this.Exchange ) );
            save.Tag( 1, new StringTag( this.RoutingKey ) );
            save.Tag( 2, new StringTag( this.MessageName ) );
            save.Contract( 3, this.Message );
        }

        void ITlvContract.Parse( ITlvParseContext parse )
        {
            this.Exchange = parse.Tag<StringTag>( 0 );
            this.RoutingKey = parse.Tag<StringTag>( 1 );
            this.MessageName = parse.Tag<StringTag>( 2 );
            this.Message = parse.Contract( 3 );
        }
    }
}