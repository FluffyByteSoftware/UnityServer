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
        public bool IsRunning { get; private set; } = false;
        
        private readonly TcpListener _listener;

        public event EventHandler<ClientEventArgs>? NewRawClientJoined;
        public event EventHandler<ClientEventArgs>? RawClientDisconnected;

        public Sentinel()
        {
            _listener = new(IPAddress.Parse("10.0.0.84"), 9998);
        }

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
                _listener.Start();
                Task.Run(ListenLoop);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
                return Task.FromException(ex);
            }
        }


        public async Task Stop()
        {
            if (!IsRunning) return;

            try
            {
                IsRunning = false;

                Scribe.Write("Stopping TCP Listener...");

                _listener.Stop(); // Properly stop the listener
                await RawClientManager.Singleton.BroadcastMessage("Shutdown immediately!");

                Scribe.Write("Server stopped.");
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }
        }


        private async Task ListenLoop()
        {
            try
            {
                while (IsRunning)
                {
                    try
                    {
                        TcpClient newConnection = await _listener.AcceptTcpClientAsync();

                        if (!IsRunning)
                        {
                            newConnection.Close();
                            return; // Prevent adding clients if stopping
                        }

                        RawClient newClient = new(newConnection);
                        Scribe.Write($"New Client Joined :: {newClient.Name} from {newClient.Address}");

                        NewRawClientJoined?.Invoke(this, new ClientEventArgs(newClient)); // Fire event

                        _ = HandleNewClient(newClient);
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
            }
        }

    }
}
