using System;

namespace MiniBus
{
    public class CorrId
    {
        public static string Create()
        {
            return Guid.NewGuid().ToString( "B" );
        }
    }
}
