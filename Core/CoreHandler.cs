﻿using P2P_UAQ_Server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using P2P_UAQ_Server.Core.Events;
using P2P_UAQ_Server.ViewModels;
using P2P_UAQ_Server.Views;
using System.Windows;


namespace P2P_UAQ_Server.Core
{
    public class CoreHandler
    {
        private readonly static CoreHandler _instance = new CoreHandler();
		private string? _serverIP;
		private int _serverPort;
		private int _maxConnections;
		private TcpListener? _server;
		private List<Connection> _connections = new List<Connection>();

		// datos conexiones 
		private TcpClient? _client;
		private Connection _newConnection = new Connection(); // Variable reutilizable para los usuarios conectados

		private bool _isRunning = false;

		public event EventHandler<PrivateMessageReceivedEventArgs>? PrivateMessageReceived;
		public event EventHandler<MessageReceivedEventArgs>? PublicMessageReceived;


		private CoreHandler() {
			_serverIP = "";
			_serverPort = 0;
        }

        public static CoreHandler Instance { 
            get { return _instance; } 
        }

        public event Action<string>? ServerStatusUpdated;


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

			HandlerOnMessageReceived($"Server listo y esperando en: {_serverIP}:{_serverPort}");

			while (true)
			{
				_client = await _server.AcceptTcpClientAsync();
				// guardamos la conexión con sus datos

				_newConnection = new Connection();
				_newConnection.Stream = _client.GetStream();
				_newConnection.StreamWriter = new StreamWriter(_newConnection.Stream); // stream para enviar
				_newConnection.StreamReader = new StreamReader(_newConnection.Stream); // stream para recibir


				// confirmamos el nombre

				var dataReceived = _newConnection.StreamReader!.ReadLine();
				var message = JsonConvert.DeserializeObject<Message>(dataReceived!);
				string? json = message!.Data as string;
				var convertedData = JsonConvert.DeserializeObject<Connection>(json!);

				_newConnection.Nickname = convertedData!.Nickname;
				_newConnection.IpAddress = convertedData.IpAddress; // ip

				if (object.Equals(convertedData.IpAddress, "0.0.0.0")) _newConnection.IpAddress = "127.0.0.1";

				_newConnection.Port = convertedData.Port; // puerto

				HandlerOnMessageReceived($"En espera de aprovación de nombre: {_newConnection.Nickname} - {_newConnection.IpAddress}:{_newConnection.Port}.");

				if (message.Type == MessageType.UserConnected)
				{
					var existingConnection = _connections.FindAll(c => c.Nickname == _newConnection.Nickname);

					if (existingConnection.Count == 0)
					{
						var messageToSend = new Message();
						messageToSend.Type = MessageType.UsernameInUse;
						messageToSend.Data = false;

						_newConnection.StreamWriter.WriteLine(JsonConvert.SerializeObject(messageToSend));
						_newConnection.StreamWriter.Flush();

						_connections.Add(_newConnection);

						HandlerOnMessageReceived("Nombre disponible. Notificando al cliente.");
						HandlerOnMessageReceived($"Conexión agregada: {_newConnection.IpAddress}:{_newConnection.Port} y notificando a todos.");
						
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

						HandlerOnMessageReceived($"Conexión rechazada: {_newConnection.IpAddress}:{_newConnection.Port}. Notificando al cliente.");
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

                    if (message!.Type == MessageType.UserDisconnected)
                    {
                       
                    }
                }
                catch
                {
					// disconnected user
					_connections.RemoveAll(c => c.Nickname == connection.Nickname && c.IpAddress == connection.IpAddress && c.Port == connection.Port);
					HandlerOnMessageReceived($"Usuario desconectado. Actualizando a todos: {connection.Nickname} - {connection.IpAddress}:{connection.Port}. Notificando al cliente.");

					foreach (Connection c in _connections)
					{
						try
						{
							var msgUserDisconnected = $"{connection.Nickname} se ha desconectado.";
							var msgUserToBeSent = new Message { Type = MessageType.Message, Data = msgUserDisconnected };

							c.StreamWriter!.WriteLine(JsonConvert.SerializeObject(msgUserToBeSent));
							c.StreamWriter!.Flush();

							SendDisconnectedUserToAll(c, connection);
						}
						catch
						{
						}
					}

					connectionOpen = false;
				}
            }
        }

        public void SendConnectionListToAll(Connection receiver, Connection connection)
        {
			var message = new Message
			{
				Type = MessageType.UserConnected,
				Data = JsonConvert.SerializeObject(connection),
			};

			var json = JsonConvert.SerializeObject(message);

			receiver.StreamWriter!.WriteLine(json);
			receiver.StreamWriter!.Flush();
		}


		public void SendDisconnectedUserToAll(Connection receiver, Connection connection)
        {
			var message = new Message
			{
				Type = MessageType.UserDisconnected,
				Data = JsonConvert.SerializeObject(connection),
			};

			var json = JsonConvert.SerializeObject(message);

			receiver.StreamWriter!.WriteLine(json);
			receiver.StreamWriter!.Flush();

		}

		public void StopServer()
        {
            if (_isRunning)
            {
                _server!.Stop();
                _isRunning = false;
            }
			HandlerOnMessageReceived("Servidor cerrado.");
        }


		// Eventos de interfaz
		
		// Invokers

		private void OnMessageReceived(MessageReceivedEventArgs e) => PublicMessageReceived?.Invoke(this, e);
		

		// Handlers

		private void HandlerOnMessageReceived(string value) => OnMessageReceived(new MessageReceivedEventArgs(value));
		
	}
}
