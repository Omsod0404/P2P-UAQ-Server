using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using P2P_UAQ_Server.ViewModels;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Net.NetworkInformation;
using P2P_UAQ_Server.Models;
using Newtonsoft.Json;


/*
 
WORKING ON MY BRANCH

 */

namespace P2P_UAQ_Server.Models
{
    public class ServerModel
    {
        // datos server
        private string _serverIP;
        private int _serverPort;
        private string _maxConnections;
        private TcpListener _server;
        private List<Connection> _connections = new List<Connection>();

        // datos conexiones 
        private TcpClient _client;
        private Connection _newConnection = new Connection(); // Variable reutilizable para los usuarios conectados

        //private Stream _stream;
        //private StreamWriter _writer;
        //private StreamReader _reader;

        private bool _isRunning = false;
        //private Thread listenThread;

        // ****

        // ****


        public ServerModel(string ipAddress, string port, string maxConnections)
        {
            this._serverIP = ipAddress;
            this._serverPort = Int32.Parse(port);
            this._maxConnections = maxConnections;
        }

        //Este es para manejar el estado del servidor
        public event Action<string> ServerStatusUpdated;

        public bool StartServer()
        {
            if (!_isRunning)
            {

                InitializeLocalServer();

                return true;                    
               
            }
            return true;
        }

        public void StopServer()
        {
            if (_isRunning)
            {
                _server.Stop();
                _isRunning = false;
            }
            OnStatusUpdated("Conexiones cerradas");
        }

        //Para actualizar el status del server en el dashboard, esta se tiene que quedar
        public void OnStatusUpdated(string status)
        {
            ServerStatusUpdated?.Invoke(status);
        }

        // ******************************************************************************

        // PARA INICIAR SERVIDOR
        public async void InitializeLocalServer()
        {

            // iniciamos servidor para escuhcar todo

            _server = new TcpListener(IPAddress.Parse(_serverIP), _serverPort);
            _server.Start(int.Parse(_maxConnections));

            OnStatusUpdated("Server listo y esperando en: " + _serverIP + ":" + _serverPort);

            while (true)
            {
                _client = await _server.AcceptTcpClientAsync();
                // guardamos la conexión con sus datos

                _newConnection = new Connection();
                _newConnection.Stream = _client.GetStream();
                _newConnection.StreamWriter = new StreamWriter(_newConnection.Stream); // stream para enviar
                _newConnection.StreamReader = new StreamReader(_newConnection.Stream); // stream para recibir

                var newConnectionEndPoint = ((IPEndPoint)_client.Client.RemoteEndPoint!);

                _newConnection.IpAddress = newConnectionEndPoint.Address.ToString(); // ip
                _newConnection.Port = newConnectionEndPoint.Port; // puerto

                OnStatusUpdated("En espera de aprovación de nombre: " + _newConnection.IpAddress + ":" + _newConnection.Port);

                // confirmamos el nombre

                var dataReceived = await _newConnection.StreamReader!.ReadLineAsync();
                var message = JsonConvert.DeserializeObject<Message>(dataReceived!);
                var convertedData = JsonConvert.DeserializeObject<Connection>(message!.Data as string);

                _newConnection.Nickname = convertedData!.Nickname;

				OnStatusUpdated("mensaje recibido");

                if (message.Type == MessageType.UserConnected)
                {
                    var existingConnection = _connections.FindAll(c => c.Nickname == _newConnection.Nickname && c.IpAddress == _newConnection.IpAddress && c.Port == _newConnection.Port);

                    if (existingConnection.Count == 0)
                    {
                        var messageToSend = new Message();
                        messageToSend.Type = MessageType.UsernameInUse;
                        messageToSend.Data = false;

                        _newConnection.StreamWriter.WriteLine(JsonConvert.SerializeObject(messageToSend));
                        _newConnection.StreamWriter.Flush();

                        _connections.Add(_newConnection);
                        SendConnectionListToAll();

                        OnStatusUpdated("Conexión agregada" + _newConnection.IpAddress + "" + _newConnection.Port + " y lista enviada a todos");

                        Thread thread = new Thread(ListenToConnection);
                        thread.Start();
                    }
                    else
                    {
                        // enviar error
                        message = new Message(); // overwrite el mensaje

                        message.Type = MessageType.UsernameInUse;
                        message.NicknameRequester = "server";
                        message.PortRequester = _serverPort;
                        message.IpAddressRequester = _serverIP;
                        message.Data = _newConnection.Nickname; // envia como dato el nombre en uso

                        string messageJson = JsonConvert.SerializeObject(message);

                        _newConnection.StreamWriter.WriteLine(messageJson);

                        OnStatusUpdated("Conexión rechazada: " + _newConnection.IpAddress + ":" + _newConnection.Port);
                    }
                }

            }
        }

