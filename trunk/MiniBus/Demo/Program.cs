using System;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Echo.Client;
using Echo.Client.Messages;
using Echo.Service;
using Gateway.Service;
using MiniBus;
using MiniBus.ClientApi;
using MiniBus.Gateway;
using MiniBus.Services;
using PocketTlv;
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
            //RabbitBusDemo();

            GatewayDemo();
        }

        private static void GatewayDemo()
        {
            using( var conn = new RabbitConn() )
            {
                GatewayService gatewayService = new GatewayService( 10001 );
                gatewayService.Connect( conn.Connect() );

                GatewayClientBus clientBus = new GatewayClientBus( "localhost", 10001 );
                clientBus.Connect();

                var echoService = new EchoService( 0 );
                echoService.Connect( new RabbitServerBus( conn.Connect() ) );

                EchoClient echoClient = new EchoClient( clientBus );

                echoClient.DoEcho( "Hello" );
                Console.WriteLine( "Gateway demo complete." );

                Thread.Sleep( 10 * 1000 );
            }
        }

        private static void RabbitBusDemo()
        {
            using( var conn = new RabbitConn() )
            {
                var service = new EchoService( 0 );
                service.Connect( new RabbitServerBus( conn.Connect() ) );

                //var service2 = new EchoService( 1 );
                //service2.Connect( new RabbitServerBus( conn.Connect() ) );

                var client1 = new EchoClient( new RabbitClientBus( conn.Connect() ) );
                client1.CallOfTheVoid += x => Console.WriteLine( $"Client: Event '{x}'" );

                Console.WriteLine( "Demo starting." );

                for( int i = 0; i < 60; i++ )
                {
                    Thread.Sleep( 1000 );
                    client1.DoEcho( "Hello" );
                    Console.WriteLine( "EchoClient: Echo complete." );
                }
            }

            MessageBox.Show( "Done" );
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
                    AutomaticRecoveryEnabled = true,
                    RequestedHeartbeat = TimeSpan.FromSeconds( 60 ),
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