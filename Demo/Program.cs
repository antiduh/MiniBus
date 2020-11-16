using System;
using System.Threading;
using System.Windows.Forms;
using Echo.Client;
using Echo.Service;
using Gateway.Service;
using MiniBus;
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
            RabbitBusDemo();

            //GatewayDemo();
        }

        private static void GatewayDemo()
        {
            using( var rabbitConnClient = new RabbitConn() )
            using( var rabbitConnServer = new RabbitConn() )
            {
                // |<------ Client Domain----->|<---------- Rabbit Domain ------------------------->|
                // |                           |                                                    |
                // EchoClient ---TCP---> GatewayService ---AMQP---> RabbitMQ Cluster ---> EchoService

                // -- Echo Service ---
                var echoService = new EchoService( 0 );
                echoService.Connect( new RabbitServerBus( rabbitConnClient.Connect() ) );

                // --- Gateway ---
                GatewayService gatewayService = new GatewayService( 10001 );
                gatewayService.Connect( rabbitConnServer.Connect() );

                GatewayClientBus clientBus = new GatewayClientBus( "localhost", 10001 );
                clientBus.Connect();

                // --- Echo Client ---
                EchoClient echoClient = new EchoClient( clientBus );

                for( int i = 0; i < 1000; i++ )
                {
                    echoClient.DoEcho( "Hello" );
                    Console.WriteLine( "EchoClient: Echo complete." );
                    Thread.Sleep( 1000 );
                }

                Console.WriteLine( "Gateway demo complete." );

                Thread.Sleep( 10 * 1000 );
            }
        }

        private static void RabbitBusDemo()
        {
            using( var rabbitConnClient = new RabbitConn() )
            using( var rabbitConnServer = new RabbitConn() )
            {
                var service = new EchoService( 0 );
                service.Connect( new RabbitServerBus( rabbitConnServer.Connect() ) );

                //var service2 = new EchoService( 1 );
                //service2.Connect( new RabbitServerBus( conn.Connect() ) );

                var client1 = new EchoClient( new RabbitClientBus( rabbitConnClient.Connect() ) );

                Console.WriteLine( "Demo starting." );

                for( int i = 0; i < 1000; i++ )
                {
                    client1.DoEcho( "Hello" );
                    Console.WriteLine( "EchoClient: Echo complete." );
                    Thread.Sleep( 100 );
                }
            }

            MessageBox.Show( "Done" );
            Console.WriteLine( "Demo exiting." );
        }

        private class RabbitConn : IDisposable
        {
            private ConnectionFactory rabbitConnFactory;

            private IConnection connection;

            public RabbitConn()
            {
                this.rabbitConnFactory = new ConnectionFactory()
                {
                    HostName = "127.0.0.1",
                    UserName = "guest",
                    Password = "guest",
                    VirtualHost = "sdv-test",
                    AutomaticRecoveryEnabled = true,
                    RequestedHeartbeat = TimeSpan.FromSeconds( 60 ),
                    NetworkRecoveryInterval = TimeSpan.FromSeconds( 1 )
                };

                this.connection = rabbitConnFactory.CreateConnection();
            }

            public ModelWithRecovery Connect()
            {
                return new ModelWithRecovery(
                    this.connection.CreateModel(),
                    (IAutorecoveringConnection)this.connection
                );
            }

            public void Dispose()
            {
                this.connection.Dispose();
            }
        }
    }
}