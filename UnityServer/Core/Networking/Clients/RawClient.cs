using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityServer.Tools;

namespace UnityServer.Core.Networking.Clients
{
    public class RawClient : IClient, IDisposable
    {
        private readonly TcpClient _tcpClient; // Underlying TCP client handling network communication

        private static int _id = 0; // Static counter to ensure unique client IDs
        public int ID { get; private set; } // Unique ID for each client
        public string Name { get; private set; } = "RawClient"; // Client name (default)

        public IPAddress Address { get; private set; } // Client IP address
        public string DnsAddress { get; private set; } = "unknown.unknown.com"; // Client DNS name (if available)

        public bool IsConnected { get; private set; } = false; // Indicates if the client is connected
        public bool Disconnecting { get; private set; } = false; // Prevents redundant disconnect attempts

        public event EventHandler? Disconnected; // Event triggered when the client disconnects

        public DateTime LastUpdateTime { get; private set; } // Last active time (useful for timeout handling)
        public DateTime ConnectionStartTime { get; private set; } // Time when the connection started
        private DateTime _lastPollTime = DateTime.MinValue; // Last time SafePollClient was called
        private bool _lastPollResult = false; // Last known result of SafePollClient check

        private readonly NetworkStream _stream; // Network stream for reading/writing data
        private readonly StreamReader _reader; // Stream reader for receiving text-based data
        private readonly StreamWriter _writer; // Stream writer for sending text-based data

        public RawClient(TcpClient incomingClient)
        {
            _tcpClient = incomingClient;

            ID = Interlocked.Increment(ref _id); // Assign unique client ID

            IPEndPoint? remoteEndPoint = (IPEndPoint?)incomingClient.Client.RemoteEndPoint; // Retrieve remote address

            LastUpdateTime = DateTime.Now;
            ConnectionStartTime = DateTime.Now;

            _stream = _tcpClient.GetStream(); // Get network stream
            _reader = new(_stream, Encoding.ASCII); // Initialize reader
            _writer = new(_stream, Encoding.ASCII) { AutoFlush = true }; // Initialize writer (auto-flush enabled)

            Name = $"RawClient_{ID}"; // Default naming convention

            if (remoteEndPoint == null)
            {
                Address = IPAddress.Parse("0.0.0.0");
                Disconnect(); // Prevent further operations if the address is invalid
                return;
            }

            Address = remoteEndPoint.Address;

            // Attempt to get the hostname (reverse DNS lookup)
            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(Address);
                DnsAddress = hostEntry.HostName; // Use HostName instead of raw IP
            }
            catch (SocketException)
            {
                DnsAddress = Address.ToString(); // Fallback to IP if DNS lookup fails
                Disconnect();
                return;
            }

            Scribe.Write($"NewClient: {ID} has joined from {Address}!");

            Name = $"RawClient_{ID}:{Address}::{DnsAddress}"; // Finalize the client name format
            IsConnected = true; // Mark client as connected
        }

        public async Task<string> ReadLineAsync()
        {
            try
            {
                if (!SafeToProceedWithClient())
                {
                    Disconnect();
                    return string.Empty;
                }

                string? response = await _reader.ReadLineAsync();

                if (response == null) // Null means actual disconnect (remote socket closed)
                {
                    Scribe.Write($"{Name}: Remote client disconnected.");
                    Disconnect();
                    return string.Empty;
                }

                return response; // Allow empty messages (e.g., Enter key)
            }
            catch (IOException)
            {
                Disconnect(); // Handle network failure
                return string.Empty;
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
                Disconnect();
            }

            return string.Empty;
        }

        public async Task WriteAsync(string message)
        {
            try
            {
                if (!SafeToProceedWithClient())
                {
                    Disconnect();
                    return;
                }

                await _writer.WriteAsync(message);
            }
            catch (IOException)
            {
                Scribe.Write($"{Name}: Write failed due to network error. Retrying...");

                await Task.Delay(100); // Small delay before retry

                if (!SafeToProceedWithClient())
                {
                    Scribe.Write($"{Name}: Write failed again, client is disconnected.");
                    Disconnect();
                    return;
                }

                await _writer.WriteAsync(message); // Retry once
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
                Disconnect();
            }
        }

        public async Task WriteLineAsync(string message)
        {
            try
            {
                if (!SafeToProceedWithClient())
                {
                    Disconnect();
                    return;
                }

                await WriteAsync(message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (Disconnecting) return;

            Disconnecting = true;
            IsConnected = false;

            Scribe.Write($"Disconnecting Client: {Name}, with ID: {ID}");

            try
            {
                _writer.Close();
                _reader.Close();
                _stream.Close();
                _tcpClient.Close();
            }
            catch (Exception ex)
            {
                Scribe.Error(ex);
            }

            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public bool SafePollClient()
        {
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                IsConnected = false;
                return false;
            }

            if ((DateTime.Now - _lastPollTime).TotalMilliseconds < 500)
                return _lastPollResult;

            _lastPollTime = DateTime.Now;
            Socket socket = _tcpClient.Client;

            bool isDisconnected = socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0;
            _lastPollResult = !isDisconnected && socket.Poll(0, SelectMode.SelectWrite);

            IsConnected = _lastPollResult;
            return _lastPollResult;
        }

        private bool SafeToProceedWithClient()
        {
            return !Disconnecting && SafePollClient();
        }

        public void Dispose()
        {
            _stream.Dispose();
            _reader.Dispose();
            _writer.Dispose();
            _tcpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
