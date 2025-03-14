using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityServer.Core.Networking.Clients;
using UnityServer.Tools;
using UnityServer.Tools.Types;

namespace UnityServer.Core.Networking
{
    public class RawClientManager
    {
        private static readonly Lazy<RawClientManager> _singleton = new(() => new());
        public static RawClientManager Singleton => _singleton.Value;

        public ThreadSafeList<RawClient> RawClientsConnected { get; private set; } = [];

        public void RegisterRawClient(RawClient rClient)
        {
            if (!RawClientsConnected.Contains(rClient))
            {
                RawClientsConnected.Add(rClient);
                rClient.Disconnected -= OnRawClientDisconnected;    // Prevent double subscriptions
                rClient.Disconnected += OnRawClientDisconnected;
            }
        }

        public void UnregisterRawClient(RawClient rClient)
        {
            if (RawClientsConnected.Remove(rClient))
            {
                rClient.Disconnected -= OnRawClientDisconnected;
            }
        }

        // 🔹 This will be called whenever a RawClient disconnects
        private void OnRawClientDisconnected(object? sender, EventArgs e)
        {
            if (sender is RawClient rClient)
            {
                Scribe.Write($"{rClient.Name} has disconnected. Removing from client list.");
                UnregisterRawClient(rClient);
            }
        }

        public void PrintRawClientsConnected()
        {
            StringBuilder whoList = new();

            var clients = RawClientsConnected.ToList(); // Create a copy locally

            whoList.AppendLine($"Total Users Online: {clients.Count}");

            foreach (RawClient rClient in clients)
            {
                whoList.AppendLine($"{rClient.Name} from {rClient.Address} -- {rClient.DnsAddress}");
            }

            Scribe.Write(whoList.ToString());
        }

        public async Task BroadcastMessage(string message)
        {
            var clients = RawClientsConnected.ToList();

            foreach (RawClient client in clients)
            {
                if (client.IsConnected)
                {
                    await client.WriteLineAsync(message);
                }
            }
        }
    }
}
