using P2P_UAQ_Server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
//using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using P2P_UAQ_Server.Core.Events;
using P2P_UAQ_Server.ViewModels;
using P2P_UAQ_Server.Views;

namespace P2P_UAQ_Server.Core
{
    public class CoreHandler
    {
        private readonly static CoreHandler _instance = new CoreHandler();
        private List<Connection> _connections = new List<Connection> ();
        private Connection _newConnection = new Connection(); // Variable reutilizable para los usuarios conectados.
        private TcpListener _server;
        private TcpClient _client;

        // Eventos de interfaz
        public event EventHandler<PrivateMessageReceivedEventArgs> PrivateMessageReceived;

        private CoreHandler() { 
        
        }

        public static CoreHandler Instance { 
            get { return _instance; } 
        }

        // Invoker
        private void OnPrivateMessageReceived(PrivateMessageReceivedEventArgs e) => PrivateMessageReceived?.Invoke(this, e);

        // Handler
        private void HandlePrivateMessageReceived(string message) => OnPrivateMessageReceived(new PrivateMessageReceivedEventArgs(message));

        public async void InitializeLocalServer() {
            
            int port = 666; // TODO: change
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);

            _server = new TcpListener(localEndPoint);
            _server.Start();

            HandlePrivateMessageReceived("Server is ready and listening...");

            while (true)
            {
                _client = await _server.AcceptTcpClientAsync();
                


                _newConnection = new Connection();
                _newConnection.Stream = _client.GetStream();
                _newConnection.StreamWriter = new StreamWriter(_newConnection.Stream);
                _newConnection.StreamReader = new StreamReader(_newConnection.Stream);

                var ipFromNewConnection = ((IPEndPoint)_client.Client.RemoteEndPoint!);

                _newConnection.IpAddress = ipFromNewConnection.Address.ToString();
                _newConnection.Port = ipFromNewConnection.Port;



                Thread thread = new Thread(ListenAsLocalServer);
                thread.Start();
               
            }
        }
        
        public async void ListenAsLocalServer() {
            
            Connection connection = _newConnection;

            while (true) 
            {
                try {

                    var dataReceived = await connection.StreamReader!.ReadLineAsync();
                    var message = JsonConvert.DeserializeObject<Message>(dataReceived!);

                    if (message.Type == MessageType.UserConnected)
                    {
                        // checks if the name is available
                        var existingConnection = _connections.FindAll(c => c.Nickname == connection.Nickname && c.IpAddress == connection.IpAddress && c.Port == connection.Port);

                        if (existingConnection.Count == 0)
                        {
                            SendConnectionToUsers(connection, MessageType.UserConnected);
                            _connections.Add(connection);

                            HandlePrivateMessageReceived("User added and sent: "+connection.Nickname+":"+connection.IpAddress+":"+connection.Port);
                        }
                        else
                        {
                            // TODO: Manejar nombre en existencia, enviar error
                        }
                    }

                    if (message.Type == MessageType.UserDisconnected)
                    {
                        _connections.RemoveAll(c => c.Nickname == connection.Nickname && c.IpAddress == connection.IpAddress && c.Port == connection.Port);
                        SendConnectionToUsers(connection, MessageType.UserDisconnected);

                        HandlePrivateMessageReceived("User removed and sent: " + connection.Nickname + ":" + connection.IpAddress + ":" + connection.Port);
                    }
                }
                catch
                {

                }
            }

            
           
        }
        // ****

        public void SendConnectionToUsers(Connection connection, MessageType messageType) {
            
            var connections = _connections;
            var message = new Message();

            foreach (var c in connections)
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(c.IpAddress!), c.Port);
                TcpClient client = new TcpClient(endPoint);

                message.Type = messageType;
                message.NicknameRequester = c.Nickname;
                message.PortRequester = c.Port;
                message.IpAddressRequester = c.IpAddress;
                message.Data = connection;

                string connectionJson = JsonConvert.SerializeObject(message);

                c.StreamWriter.WriteLine(connectionJson);
                c.StreamWriter.Flush();
            }
        }
    }
}
