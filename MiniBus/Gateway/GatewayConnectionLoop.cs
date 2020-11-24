using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace MiniBus.Gateway
{
    public class GatewayConnectionLoop
    {
        private readonly HostList hostList;
        private readonly TimeSpan retryDelay;
        private Action<TcpClient> completedCallback;

        private Thread thread;

        private TcpClient tcpClient;

        public GatewayConnectionLoop( HostList hostList, TimeSpan retryDelay, Action<TcpClient> completedCallback )
        {
            this.hostList = hostList;
            this.retryDelay = retryDelay;
            this.completedCallback = completedCallback;
            this.thread = new Thread( ThreadEntry );
            this.thread.Start();
        }

        public void Cancel()
        {
            if( this.thread != null )
            {
                this.thread.Interrupt();
                this.thread = null;

                this.completedCallback = null;
            }
        }

        private void ThreadEntry()
        {
            try
            {
                ReconnectLoop();
            }
            catch( ThreadInterruptedException )
            {
                // The reconnection thread is being stopped.
            }
            finally
            {
                this.completedCallback = null;
            }
        }

        private void ReconnectLoop()
        {
            while( true )
            {
                Hostname host = this.hostList.GetConnection();

                try
                {
                    this.tcpClient = new TcpClient( host.Host, host.Port );

                    this.completedCallback?.Invoke( this.tcpClient );

                    return;
                }
                catch( SocketException )
                {
                    Thread.Sleep( this.retryDelay );
                }
                catch( IOException )
                {
                    Thread.Sleep( this.retryDelay );
                }
            }
        }
    }
}