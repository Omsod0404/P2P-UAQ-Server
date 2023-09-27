using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace ClientP2P
{

    public class Connection
    {
        public string Name { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }
    }

    public class Client
    {
        static void Main(string[] args)
        {
            while(true)
            {
                string serverIp = "127.0.0.1";
                int serverPort = 666;
                byte[] buff = new byte[2097152]; // 2MB

                // Connect to the server
                Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(new IPEndPoint(IPAddress.Parse(serverIp), serverPort));

                Console.WriteLine("Connected to server...");


                // Read and send the username
                Console.WriteLine("Write user name: ");
                string username = Console.ReadLine();
                clientSocket.Send(Encoding.UTF8.GetBytes(username));

                // Check the existance of a user
                int bytCondition = clientSocket.Receive(buff);
                string nameInUse = Encoding.UTF8.GetString(buff, 0, bytCondition);
                bool inUse = nameInUse.Equals("1");
                while (inUse)
                {
                    Console.WriteLine("Name in use. Try another: ");
                    username = Console.ReadLine();
                    clientSocket.Send(Encoding.UTF8.GetBytes(username));

                    bytCondition = clientSocket.Receive(buff);
                    nameInUse = Encoding.UTF8.GetString(buff, 0, bytCondition);
                    inUse = nameInUse.Equals("1");
                }

                // Receive json
                int jsonBytesReceived = clientSocket.Receive(buff);
                string jsonContactBook = Encoding.UTF8.GetString(buff, 0, jsonBytesReceived);

                // Deserialize contactBook
                List<Connection> contactBook = JsonSerializer.Deserialize<List<Connection>>(jsonContactBook);

                Console.WriteLine("Received contactBook from server:");
                foreach (var connection in contactBook)
                {
                    Console.WriteLine($"Name: {connection.Name}, IP: {connection.Ip}, Port: {connection.Port}");
                }
            }
            
        }
    }
}