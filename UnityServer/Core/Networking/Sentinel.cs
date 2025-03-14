using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityServer.Core.Networking.Clients;
using UnityServer.Tools;
using UnityServer.Tools.EventArgs;

namespace UnityServer.Core.Networking
{
    public class Sentinel
    {
        public bool IsRunning { get; private set; } = false; // Indicates if the server is running

        private readonly TcpListener _listener; // The TCP listener that handles incoming connections

        // Events for tracking client connections and disconnections
        public event EventHandler<ClientEventArgs>? NewRawClientJoined;
        public event EventHandler<ClientEventArgs>? RawClientDisconnected;

        public Sentinel()
        {
            _listener = new(IPAddress.Parse("10.0.0.84"), 9998); // Bind listener to specific IP and port
        }

        // Starts the Sentinel (server) if it is not already running
        public Task Start()
        {
            if (IsRunning)
            {
                Scribe.Write("Sentinel is already running.");
                return Task.CompletedTask;
            }

            try
            {
                IsRunning = true;
                Scribe.Write("Starting TCP Listener...");
                _listener.Start(); // Start listening for client connections
                Task.Run(ListenLoop); // Run the listener loop asynchronously

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
                return Task.FromException(ex);
            }
        }

        // Stops the Sentinel (server) and disconnects all clients
        public async Task Stop()
        {
            if (!IsRunning) return;

            try
            {
                IsRunning = false;
                Scribe.Write("Stopping TCP Listener...");

                _listener.Stop(); // Properly stop the listener

                // Ensure all connected clients are forcibly disconnected
                foreach (var client in RawClientManager.Singleton.GetAllClients())
                {
                    client.Disconnect();
                }

                await RawClientManager.Singleton.BroadcastMessage("Shutdown immediately!");
                Scribe.Write("Server stopped.");
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }
        }

        // Listens for new client connections in an asynchronous loop
        private async Task ListenLoop()
        {
            try
            {
                while (IsRunning)
                {
                    try
                    {
                        TcpClient newConnection = await _listener.AcceptTcpClientAsync();

                        // Prevent new connections if the server is stopping
                        if (!IsRunning)
                        {
                            newConnection.Close();
                            return;
                        }

                        RawClient newClient = new(newConnection);
                        Scribe.Write($"New Client Joined :: {newClient.Name} from {newClient.Address}");

                        // Fire event to notify external components of a new client
                        NewRawClientJoined?.Invoke(this, new ClientEventArgs(newClient));

                        _ = HandleNewClient(newClient); // Start handling the client asynchronously
                    }
                    catch (ObjectDisposedException)
                    {
                        Scribe.Write("TcpListener was disposed, stopping listen loop.");
                    }
                    catch (SocketException) when (!IsRunning)
                    {
                        Scribe.Write("TcpListener stopped, ignoring exception.");
                    }
                    catch (Exception ex)
                    {
                        Scribe.Error(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }
        }

        // Handles communication with a connected client
        private async Task HandleNewClient(RawClient newRawClient)
        {
            try
            {
                while (IsRunning && newRawClient.IsConnected)
                {
                    string response = await newRawClient.ReadLineAsync();
                    if (response == null) break; // Handle disconnection

                    Scribe.Write($"Response from {newRawClient.Name}: {response}");
                    await newRawClient.WriteLineAsync($"Response received: {response}");
                }
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }
            finally
            {
                newRawClient.Disconnect(); // Ensure cleanup
                Scribe.Write($"Client {newRawClient.Name} disconnected.");

                // Fire event to notify external components of client disconnection
                RawClientDisconnected?.Invoke(this, new ClientEventArgs(newRawClient));
            }
        }
    }
}
