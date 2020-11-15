using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniBus.ClientApi
{
    public class CorrId
    {
        public static string Create()
        {
            return Guid.NewGuid().ToString( "B" );
        }
    }
}
