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
        private readonly TcpClient _tcpClient;
        
        private static int _id = 0;
        public int ID { get; private set; }
        public string Name { get; private set; } = "RawClient";

        public IPAddress Address { get; private set; }
        public string DnsAddress { get; private set; } = "unknown.unknown.com";

        public bool IsConnected { get; private set; } = false;
        public bool Disconnecting { get; private set; } = false;

        public event EventHandler? Disconnected;

        public DateTime LastUpdateTime { get; private set; }
        public DateTime ConnectionStartTime { get; private set; }
        private DateTime _lastPollTime = DateTime.MinValue;
        private bool _lastPollResult = false;

        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;

        public RawClient(TcpClient incomingClient)
        {
            _tcpClient = incomingClient;
            
            ID = Interlocked.Increment(ref _id);

            IPEndPoint? remoteEndPoint = (IPEndPoint?)incomingClient.Client.RemoteEndPoint;


            LastUpdateTime = DateTime.Now;
            ConnectionStartTime = DateTime.Now;

            _stream = _tcpClient.GetStream();
            _reader = new(_stream, Encoding.ASCII);
            _writer = new(_stream, Encoding.ASCII) { AutoFlush = true };

            Name = $"RawClient_{ID}";

            if (remoteEndPoint == null)
            {
                Address = IPAddress.Parse("0.0.0.0");
                Disconnect();
                return;
            }

            Address = remoteEndPoint.Address;

            // Attempt to get the hostname (reverse DNS lookup)
            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(Address);
                DnsAddress = hostEntry.HostName; // Use HostName instead of IP list
            }
            catch (SocketException)
            {
                DnsAddress = Address.ToString(); // Fallback to IP if DNS lookup fails
                Disconnect();
                return;
            }

            Scribe.Write($"NewClient: {ID} has joined from {Address}!");
            
            Name = $"RawClient_{ID}:{Address}::{DnsAddress}";
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
                Disconnect();
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
            catch (IOException) // Network-related write failure
            {
                Scribe.Write($"{Name}: Write failed due to network error. Retrying...");

                await Task.Delay(100); // Small delay before retry

                if (!SafeToProceedWithClient()) // ✅ Check connection before retrying
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
            catch(Exception ex)
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

            Dispose();

            Disconnected?.Invoke(this, EventArgs.Empty);
        }


        public bool SafePollClient()
        {
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                IsConnected = false;
                return false;
            }

            // Only poll if 500ms have passed
            if ((DateTime.Now - _lastPollTime).TotalMilliseconds < 500)
                return _lastPollResult;

            _lastPollTime = DateTime.Now;

            Socket socket = _tcpClient.Client;
            bool isDisconnected = socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0;

            _lastPollResult = !isDisconnected && socket.Poll(0, SelectMode.SelectWrite);

            IsConnected = _lastPollResult; // ✅ Update IsConnected dynamically
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
