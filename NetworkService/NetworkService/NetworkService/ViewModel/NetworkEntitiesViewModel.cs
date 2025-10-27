using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Messaging;
using NetworkService.Helpers;
using NetworkService.Model;
using Notification.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace NetworkService.ViewModel
{
    public class NetworkEntitiesViewModel : BindableBase
    {
        private Server _selectedServer;
        bool _isNameFilterSelected = true;
        public ICollectionView FilteredView { get; set; }
        private string _filterText;
        private string _terminalText;
        private Server _serverForUndo;
        private NotificationManager _notificationManager = new NotificationManager();

        public Server SelectedServer
        {
            get => _selectedServer;
            set
            {
                if (_selectedServer != value)
                {
                    _selectedServer = value;
                    OnPropertyChanged(nameof(SelectedServer));
                    DeleteCommand.RaiseCanExecuteChanged();  //calls CanDelete
                }
            }
        }

        public bool IsNameFilterSelected
        {
            get => _isNameFilterSelected;
            set
            {
                if (_isNameFilterSelected != value)
                {
                    _isNameFilterSelected = value;
                    OnPropertyChanged(nameof(IsNameFilterSelected));
                    FilteredView.Refresh();
                }
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText != value)
                {
                    _filterText = value;
                    OnPropertyChanged(nameof(FilterText));
                    FilteredView.Refresh();
                }
            }
        }

        public string TerminalText
        {
            get => _terminalText;
            set
            {
                if (_terminalText != value)
                {
                    _terminalText = value;
                    OnPropertyChanged(nameof(TerminalText));
                    ProcessTerminalInput(_terminalText);
                }
            }
        }

        public Server ServerForUndo
        {
            get => _serverForUndo;
            set
            {
                if (_serverForUndo != value)
                {
                    _serverForUndo = value;
                    OnPropertyChanged(nameof(ServerForUndo));
                    UndoCommand?.RaiseCanExecuteChanged();  // calls CanUndo
                }
            }
        }

        private Server _currentServer;
        public Server CurrentServer
        {
            get => _currentServer;
            set
            {
                _currentServer = value;
                OnPropertyChanged(nameof(CurrentServer));
            }
        }

        public MyICommand AddCommand { get; set; }

        public MyICommand DeleteCommand { get; set; }

        public MyICommand UndoCommand { get; set; }

        private enum LastActionType { None, Add, Delete }
        private LastActionType _lastAction = LastActionType.None;

        private bool _isAddSectionExpanded = false;
        private bool _isManagementSectionExpanded = false;
        private bool _isTerminalSectionExpanded = false;

        public bool IsAddSectionExpanded
        {
            get => _isAddSectionExpanded;
            set => SetProperty(ref _isAddSectionExpanded, value);
        }

        public bool IsManagementSectionExpanded
        {
            get => _isManagementSectionExpanded;
            set => SetProperty(ref _isManagementSectionExpanded, value);
        }

        public bool IsTerminalSectionExpanded
        {
            get => _isTerminalSectionExpanded;
            set => SetProperty(ref _isTerminalSectionExpanded, value);
        }

        public ICommand ToggleAddSectionCommand { get; }
        public ICommand ToggleManagementSectionCommand { get; }
        public ICommand ToggleTerminalSectionCommand { get; }

        public NetworkEntitiesViewModel()
        {
            LoadServers();
            FilteredView = CollectionViewSource.GetDefaultView(Servers);
            FilteredView.Filter = FilterServers;
            AddCommand = new MyICommand(onAdd);
            DeleteCommand = new MyICommand(onDelete, CanDelete);
            UndoCommand = new MyICommand(OnUndo, CanUndo);
            CurrentServer = new Server("", "", new ServerType(TypeName.Web));

            ToggleAddSectionCommand = new MyICommand(() => IsAddSectionExpanded = !IsAddSectionExpanded);
            ToggleManagementSectionCommand = new MyICommand(() => IsManagementSectionExpanded = !IsManagementSectionExpanded);
            ToggleTerminalSectionCommand = new MyICommand(() => IsTerminalSectionExpanded = !IsTerminalSectionExpanded);
        }

        public ObservableCollection<Server> Servers { get; set; }

        public void LoadServers()
        {
            Servers = new ObservableCollection<Server>();
            Servers.Add(new Server("MojServer", "192.323.212.121", new ServerType(TypeName.Web)));
            Servers.Add(new Server("MojServer222", "192.323.212.121", new ServerType(TypeName.Web)));
            Servers.Add(new Server("MojServer1", "192.323.212.222", new ServerType(TypeName.File)));
            Servers.Add(new Server("MojServer2", "192.323.212.333", new ServerType(TypeName.Database)));
        }

        private void onAdd()
        {
            CurrentServer.Validate();
            if (!CurrentServer.IsValid)
            {
                _notificationManager.Show("Error", "Please fix validation errors.", NotificationType.Error, "WindowNotificationArea");
                return;
            }

            var newServer = new Server( CurrentServer.ServerName, CurrentServer.IpAddress, CurrentServer.ServerTypeProperty);
            Servers.Add(newServer);
            Messenger.Default.Send(Servers);
            ResetFormFields();
            RestartSimulator();
            _lastAction = LastActionType.Add;
            ServerForUndo = newServer;

            _notificationManager.Show("Success", $"Server created successfully!", NotificationType.Success, "WindowNotificationArea");
        }

        private void ResetFormFields()
        {
            CurrentServer = new Server("", "", new ServerType(TypeName.Web));
        }

        private void onDelete()
        {
            var result = MessageBox.Show("Are you sure you want to delete?", "Confirm Delete", MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                return;
            }

            ServerForUndo = SelectedServer;
            Servers.Remove(SelectedServer);
            Messenger.Default.Send(Servers);
            RestartSimulator();
            _lastAction = LastActionType.Delete;
            _notificationManager.Show("Success", $"Server deleted successfully!", NotificationType.Success, "WindowNotificationArea");
        }

        private bool CanDelete()
        {
            return SelectedServer != null;
        }

        private void RestartSimulator()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("MeteringSimulator"))
                {
                    process.Kill();
                }

                string relativePath = @"../../../../../MeteringSimulator/MeteringSimulator/bin/Debug/MeteringSimulator.exe";
                string exePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath));

                Process.Start(exePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to restart simulator: " + ex.Message);
            }
        }

        private bool FilterServers(object obj)
        {
            if (obj is Server server)
            {
                if (string.IsNullOrWhiteSpace(FilterText))
                    return true;

                if (IsNameFilterSelected)
                    return server.ServerName.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0;
                else
                    return server.ServerTypeProperty.Name.ToString().IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        public IEnumerable<TypeName> ServerTypes
        {
            get { return Enum.GetValues(typeof(TypeName)).Cast<TypeName>(); }
        }

        private void ProcessTerminalInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;

            var trimmedInput = input.Trim();

            if (!trimmedInput.EndsWith("d", StringComparison.OrdinalIgnoreCase))
                return;
            
            var idPart = trimmedInput.Substring(0, trimmedInput.Length - 1);

            if (!int.TryParse(idPart, out int serverId))
                return;

            var server = Servers.FirstOrDefault(s => s.Id == serverId);
            if (server != null)
            {
                var result = MessageBox.Show("Are you sure you want to delete?", "Confirm Delete", MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }

                ServerForUndo = server;
                Servers.Remove(server);
                Messenger.Default.Send(Servers);
                RestartSimulator();
                _lastAction = LastActionType.Delete;
                _notificationManager.Show("Success", $"Server deleted successfully!", NotificationType.Success, "WindowNotificationArea");
            }
        }

        private bool CanUndo()
        {
            return ServerForUndo != null;
        }

        private void OnUndo()
        {
            switch(_lastAction)
            {
                case LastActionType.Add:
                {
                    Servers.Remove(ServerForUndo);
                    Messenger.Default.Send(Servers);
                    RestartSimulator();
                    _notificationManager.Show("Success", $"Action undone successfully!", NotificationType.Success, "WindowNotificationArea");
                    _lastAction = LastActionType.None;
                    ServerForUndo = null;
                    break;
                }
                case LastActionType.Delete:
                {
                    Servers.Add(ServerForUndo);
                    Messenger.Default.Send(Servers);
                    RestartSimulator();
                    _notificationManager.Show("Success", $"Action undone successfully!", NotificationType.Success, "WindowNotificationArea");
                    _lastAction = LastActionType.None;
                    ServerForUndo = null;
                    break;
                }
            }
        }

    }
}
