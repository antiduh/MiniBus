using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using PocketTlv;

namespace MiniBus.Gateway
{
    public class GatewayConnection : IDisposable
    {
        private readonly HostList hostList;
        private readonly ContractRegistry contractReg;

        private bool connected;

        private bool reconnecting;

        private object connectionLock;

        private object writeLock;

        private object readLock;

        private ManualResetEventSlim connectedWaiter;

        private GatewayConnectionLoop connectionLoop;

        private TlvStreamWriter tlvWriter;
        private TlvStreamReader tlvReader;

        private TcpClient tcpClient;

        private Stream tcpStream;

        public GatewayConnection( HostList hostList, ContractRegistry contractReg )
        {
            this.hostList = hostList;
            this.contractReg = contractReg;

            this.connected = false;
            this.reconnecting = false;

            this.connectionLock = new object();
            this.writeLock = new object();
            this.readLock = new object();

            this.connectedWaiter = new ManualResetEventSlim( false );
        }

        /// <summary>
        /// Occurs when the connection to the gateway has been restored after a connection loss event.
        /// </summary>
        public event Action ConnectionRestored;

        /// <summary>
        /// Occurs when the connection to the gateway has been lost.
        /// </summary>
        public event Action ConnectionLost;

        public void Connect()
        {
            lock( this.connectionLock )
            {
                StartReconnect();
            }

            this.connectedWaiter.Wait();
        }

        public void Disconnect()
        {
            lock( this.connectionLock )
            {
                DisconnectInternal();
            }
        }

        public void WaitConnected()
        {
            this.connectedWaiter.Wait();
        }

        public void Dispose()
        {
            Disconnect();

            this.connectedWaiter?.Dispose();
            this.connectedWaiter = null;
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
                lock( this.writeLock )
                {
                    this.tlvWriter.Write( contract );
                }
            }
            catch( IOException )
            {
                ConnectionFailure();
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
                lock( this.readLock )
                {
                    return this.tlvReader.ReadContract();
                }
            }
            catch( IOException )
            {
                ConnectionFailure();
                throw new ChannelDownException();
            }
        }

        private void ConnectionFailure()
        {
            this.ConnectionLost?.Invoke();

            lock( this.connectionLock )
            {
                this.reconnecting = true;

                DisconnectInternal();
                StartReconnect();
            }
        }

        private void StartReconnect()
        {
            if( this.connectionLoop != null )
            {
                // Already running.
                return;
            }
            else
            {
                this.connectionLoop = new GatewayConnectionLoop( 
                    this.hostList, 
                    TimeSpan.FromSeconds( 1 ),
                    ConnectionLoop_Completed 
                );
            }
        }

        private void DisconnectInternal()
        {
            if( this.connected == false )
            {
                return;
            }

            this.connectionLoop?.Cancel();
            this.connectionLoop = null;

            this.connectedWaiter.Reset();

            this.tlvReader = null;
            this.tlvWriter = null;

            this.tcpStream?.Close();
            this.tcpStream = null;

            this.tcpClient?.Dispose();
            this.tcpClient = null;

            // Finally, mark that we're disconnected.
            this.connected = false;
        }

        private void ConnectionLoop_Completed( TcpClient newClient )
        {
            lock( this.connectionLock )
            {
                this.tcpClient = newClient;
                this.tcpStream = this.tcpClient.GetStream();
                
                this.tlvReader = new TlvStreamReader( this.tcpStream, this.contractReg );
                this.tlvWriter = new TlvStreamWriter( this.tcpStream );

                this.connectionLoop = null;
                this.connected = true;

                this.connectedWaiter.Set();

                if( this.reconnecting )
                {
                    this.reconnecting = false;
                    this.ConnectionRestored?.Invoke();
                }
            }
        }
    }
}