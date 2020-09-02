using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniBus.ClientApi
{
    public class ObjectPool<T>
    {
        private readonly ConcurrentBag<T> objects;

        private readonly Func<T> objectGenerator;

        public ObjectPool( Func<T> objectGenerator )
        {
            if( objectGenerator == null )
            {
                throw new ArgumentNullException( nameof( objectGenerator ) );
            }

            this.objectGenerator = objectGenerator;

            this.objects = new ConcurrentBag<T>();
        }

        public T Get()
        {
            if( objects.TryTake( out T item ) )
            {
                return item;
            }

            return objectGenerator();
        }

        public void Return( T item )
        {
            objects.Add( item );
        }
    }
}
