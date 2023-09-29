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

namespace P2P_UAQ_Server.Models
{
    public class ServerModel
    {
        private string ipAddress;
        private int port;
        private int maxConnections;

        private TcpListener server;
        private bool isRunning = false;
        private Thread listenThread;


        public ServerModel(string ipAddress, int port, int maxConnections)
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
                server = new TcpListener(ip, port);
                server.Start(maxConnections);

                //Esta es la manera en que se manda la informacion del estado al servidor, es como si fuera el console.Write
                OnStatusUpdated("Servidor escuchando en "+ip+":"+port);
                OnStatusUpdated("Esperando conexiones...");
                    
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
        private void OnStatusUpdated(string status)
        {
            ServerStatusUpdated?.Invoke(status);
        }

    }
}
