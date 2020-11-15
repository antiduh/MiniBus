using System;
using RabbitMQ.Client;

namespace MiniBus.Services
{
    public class ModelWithRecovery
    {
        public ModelWithRecovery( IModel model, IAutorecoveringConnection conn )
        {
            this.Model = model;

            conn.RecoverySucceeded += Conn_RecoverySucceeded;
        }

        public IModel Model { get; private set; }

        public event EventHandler<EventArgs> RecoverySucceeded;

        private void Conn_RecoverySucceeded( object sender, EventArgs e )
        {
            this.RecoverySucceeded?.Invoke( sender, e );
        }
    }
}