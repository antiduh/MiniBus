using System;
using System.Collections.Generic;
using System.Reflection;

namespace MiniBus
{
    public class MessageDefRegistry
    {
        private Dictionary<Type, MessageDef> messageMap;

        public MessageDefRegistry()
        {
            this.messageMap = new Dictionary<Type, MessageDef>();
        }

        public MessageDef Get<T>() where T : IMessage
        {
            return AddFromType( typeof( T ) );
        }

        public MessageDef Get( IMessage msg )
        {
            // Gotta use the dynamic type lookup here.
            Type type = msg.GetType();

            return AddFromType( type );
        }

        private MessageDef AddFromType( Type type )
        {
            MessageDef def;

            if( this.messageMap.TryGetValue( type, out def ) == false )
            {
                var msgName = type.GetCustomAttribute<MsgNameAttribute>( false );
                var exchangeDef = type.GetCustomAttribute<ExchangeAttribute>( false );

                def = new MessageDef( exchangeDef, msgName );
                this.messageMap[type] = def;
            }

            return def;
        }
    }
}