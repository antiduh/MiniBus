﻿using System;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Echo.Client;
using Echo.Service;
using Gateway.Service;
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
            RabbitBusDemo();

            //TlvDemo();
        }

        public class EchoRequestTag : ITlvContract
        {
            public string Text { get; set; }

            public int ContractId => 1;

            void ITlvContract.Parse( ITlvParseContext parse )
            {
                this.Text = parse.Tag<StringTag>( 0 );
            }

            void ITlvContract.Save( ITlvSaveContext save )
            {
                save.Tag( 0, new StringTag( this.Text ) );
            }
        }

        private static void TlvDemo()
        {
            var frame = new GatewayMessage()
            {
                Exchange = "voren-core",
                RoutingKey = "voren.Echo",
                MessageName = "EchoRequest",
                //Guid = "123456",
                Message = new EchoRequestTag() { Text = "Hello world." }
            };

            GatewayService service = new GatewayService( 10001 );
            service.Start();

            TcpClient client = new TcpClient( "127.0.0.1", 10001 );

            TlvStreamWriter writer = new TlvStreamWriter( client.GetStream() );

            writer.Write( frame );

            Thread.Sleep( 10 * 1000 );
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