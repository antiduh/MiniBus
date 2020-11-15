using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;

namespace MiniBus.Services
{
    public class ServerEnvelope
    {
        public string CorrelationId { get; set; }

        public string SendRepliesTo { get; set; }

        public string ClientId { get; set; }

        public static ServerEnvelope FromRabbit( IBasicProperties props )
        {
            var env = new ServerEnvelope();

            if( props.CorrelationId != null )
            {
                env.CorrelationId = props.CorrelationId;
            }

            if( props.ReplyTo != null )
            {
                env.SendRepliesTo = props.ReplyTo;
            }

            IDictionary<string, object> headers = props.Headers;

            if( headers != null && headers.ContainsKey( "clientId" ) )
            {
                env.ClientId = Encoding.UTF8.GetString( (byte[])headers["clientId"] );
            }

            return env;
        }

        public void ToRabbit( IBasicProperties props )
        {
            // Don't assign values to properties if they're null. Rabbit pays attention to whether or
            // not a field was assigned. If it's been assigned, it'll try to serialize it, causing it
            // to serialize a null field.
            if( this.CorrelationId != null )
            {
                props.CorrelationId = this.CorrelationId;
            }

            if( this.SendRepliesTo != null )
            {
                props.ReplyTo = this.SendRepliesTo;
            }

            if( this.ClientId != null )
            {
                if( props.Headers == null )
                {
                    props.Headers = new Dictionary<string, object>();
                }

                props.Headers.Add( "clientId", this.ClientId );
            }
        }
    }
}