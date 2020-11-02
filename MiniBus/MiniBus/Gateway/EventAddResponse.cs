using System;
using PocketTlv;

namespace MiniBus.ClientApi.Gateway
{
    public class EventAddResponse : ITlvContract
    {
        public EventAddResponse()
        { }

        public EventAddResponse( string requestId )
        {
            if( string.IsNullOrEmpty( requestId ) )
            {
                throw new ArgumentException( $"'{nameof( requestId )}' cannot be null or empty", nameof( requestId ) );
            }

            this.RequestId = requestId;
        }

        public string RequestId { get; set; }

        // ----- ITlvContract Implementation -----

        int ITlvContract.ContractId => GatewayTlvs.EventAddResponse;

        void ITlvContract.Parse( ITlvParseContext parse )
        {
            this.RequestId = parse.Tag<StringTag>( 0 );
        }

        void ITlvContract.Save( ITlvSaveContext save )
        {
            save.Tag( 0, new StringTag( this.RequestId ) );
        }
    }
}