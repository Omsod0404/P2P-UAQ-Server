using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Server.ViewModels;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;

namespace Server.Models
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
        private Thread serverThread;
        

        public ServerModel(string ipAddress, string port, string maxConnections)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            this.maxConnections = maxConnections;
        }

        public bool StartServer()
        {
            if (!isRunning)
            {
                try
                {
                    IPAddress ip = IPAddress.Parse(ipAddress);
                    server = new TcpListener(ip, int.Parse(port));
                    server.Start(int.Parse(maxConnections));


					isRunning = true;
					Console.WriteLine("Escuchando en {0}:{0}", ipAddress, port);

					while (true)
                    {
                        var client = server.AcceptTcpClient();

                        _stream = client.GetStream();
                        _reader = new StreamReader(_stream);
                        _writer = new StreamWriter(_stream);

                    }

                    

                    return true;

                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error al iniciar el servidor: " + ex.Message);
                    return false;
                }
            }
            return true;
        }

          
        
        //Esto se tiene que quitar
        public void StopServer()
        {
            if (isRunning)
            {
                server.Stop();
                isRunning = false;
            }
        }

    }
}
