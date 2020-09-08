using System;
using PocketTlv;

namespace MiniBus.ClientApi.Gateway
{
    /// <summary>
    /// Used by a client to check that the connection to the gateway itself is still alive.
    /// </summary>
    public class GatewayHeartbeatResponse : ITlvContract
    {
        public GatewayHeartbeatResponse()
        {
        }

        public int ContractId => GatewayTlvs.EchoResponse;

        public void Parse( ITlvParseContext parse )
        {
        }

        public void Save( ITlvSaveContext save )
        {
        }
    }
}