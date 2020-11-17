using System;
using System.IO;
using System.Threading;
using PocketTlv;

namespace MiniBus.Gateway
{
    /// <summary>
    /// Reads and writes Minibus messages from a stream.
    /// </summary>
    public class TlvClient
    {
        private readonly Stream client;

        private Thread receiveThread;

        private TlvStreamReader tlvReader;
        private TlvStreamWriter tlvWriter;

        private bool started;

        public TlvClient( Stream client )
        {
            this.client = client;

            this.started = false;

            this.tlvReader = new TlvStreamReader( client );
            this.tlvWriter = new TlvStreamWriter( client );
        }

        public event Action<ITlvContract> Received;

        public void Start()
        {
            if( this.started )
            {
                throw new InvalidOperationException( "Already started." );
            }

            this.receiveThread = new Thread( ReceiveThreadEntry );
            this.receiveThread.Start();
        }

        public void Stop()
        {
            if( this.started == false )
            {
                return;
            }

            this.client.Close();
            this.client.Dispose();

            this.receiveThread.Join();
            this.started = false;
        }

        public void SendMessage( ITlvContract msg )
        {
            if( msg is null )
            {
                throw new ArgumentNullException( nameof( msg ) );
            }

            lock( this.tlvWriter )
            {
                this.tlvWriter.Write( msg );
            }
        }

        private void ReceiveThreadEntry()
        {
            ITlvContract contract;

            while( true )
            {
                contract = this.tlvReader.ReadContract();

                if( contract == null )
                {
                    break;
                }

                this.Received?.Invoke( contract );
            }
        }

        public void Register<T>() where T : ITlvContract, new()
        {
            if( this.started )
            {
                throw new InvalidOperationException( 
                    "In order to prevent race conditions, the client does not allow contract " +
                    "registrations after it has been started." 
                );
            }

            this.tlvReader.RegisterContract<T>();
        }
    }
}