        public async void ListenToConnection()
        {

            Connection connection = _newConnection;

            
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
                    }
                }
                catch (Exception ex)
                {

                }
            
        }
        // ****

        public async void SendConnectionListToAll()
        {

            var connections = _connections;

            var message = new Message
            {
                Type = MessageType.UserConnected,
                Data = connections,
            };

            var json = JsonConvert.SerializeObject(message);

            foreach (var c in connections)
            {
                await c.StreamWriter.WriteAsync(json);
                await c.StreamWriter.FlushAsync();
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

        // ******************************************************************************
        // ******************************************************************************
        // ******************************************************************************


        //        public async void connectionManager(TcpListener server) { 

        //            List<Connection> contactBook = new List<Connection>();

        //            OnStatusUpdated("Connection list is ready");

        //            while (true)
        //            {
        //                TcpClient client = await server.AcceptTcpClientAsync();
        //                Thread clientT = new Thread(() => clientThread(client, contactBook));
        //                clientT.Start();
        //            }

        //        }

        //        public void clientThread(TcpClient client, List<Connection> contactBook)
        //        {
        //            byte[] buff = new byte[2097152]; // 2MB

        //            string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
        //            int clientPort = ((IPEndPoint)client.Client.RemoteEndPoint).Port;

        //            OnStatusUpdated("Client connected: " + clientIP + ":" + clientPort);

        //            int bytesName = client.GetStream().Read(buff);
        //            string clientName = Encoding.UTF8.GetString(buff, 0, bytesName);

        //            clientName = checkName(clientName, contactBook, client);

        //            addConnectionToContactBook(contactBook, clientIP, clientName, clientPort);

        //            sendJSON(client, contactBook, clientName);

        //        }

        //        public string checkName(string name, List<Connection> contactBook, TcpClient client) {

        //            byte[] buff = new byte[120];
        //            int bytesInName;
        //            NetworkStream stream = client.GetStream();


        //            bool inUse = nameInUse(name, contactBook);

        //            if (!inUse) { stream.WriteByte(0); }

        //            while (inUse == true) {

        //                stream.WriteByte(1);
        //                //int b = stream.Read(new byte[1], 0, 1);
        //                bytesInName = stream.Read(buff);
        //                name = Encoding.UTF8.GetString(buff, 0, bytesInName);

        //                inUse = nameInUse(name, contactBook);

        //                if (!inUse) { stream.WriteByte(0); }

        //            }

        //            return name;
        //        }

        //        public bool nameInUse(string name, List<Connection> contactBook) {

        //            bool inUse = false;

        //            foreach (Connection c in contactBook)
        //            {
        //                if (c.Nickname.Equals(name)) { inUse = true; break; }
        //            }

        //            return inUse;
        //        }

        //        public void sendJSON(TcpClient client, List<Connection> contactBook, string name) {

        //            NetworkStream stream = client.GetStream();

        //            string json = JsonSerializer.Serialize(removeUser(contactBook, name));

        //            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        //            stream.Write(jsonBytes, 0, jsonBytes.Length);

        //            OnStatusUpdated("Contact list sent to client:" + name);
        //        }

        //        public List<Connection> removeUser(List<Connection> contactBook, string name)
        //        {

        //            List<Connection> connections = new List<Connection> { };
        //            foreach (var connection in contactBook)
        //            {
        //                if (!connection.Nickname.Equals(name)) { connections.Add(connection); }
        //            }

        //            return connections;
        //        }

        //        public void addConnectionToContactBook(List<Connection> contactBook, string ip, string name, int port) {

        //            var connection = new Connection
        //            {

        //                Nickname = name,
        //                IpAddress = ip,
        //                Port = port

        //            };

        //            contactBook.Add(connection);

        //            OnStatusUpdated("List updated (New contact added): " + name + ":" + ip + ":" + port);

        //        }

        //        // ****


    }
}
