using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace MiniBus.Gateway
{
    /// <summary>
    /// Stores the list of known hosts.
    /// </summary>
    public class HostList
    {
        private List<Hostname> hosts;

        private Random rand;

        public HostList()
        {
            this.hosts = new List<Hostname>();
            this.rand = new Random();
        }

        public void AddHost( Hostname host )
        {
            lock( this.hosts )
            {
                this.hosts.Add( host );
            }
        }

        public void TemporarilySupress( Hostname host )
        {
            throw new NotImplementedException();
        }

        public Hostname GetConnection()
        {
            lock( this.hosts )
            {
                int index = rand.Next( 0, this.hosts.Count );

                return this.hosts[index];
            }
        }
    }
}