using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Echo.Client;
using Echo.Service;
using MiniBus.Services;
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

            var service = new EchoService( 0 );
            service.Connect( new RabbitServerBus( conn.Connect() ) );

            var service2 = new EchoService( 1 );
            service2.Connect( new RabbitServerBus( conn.Connect() ) );

            var client1 = new EchoClient( new RabbitClientBus( conn.Connect() ) );
            var client2 = new EchoClient( new RabbitClientBus( conn.Connect() ) );

            Task task1 = Task.Run( () =>
            {
                for( int i = 0; i < 5000; i++ )
                {
                    client1.DoEcho( "Hello" );
                }
            } );

            Task task2 = Task.Run( () =>
            {
                for( int i = 0; i < 5000; i++ )
                {
                    client2.DoEcho( "Hello" );
                }
            } );

            task1.Wait();
            task2.Wait();

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