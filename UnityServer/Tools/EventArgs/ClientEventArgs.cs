using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityServer.Core.Networking.Clients;

namespace UnityServer.Tools.EventArgs
{
    public class ClientEventArgs(RawClient rClient)
    {
        public RawClient Client { get; } = rClient;

        public DateTime ConnectionTime { get; } = DateTime.UtcNow;

        
    }
}
