using System;
using System.Windows.Forms;
using Echo.Client;
using Echo.Service;
using RabbitMQ.Client;

namespace Demo
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Console.WriteLine( "Demo starting." );

            var conn = new RabbitConn();
            IModel channel = conn.Connect();

            var service = new EchoService();
            service.Connect( channel );


            var client = new EchoClient( new RabbitClientBus( channel ) );
            client.DoEcho( "Hello" );

            MessageBox.Show( "Done" );

            conn.Dispose();

            Console.WriteLine( "Demo exiting." );
        }

        private class RabbitConn : IDisposable
        {
            private ConnectionFactory rabbitConnFactory;

            private IConnection connection;

            private IModel channel;

            public RabbitConn()
            {
                this.rabbitConnFactory = new ConnectionFactory()
                {
                    HostName = "localhost",
                    UserName = "guest",
                    Password = "guest",
                    VirtualHost = "sdv-test",
                };
            }

            public IModel Connect()
            {
                this.connection = rabbitConnFactory.CreateConnection();
                this.channel = this.connection.CreateModel();

                return this.channel;
            }

            public void Dispose()
            {
                this.channel.Dispose();
                this.connection.Dispose();
            }
        }
    }
}