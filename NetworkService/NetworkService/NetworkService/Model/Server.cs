using GalaSoft.MvvmLight.Messaging;
using NetworkService.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NetworkService.Model
{
    public class Server : ValidationBase, INotifyPropertyChanged
    {
        private int _id;
        private string _serverName, _ipAddress;
        private ServerType _serverType;
        private static int id_counter = 0;
        private double _currentValue;

        public Server(string serverName, string ipAddress, ServerType serverType)
        {
            _id = ++id_counter;
            _serverName = serverName;
            _ipAddress = ipAddress;
            _serverType = serverType;
            _currentValue = 0;
        }

        public int Id { get => _id;}

        public string ServerName
        {
            get => _serverName;
            set
            {
                if (_serverName != value)
                {
                    _serverName = value;
                    OnPropertyChanged("ServerName");
                }
            }
        }

        public string IpAddress
        {   get => _ipAddress;
            set
            {
                if (_ipAddress != value)
                {
                    _ipAddress = value;
                    OnPropertyChanged("IpAddress");
                }
            }
        }
        public ServerType ServerTypeProperty
        {   get => _serverType;
            set
            { 
                if(_serverType != value)
                {
                    _serverType = value;
                    OnPropertyChanged("ServerTypeProperty");
                }
            }
        }

        public double CurrentValue
        {
            get => _currentValue;
            set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                    OnPropertyChanged("CurrentValue");
                    Messenger.Default.Send(this);
                }
            }
        }

        public TypeName TypeName
        {
            get => ServerTypeProperty.Name;
            set
            {
                if (ServerTypeProperty.Name != value)
                {
                    ServerTypeProperty = new ServerType(value);
                    OnPropertyChanged(nameof(TypeName));
                }
            }
        }

        protected override void ValidateSelf()
        {
            if(string.IsNullOrEmpty(this._serverName))
            {
                this.ValidationErrors["ServerName"] = "Server name is required.";
            }

            if (string.IsNullOrEmpty(this._ipAddress))
            {
                this.ValidationErrors["IpAddress"] = "IP Address is required.";
            }
            else
            {
                var ipv4Pattern = @"^((25[0-5]|2[0-4]\d|[01]?\d\d?)\.){3}" +
                          @"(25[0-5]|2[0-4]\d|[01]?\d\d?)$";
                if (!Regex.IsMatch(this._ipAddress, ipv4Pattern))
                {
                    this.ValidationErrors["IpAddress"] = "IP Address must be in format xxx.xxx.xxx.xxx";
                }
            }
        }
    }
}
