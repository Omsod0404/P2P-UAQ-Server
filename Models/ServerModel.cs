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

namespace Server.Models
{
    public class ServerModel
    {
        private string ipAddress;
        private int port;
        private int maxConnections;

        private TcpListener server;
        private bool isRunning = false;
        private Thread serverThread;
        

        public ServerModel(string ipAddress, int port, int maxConnections)
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
                    server = new TcpListener(ip, port);
                    server.Start(maxConnections);

                    

                    isRunning = true;

                    Console.WriteLine("Escuchando en {0}:{0}", ipAddress, port);
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
