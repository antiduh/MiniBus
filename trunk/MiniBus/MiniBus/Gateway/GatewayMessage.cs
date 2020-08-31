using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PocketTLV;
using PocketTLV.Primitives;

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

        void ITlvContract.Save( ITlvSaveContext saveContext )
        {
            saveContext.Save( 0, new StringTag( this.Exchange ) );
            saveContext.Save( 1, new StringTag( this.RoutingKey ) );
            saveContext.Save( 2, new StringTag( this.MessageName ) );
            saveContext.Save( 3, this.Message );
        }

        void ITlvContract.Parse( ITlvParseContext parseContext )
        {
            this.Exchange = parseContext.ParseTag<StringTag>( 0 );
            this.RoutingKey = parseContext.ParseTag<StringTag>( 1 );
            this.MessageName = parseContext.ParseTag<StringTag>( 2 );
            this.Message = parseContext.ParseSubContract( 3 );
        }

    }
}