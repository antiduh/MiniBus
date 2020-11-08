using System;

namespace MiniBus.ClientApi.Gateway
{
    public class GatewayTlvs
    {
        public const int TlvBase = 200;

        public const int EchoRequest = TlvBase + 1;

        public const int EchoResponse = TlvBase + 2;

        public const int InboundMsg = TlvBase + 3;

        public const int OutboundMsg = TlvBase + 4;
    }
}