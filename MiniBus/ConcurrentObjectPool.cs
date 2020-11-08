using System;
using System.Collections.Concurrent;

namespace MiniBus
{
    public class ConcurrentObjectPool<T>
    {
        private readonly ConcurrentBag<T> objects;

        private readonly Func<T> objectGenerator;

        public ConcurrentObjectPool( Func<T> objectGenerator )
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