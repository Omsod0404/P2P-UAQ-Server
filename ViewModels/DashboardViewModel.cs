using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using P2P_UAQ_Server.Models;
using P2P_UAQ_Server.Views;

namespace P2P_UAQ_Server.ViewModels
{
    public class DashboardViewModel:ViewModelBase
    {
        private bool _isViewVisible = true;
        private ServerModel serverModel;

        public bool IsViewVisible
        {
            get
            {
                return _isViewVisible;
            }
            set
            {
                _isViewVisible = value;
                OnPropertyChanged(nameof(IsViewVisible));
            }
        }
   
        private List<string> serverStatusMessages = new List<string>();
        

        public DashboardViewModel(ServerModel serverModel)
        {
            this.serverModel = serverModel;
            serverModel.ServerStatusUpdated += OnServerStatusUpdated;
            
        }

        

        public string AllServerStatusMessages
        {
            get { return string.Join(Environment.NewLine, serverStatusMessages); }
        }

        private void OnServerStatusUpdated(string status)
        {
            
            serverStatusMessages.Add(status);           
            OnPropertyChanged(nameof(AllServerStatusMessages));
        }

        public void TurnOffServer()
        {
            serverModel.StopServer();
        }

    }
}
