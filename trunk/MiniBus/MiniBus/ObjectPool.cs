using System;
using System.Collections.Generic;

namespace MiniBus
{
    public class ObjectPool<T>
    {
        private readonly Queue<T> objects;

        private readonly Func<T> objectGenerator;

        public ObjectPool( Func<T> objectGenerator )
        {
            if( objectGenerator == null )
            {
                throw new ArgumentNullException( nameof( objectGenerator ) );
            }

            this.objectGenerator = objectGenerator;

            this.objects = new Queue<T>();
        }

        public T Get()
        {
            if( objects.Count > 0 )
            {
                return objects.Dequeue();
            }

            return objectGenerator();
        }

        public void Return( T item )
        {
            objects.Enqueue( item );
        }
    }
}
