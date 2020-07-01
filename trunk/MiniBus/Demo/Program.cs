﻿using System;
using System.Net;
using System.Threading;
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
            RunDemo();
        }

        private static void RunDemo()
        {
            using( var conn = new RabbitConn() )
            {
                var service = new EchoService( 0 );
                service.Connect( new RabbitServerBus( conn.Connect() ) );

                //var service2 = new EchoService( 1 );
                //service2.Connect( new RabbitServerBus( conn.Connect() ) );

                var client1 = new EchoClient( new RabbitClientBus( conn.Connect() ) );
                client1.CallOfTheVoid += x => Console.WriteLine( "Event: " + x );

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