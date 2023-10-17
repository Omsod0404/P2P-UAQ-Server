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


/*
 
WORKING ON MY BRANCH

 */

namespace P2P_UAQ_Server.Models
{
    public class ServerModel
    {
        private string ipAddress;
        private string port;
        private string maxConnections;

        private TcpListener server;
        private Stream _stream;
        private StreamWriter _writer;
        private StreamReader _reader;
        private bool isRunning = false;
        private Thread listenThread;

        // ****

        // ****


        public ServerModel(string ipAddress, string port, string maxConnections)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            this.maxConnections = maxConnections;
        }

        //Este es para manejar el estado del servidor
        public event Action<string> ServerStatusUpdated;

        public bool StartServer()
        {
            if (!isRunning)
            {
                IPAddress ip = IPAddress.Parse(ipAddress);
                server = new TcpListener(ip, int.Parse(port));
                server.Start(int.Parse(maxConnections));

                //Esta es la manera en que se manda la informacion del estado al servidor, es como si fuera el console.Write
                OnStatusUpdated("Servidor escuchando en "+ip+":"+port);
                OnStatusUpdated("Esperando conexiones...");
                connectionManager(server);

                return true;                    
               
            }
            return true;
        }

        public void StopServer()
        {
            if (isRunning)
            {
                server.Stop();
                isRunning = false;
            }
            OnStatusUpdated("Conexiones cerradas");
        }

        //Para actualizar el status del server en el dashboard, esta se tiene que quedar
        public void OnStatusUpdated(string status)
        {
            ServerStatusUpdated?.Invoke(status);
        }

        // ****

        public async void connectionManager(TcpListener server) { 
            
            List<Connection> contactBook = new List<Connection>();
            
            OnStatusUpdated("Connection list is ready");

            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                Thread clientT = new Thread(() => clientThread(client, contactBook));
                clientT.Start();
            }

        }

        public void clientThread(TcpClient client, List<Connection> contactBook)
        {
            byte[] buff = new byte[2097152]; // 2MB

            string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            int clientPort = ((IPEndPoint)client.Client.RemoteEndPoint).Port;

            OnStatusUpdated("Client connected: " + clientIP + ":" + clientPort);

            int bytesName = client.GetStream().Read(buff);
            string clientName = Encoding.UTF8.GetString(buff, 0, bytesName);

            clientName = checkName(clientName, contactBook, client);

            addConnectionToContactBook(contactBook, clientIP, clientName, clientPort);

            sendJSON(client, contactBook, clientName);

        }

        public string checkName(string name, List<Connection> contactBook, TcpClient client) {

            byte[] buff = new byte[120];
            int bytesInName;
            NetworkStream stream = client.GetStream();


            bool inUse = nameInUse(name, contactBook);

            if (!inUse) { stream.WriteByte(0); }

            while (inUse == true) {

                stream.WriteByte(1);
                //int b = stream.Read(new byte[1], 0, 1);
                bytesInName = stream.Read(buff);
                name = Encoding.UTF8.GetString(buff, 0, bytesInName);

                inUse = nameInUse(name, contactBook);

                if (!inUse) { stream.WriteByte(0); }

            }

            return name;
        }

        public bool nameInUse(string name, List<Connection> contactBook) {

            bool inUse = false;

            foreach (Connection c in contactBook)
            {
                if (c.Nickname.Equals(name)) { inUse = true; break; }
            }

            return inUse;
        }

        public void sendJSON(TcpClient client, List<Connection> contactBook, string name) {
            
            NetworkStream stream = client.GetStream();
            
            string json = JsonSerializer.Serialize(removeUser(contactBook, name));
            
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            stream.Write(jsonBytes, 0, jsonBytes.Length);

            OnStatusUpdated("Contact list sent to client:" + name);
        }

        public List<Connection> removeUser(List<Connection> contactBook, string name)
        {

            List<Connection> connections = new List<Connection> { };
            foreach (var connection in contactBook)
            {
                if (!connection.Nickname.Equals(name)) { connections.Add(connection); }
            }

            return connections;
        }

        public void addConnectionToContactBook(List<Connection> contactBook, string ip, string name, int port) {

            var connection = new Connection
            {

                Nickname = name,
                IpAddress = ip,
                Port = port

            };

            contactBook.Add(connection);

            OnStatusUpdated("List updated (New contact added): " + name + ":" + ip + ":" + port);

        }

        // ****


    }
}
