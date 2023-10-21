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
		private string _serverIP;
		private int _serverPort;
		private int _maxConnections;
		private TcpListener _server;
		private List<Connection> _connections = new List<Connection>();

		// datos conexiones 
		private TcpClient _client;
		private Connection _newConnection = new Connection(); // Variable reutilizable para los usuarios conectados

		private bool _isRunning = false;

		public event EventHandler<PrivateMessageReceivedEventArgs> PrivateMessageReceived;
		public event EventHandler<MessageReceivedEventArgs> PublicMessageReceived;


		private CoreHandler() { 
        
        }

        public static CoreHandler Instance { 
            get { return _instance; } 
        }

        public event Action<string> ServerStatusUpdated;


        //Para actualizar el status del server en el dashboard, esta se tiene que quedar
        public void OnStatusUpdated(string status)
        {
            ServerStatusUpdated?.Invoke(status);
        }

        // ****

        // PARA INICIAR SERVIDOR
        public async void InitializeLocalServer(string ip, int port, string maxConnections) 
		{
			_serverIP = ip;
			_serverPort = port;
			_maxConnections = int.Parse(maxConnections);


			_server = new TcpListener(IPAddress.Parse(_serverIP), _serverPort);
			_server.Start(_maxConnections);

			HandlerOnMessageReceived("Server listo y esperando en: " + _serverIP + ":" + _serverPort);

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

				HandlerOnMessageReceived("En espera de aprovación de nombre: " + _newConnection.IpAddress + ":" + _newConnection.Port);

				// confirmamos el nombre

				var dataReceived = _newConnection.StreamReader!.ReadLine();
				var message = JsonConvert.DeserializeObject<Message>(dataReceived!);
				var convertedData = JsonConvert.DeserializeObject<Connection>(message!.Data as string);

				_newConnection.Nickname = convertedData!.Nickname;

				HandlerOnMessageReceived("Mensaje recibido");

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

						HandlerOnMessageReceived("Conexión agregada" + _newConnection.IpAddress + "" + _newConnection.Port + " y lista enviada a todos");

						foreach (Connection c in _connections)
						{
							// Se les enviara un mensaje de que x usuario se ha conectado.
							var msgUserToBeSent = new Message { Type = MessageType.Message, Data = $"{_newConnection.Nickname} se ha conectado." };

							// Enviamos el mensaje al cliente.
							c.StreamWriter!.WriteLine(JsonConvert.SerializeObject(msgUserToBeSent));
							c.StreamWriter!.Flush();

							foreach (var con in _connections)
							{
								SendConnectionListToAll(c, con);
							}
						}

						Thread thread = new Thread(ListenToConnection);
						thread.Start();
					}
					else
					{
						// enviar error
						message = new Message(); // overwrite el mensaje
						message.Type = MessageType.UsernameInUse;
						message.Data = true; // envia como dato el nombre en uso

						string messageJson = JsonConvert.SerializeObject(message);

						_newConnection.StreamWriter.WriteLine(messageJson);
						_newConnection.StreamWriter.Flush();

						HandlerOnMessageReceived("Conexión rechazada: " + _newConnection.IpAddress + ":" + _newConnection.Port);
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

                        HandlerOnMessageReceived("User removed and sent: " + connection.Nickname + ":" + connection.IpAddress + ":" + connection.Port);
                        connectionOpen = false;
                    }
                }
                catch
                {
					
				}
            }
        }
        // ****

        public void SendConnectionListToAll(Connection receiver, Connection connection)
        {

			var message = new Message
			{
				Type = MessageType.UserConnected,
				Data = JsonConvert.SerializeObject(connection),
			};

			var json = JsonConvert.SerializeObject(message);

			receiver.StreamWriter!.WriteLine(JsonConvert.SerializeObject(json));
			receiver.StreamWriter!.Flush();
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
			HandlerOnMessageReceived("Servidor cerrado");
        }


		// Eventos de interfaz
		
		// Invokers

		private void OnMessageReceived(MessageReceivedEventArgs e)
		{
			PublicMessageReceived?.Invoke(this, e);
		}

		// Handlers

		private void HandlerOnMessageReceived(string value)
		{
			OnMessageReceived(new MessageReceivedEventArgs(value));
		}
		
	}
}
