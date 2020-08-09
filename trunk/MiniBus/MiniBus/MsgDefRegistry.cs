using System;
using System.Collections.Generic;
using System.Reflection;

namespace MiniBus
{
    /// <summary>
    /// Caches message definitions for message objects.
    /// </summary>
    /// <remarks>
    /// Message definitions are used by both clients and services to define how messages are to be
    /// exchanged through RabbitMQ. Since clients and services need to agree on how to exchange
    /// messages, the routing specifics are defined directly on the message class itself using <see
    /// cref="MsgNameAttribute"/> attributes.
    ///
    /// This class handles performing the reflection as needed, and caching the reflection result
    /// for performance.
    /// </remarks>
    public class MsgDefRegistry
    {
        private Dictionary<Type, MessageDef> messageMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsgDefRegistry"/> class.
        /// </summary>
        public MsgDefRegistry()
        {
            this.messageMap = new Dictionary<Type, MessageDef>();
        }

        /// <summary>
        /// Get's the <see cref="MessageDef"/> from a message's type.
        /// </summary>
        /// <typeparam name="T">The message's type.</typeparam>
        /// <returns></returns>
        public MessageDef Get<T>() where T : IMessage
        {
            return AddFromType( typeof( T ) );
        }

        /// <summary>
        /// Gets the <see cref="MessageDef"/> from a message instance.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public MessageDef Get( IMessage msg )
        {
            return AddFromType( msg.GetType() );
        }

        /// <summary>
        /// Gets the <see cref="MessageDef"/> for the given type from the cache, loading the
        /// definition into cache if not found.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private MessageDef AddFromType( Type type )
        {
            MessageDef def;

            lock( this.messageMap )
            {

                if( this.messageMap.TryGetValue( type, out def ) == false )
                {
                    var msgName = type.GetCustomAttribute<MsgNameAttribute>( false );

                    def = new MessageDef( msgName.Name, msgName.Exchange );
                    this.messageMap[type] = def;
                }
            }

            return def;
        }
    }
}