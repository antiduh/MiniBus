using System;

namespace ClientDemo.Messages
{
    public static class EchoTlvs
    {
        public const int EchoBase = 100;

        public const int EchoRequest = EchoBase + 1;

        public const int EchoReply = EchoBase + 2;

        public const int VoidCalling = EchoBase + 3;
    }
}