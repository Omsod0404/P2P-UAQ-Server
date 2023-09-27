using System;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using ServerP2P.Models;
using System.Data;

namespace ServerP2P
{
    internal class Server
    {
        static void Main(string[] args)
        {
            // Server socket variables

            String ip = "127.0.0.1";
            int port = 666;

            // Server socket creation

            IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Socket serverSckt = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSckt.Bind(iPEndPoint);

            serverSckt.Listen(50);

            // Peers info

            List<Connection> contactBook = new List<Connection>();

            Console.WriteLine("SERVER IS UP");

            while (true)
            {

                Socket requestSckt = serverSckt.Accept();
                Thread clientThread = new Thread(() => serverThread(requestSckt, contactBook));
                clientThread.Start();

            }

        }


        public static void serverThread(Socket requestSckt, List<Connection> contactBook) {

            while (true)
            {
                byte[] buff = new byte[2097152]; // 2MB

                Console.WriteLine("Server is listening...");

                // IP and Port of client request

                IPEndPoint rqstEndPoint = (IPEndPoint)requestSckt.RemoteEndPoint;
                string rqstIP = rqstEndPoint.Address.ToString();
                int rqstPort = rqstEndPoint.Port;

                Console.WriteLine("Client connected: IP:" + rqstIP + " Port: " + rqstPort);


                // to store username sent by client

                int bytesInMsg = requestSckt.Receive(buff);
                string msg = Encoding.UTF8.GetString(buff, 0, bytesInMsg);


                // For checking names in use

                bool nameInUse = inContactBook(msg, contactBook);
                if (!nameInUse) { requestSckt.Send(Encoding.UTF8.GetBytes("0")); }
                while (nameInUse == true)
                {

                    requestSckt.Send(Encoding.UTF8.GetBytes("1"));
                    bytesInMsg = requestSckt.Receive(buff);
                    msg = Encoding.UTF8.GetString(buff, 0, bytesInMsg);
                    nameInUse = inContactBook(msg, contactBook);
                    if (!nameInUse) { requestSckt.Send(Encoding.UTF8.GetBytes("0")); }

                }

                var connection = new Connection
                {
                    Name = msg,
                    Ip = rqstIP,
                    Port = rqstPort
                };

                contactBook.Add(connection); // add contact to contacBook

                // send JSON contactBook
                string json = JsonSerializer.Serialize(removeUser(contactBook, connection.Name));

                requestSckt.Send(Encoding.UTF8.GetBytes(json));

            }

        }

        public static bool inContactBook(String name, List<Connection> contactBook)
        {
            // Checks if a user name is in use

            bool inUse = false;

            foreach (var connection in contactBook)
            {
                if (connection.Name.Equals(name)) { inUse = true; break; }
            }

            return inUse;
        }

        public static List<Connection> removeUser(List<Connection> contactBook, string name){
            
            List <Connection> connections = new List<Connection> { };
            foreach (var connection in contactBook)
            {
                if (!connection.Name.Equals(name)) { connections.Add(connection); }
            }

            return connections;
        }

    }
}