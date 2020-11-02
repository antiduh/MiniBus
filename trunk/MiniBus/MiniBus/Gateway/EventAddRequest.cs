using System;
using PocketTlv;

namespace MiniBus.ClientApi.Gateway
{
    /// <summary>
    /// Internal message sent by the <see cref="GatewayClientBus"/> to request subscription to an event.
    /// </summary>
    public class EventAddRequest : IMessage
    {
        public EventAddRequest()
        { }

        public EventAddRequest( string exchange, string name )
        {
            if( string.IsNullOrEmpty( exchange ) )
            {
                throw new ArgumentException( $"'{nameof( exchange )}' cannot be null or empty", nameof( exchange ) );
            }

            if( string.IsNullOrEmpty( name ) )
            {
                throw new ArgumentException( $"'{nameof( name )}' cannot be null or empty", nameof( name ) );
            }

            this.Exchange = exchange;
            this.EventName = name;

            this.RequestID = Guid.NewGuid().ToString( "B" );
        }

        /// <summary>
        /// Gets or sets the name of the exchange that the event is published to.
        /// </summary>
        public string Exchange { get; set; }

        /// <summary>
        /// Gets or sets the name of the event.
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// A correlation ID that uniquely identifies this request, so that multiple simultaneous
        /// requests can be discerned.
        /// </summary>
        public string RequestID { get; private set; }

        // ----- ITlvContract Implementation -----

        int ITlvContract.ContractId => GatewayTlvs.EventAddRequest;

        void ITlvContract.Parse( ITlvParseContext parse )
        {
            this.Exchange = parse.Tag<StringTag>( 0 );
            this.EventName = parse.Tag<StringTag>( 1 );
            this.RequestID = parse.Tag<StringTag>( 2 );
        }

        void ITlvContract.Save( ITlvSaveContext save )
        {
            save.Tag( 0, new StringTag( this.Exchange ) );
            save.Tag( 1, new StringTag( this.EventName ) );
            save.Tag( 2, new StringTag( this.RequestID ) );
        }
    }
}