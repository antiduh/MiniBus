using System;

namespace MiniBus
{
    public class Hostname
    {
        public Hostname( string host, int port )
        {
            this.Host = host;
            this.Port = port;
        }

        public string Host { get; }

        public int Port { get; }
    }
}