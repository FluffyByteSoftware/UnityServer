using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UnityServer.Core.Networking.Clients
{
    public interface IClient
    {
        string Name { get; }
        int ID { get; }

        public bool IsConnected { get; }

        
        IPAddress Address { get; }
        string DnsAddress { get; }

        DateTime ConnectionStartTime { get; }
        DateTime LastUpdateTime { get; }

        public Task<string> ReadLineAsync();
        public Task WriteAsync(string message);
        public Task WriteLineAsync(string message);

        public void Disconnect();
        public bool SafePollClient();

    }
}
