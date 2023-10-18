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
using P2P_UAQ_Server.Models;
using System.Windows;


namespace P2P_UAQ_Server.Core
{
    public class CoreHandler
    {
        private readonly static CoreHandler _instance = new CoreHandler();
        private List<Connection> _connections = new List<Connection> ();
        private Connection _newConnection = new Connection(); // Variable reutilizable para los usuarios conectados.
        private TcpListener _server;
        private TcpClient _client;
        private string _serverIP;
        private int _serverPort;
        private bool _isRunning = false;

        // Eventos de interfaz
        public event EventHandler<PrivateMessageReceivedEventArgs> PrivateMessageReceived;

        private CoreHandler() { 
        
        }

        public static CoreHandler Instance { 
            get { return _instance; } 
        }

        public event Action<string> ServerStatusUpdated;


        // Invoker
        //private void OnPrivateMessageReceived(PrivateMessageReceivedEventArgs e) => PrivateMessageReceived?.Invoke(this, e);

        // Handler
        //private void OnStatusUpdated(string message) => OnPrivateMessageReceived(new PrivateMessageReceivedEventArgs(message));

        //Para actualizar el status del server en el dashboard, esta se tiene que quedar
        public void OnStatusUpdated(string status)
        {
            ServerStatusUpdated?.Invoke(status);
        }

        // ****

        // PARA INICIAR SERVIDOR
        public void InitializeLocalServer(string ip, int port, string maxConnections) {

            // datos del servidor

            _serverIP = ip;
            _serverPort = port;

            // iniciamos servidor para escuhcar todo

            _server = new TcpListener(IPAddress.Any, _serverPort);
            _server.Start(int.Parse(maxConnections));

            OnStatusUpdated("Server listo y esperando en: " + _serverIP + ":" + _serverPort);

            while (true)
            {
                _client = _server.AcceptTcpClient();

                // guardamos la conexión con sus datos
                
                _newConnection = new Connection();
                _newConnection.Stream = _client.GetStream();
                _newConnection.StreamWriter = new StreamWriter(_newConnection.Stream); // stream para enviar
                _newConnection.StreamReader = new StreamReader(_newConnection.Stream); // stream para recibir

                var newConnectionEndPoint = ((IPEndPoint)_client.Client.RemoteEndPoint!); 

                _newConnection.IpAddress = newConnectionEndPoint.Address.ToString(); // ip
                _newConnection.Port = newConnectionEndPoint.Port; // puerto

                // confirmamos el nombre

                var dataReceived =  _newConnection.StreamReader!.ReadLine();
                var message = JsonConvert.DeserializeObject<Message>(dataReceived!);

                if (message.Type != MessageType.UserConnected)
                {
                    var existingConnection = _connections.FindAll(c => c.Nickname == _newConnection.Nickname && c.IpAddress == _newConnection.IpAddress && c.Port == _newConnection.Port);
                   
                    if (existingConnection.Count == 0)
                    {
                        _connections.Add(_newConnection);
                        SendConnectionListToAll();

                        Thread thread = new Thread(ListenToConnection);
                        thread.Start();
                    }
                    else
                    {
                        // enviar error
                        message = new Message(); // overwrite el mensaje

                        message.Type = MessageType.NameInUse;
                        message.NicknameRequester = "server";
                        message.PortRequester = _serverPort;
                        message.IpAddressRequester = _serverIP;
                        message.Data = _newConnection.Nickname; // envia como dato el nombre en uso

                        string messageJson = JsonConvert.SerializeObject(message);

                        _newConnection.StreamWriter.WriteLine(messageJson);
                        _newConnection.StreamWriter.Flush();
                    }
                }
               
            }
        }
        
        public async void ListenToConnection()
        {
            
            Connection connection = _newConnection;
            var connectionOpen = true;

            while (connectionOpen) 
            {
                try
                {

                    var dataReceived = await connection.StreamReader!.ReadLineAsync();
                    var message = JsonConvert.DeserializeObject<Message>(dataReceived!);

                    if (message.Type == MessageType.UserDisconnected)
                    {
                        // disconnected user
                        _connections.RemoveAll(c => c.Nickname == connection.Nickname && c.IpAddress == connection.IpAddress && c.Port == connection.Port);
                        SendDisconnectedUserToAll(connection);

                        OnStatusUpdated("User removed and sent: " + connection.Nickname + ":" + connection.IpAddress + ":" + connection.Port);
                        connectionOpen = false;
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }
        // ****

        public void SendConnectionListToAll()
        {

            var connections = _connections;
            var message = new Message();

            message.Type = MessageType.UserConnected;
            message.NicknameRequester = "server";
            message.PortRequester = _serverPort;
            message.IpAddressRequester = _serverIP;
            message.Data = connections;

            string messageJson = JsonConvert.SerializeObject(message);

            foreach (var c in connections)
            {
                c.StreamWriter.WriteLine(messageJson);
            }
        }

        public void SendDisconnectedUserToAll(Connection connection)
        {
            var connections = _connections;
            var message = new Message();

            message.Type = MessageType.UserDisconnected;
            message.NicknameRequester = "server";
            message.PortRequester = _serverPort;
            message.IpAddressRequester = _serverIP;
            message.Data = connection;

            string messageJson = JsonConvert.SerializeObject(message);

            foreach (var c in connections)
            {
                c.StreamWriter.WriteLine(messageJson);
            }
        }


        public void StopServer()
        {
            if (_isRunning)
            {
                _server.Stop();
                _isRunning = false;
            }
            OnStatusUpdated("Servidor cerrado");
        }
    }
}
