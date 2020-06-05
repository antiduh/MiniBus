using System;
using System.Windows.Forms;
using RabbitMQ.Client;

namespace Echo.Service
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Console.WriteLine( "Service starting." );

            ConnectionFactory rabbitConnFactory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "antiduh",
                Password = "passwordpassw",
                VirtualHost = "sdv-test",
            };

            IConnection rabbitCon = rabbitConnFactory.CreateConnection();

            IModel channel = rabbitCon.CreateModel();

            var service = new EchoService();

            service.Connect( channel );

            MessageBox.Show( "Running" );

            channel.Dispose();
            rabbitCon.Dispose();

            Console.WriteLine( "Service exiting." );
        }
    }
}