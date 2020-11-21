using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using PocketTlv;

namespace MiniBus.Gateway
{
    public class ClientTlvStream : IDisposable
    {
        private readonly GatewayConnectionProvider connProv;
        private readonly ContractRegistry contractReg;

        private bool connected;

        private object connectionLock;

        private ManualResetEventSlim connectedWaiter;

        private Thread connectionThread;

        private TlvStreamWriter tlvWriter;
        private TlvStreamReader tlvReader;

        private TcpClient tcpClient;

        private Stream tcpStream;

        private bool disposed;

        public ClientTlvStream( GatewayConnectionProvider connProv, ContractRegistry contractReg )
        {
            this.connProv = connProv;
            this.contractReg = contractReg;

            this.disposed = false;
            this.connectionLock = new object();

            this.connectedWaiter = new ManualResetEventSlim( false );

            this.tlvReader = new TlvStreamReader( this.contractReg );
            this.tlvWriter = new TlvStreamWriter();
        }

        public void Connect()
        {
            StartReconnect();

            this.connectedWaiter.Wait();
        }

        public void Disconnect()
        {
            lock( this.connectionLock )
            {
                this.connectedWaiter.Reset();

                this.connected = false;

                this.tlvReader?.Disconnect();
                this.tlvWriter?.Disconnect();

                this.tcpStream?.Close();
                this.tcpStream = null;

                this.tcpClient?.Dispose();
                this.tcpClient = null;
            }
        }

        public void Dispose()
        {
            Disconnect();

            this.disposed = true;

            this.tlvWriter = null;
            this.tlvReader = null;

            this.connectedWaiter?.Dispose();
            this.connectedWaiter = null;

            var threadCopy = this.connectionThread;

            if( threadCopy != null )
            {
                threadCopy?.Interrupt();
                this.connectionThread = null;
            }
        }

        public void Write( ITlvContract contract )
        {
            lock( this.connectionLock )
            {
                if( this.connected == false )
                {
                    throw new ChannelDownException();
                }
            }

            try
            {
                lock( this.tlvWriter )
                {
                    this.tlvWriter.Write( contract );
                }
            }
            catch( IOException )
            {
                Disconnect();
                StartReconnect();

                throw new ChannelDownException();
            }
        }

        public ITlvContract Read()
        {
            lock( this.connectionLock )
            {
                if( this.connected == false )
                {
                    throw new ChannelDownException();
                }
            }

            try
            {
                lock( this.tlvReader )
                {
                    return this.tlvReader.ReadContract();
                }
            }
            catch( IOException )
            {
                Disconnect();
                StartReconnect();

                throw new ChannelDownException();
            }
        }

        private void StartReconnect()
        {
            lock( this.connectionLock )
            {
                if( this.connectionThread != null )
                {
                    // Already running.
                    return;
                }
                else
                {
                    this.connectionThread = new Thread( ReconnectLoop );
                    this.connectionThread.Start();
                }
            }
        }

        private void ReconnectLoop()
        {
            while( this.disposed == false )
            {
                Hostname host = this.connProv.GetConnection();

                try
                {
                    this.tcpClient = new TcpClient( host.Host, host.Port );

                    this.tcpStream = this.tcpClient.GetStream();

                    this.tlvReader.Connect( this.tcpStream );
                    this.tlvWriter.Connect( this.tcpStream );

                    break;
                }
                catch( IOException )
                {
                    Thread.Sleep( 1000 );
                }
            }

            lock( this.connectionLock )
            {
                this.connectionThread = null;
                this.connected = true;
                this.connectedWaiter.Set();
            }
        }
    }
}