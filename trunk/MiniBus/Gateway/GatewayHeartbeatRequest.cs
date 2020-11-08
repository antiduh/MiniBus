using System;
using PocketTlv;

namespace MiniBus.ClientApi.Gateway
{
    /// <summary>
    /// Used by a client to check that the connection to the gateway itself is still alive.
    /// </summary>
    public class GatewayHeartbeatRequest : ITlvContract
    {
        public GatewayHeartbeatRequest()
        {
        }

        public int ContractId => GatewayTlvs.EchoRequest;

        public void Parse( ITlvParseContext parse )
        {
        }

        public void Save( ITlvSaveContext save )
        {
        }
    }
}