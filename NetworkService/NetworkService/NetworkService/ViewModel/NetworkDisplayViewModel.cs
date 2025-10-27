using GalaSoft.MvvmLight.Messaging;
using NetworkService.Model;
using Notification.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NetworkService.ViewModel
{
    public class NetworkDisplayViewModel : BindableBase
    {
        private ObservableCollection<ServersByType> _allServers;
        public ObservableCollection<ServersByType> AllServers
        {
            get => _allServers;
            set { _allServers = value; OnPropertyChanged(nameof(AllServers)); }
        }

        private Server _selectedServer;
        public Server SelectedServer
        {
            get => _selectedServer;
            set { _selectedServer = value; OnPropertyChanged(nameof(SelectedServer)); }
        }

        public MyICommand<object> MouseDownCommand { get; private set; }
        public MyICommand<object> MouseMoveCommand { get; private set; }
        public MyICommand<object> ResetDragCommand { get; private set; }
        public MyICommand<Canvas> DropCommand { get; private set; }
        public MyICommand<Canvas> ClearCanvasCommand { get; private set; }
        public MyICommand<Canvas> CanvasLoadedCommand { get; private set; }
        public MyICommand<Canvas> MouseDownCanvasCommand { get; private set; }
        public MyICommand<Canvas> MouseMoveCanvasCommand { get; private set; }
        public MyICommand<Tuple<Server, Server>> ConnectServersCommand { get; private set; }
        public MyICommand<Canvas> SetLineCanvasCommand { get; private set; }
        public MyICommand UndoCommand { get; private set; }

        private bool _isDragging;
        private Point _dragStartPoint;

        public static ObservableCollection<CanvasServerPair> CanvasServerPairs { get; private set; }
        private readonly Dictionary<int, Canvas> _canvasDictionary = new Dictionary<int, Canvas>();
        public ObservableCollection<ServerConnection> Connections { get; private set; }
        public ObservableCollection<Line> Lines { get; private set; }
        private Canvas _lineCanvas;

        private string _terminalText;
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

        private Server _droppedServerFromTreeView;
        public Server DroppedServerFromTreeView
        {
            get => _droppedServerFromTreeView;
            set
            {
                _droppedServerFromTreeView = value;
                OnPropertyChanged(nameof(DroppedServerFromTreeView));
                ClearCanvasCommand.RaiseCanExecuteChanged();
            }
        }

        private (Canvas From, Canvas To) _droppedServerFromCanvas;
        public (Canvas From, Canvas To) DroppedServerFromCanvas
        {
            get => _droppedServerFromCanvas;
            set
            { _droppedServerFromCanvas = value;
              OnPropertyChanged(nameof(DroppedServerFromCanvas));
              ClearCanvasCommand.RaiseCanExecuteChanged();
            }
        }

        private ServerConnection _connectionBetweenCanvases;
        public ServerConnection ConnectionBetweenCanvases
        {
            get => _connectionBetweenCanvases;
            set { _connectionBetweenCanvases = value; OnPropertyChanged(nameof(ConnectionBetweenCanvases)); }
        }

        private (Server Server, int CanvasId, List<ServerConnection> Connections) _lastClearedServer;
        public (Server Server, int CanvasId, List<ServerConnection> Connections) LastClearedServer
        {
            get => _lastClearedServer;
            set { _lastClearedServer = value; OnPropertyChanged(nameof(LastClearedServer)); }
        }

        public enum LastActionType { None, DropFromTree, DropFromCanvas, ConnectServers, ClearCanvas }
        private LastActionType _lastAction = LastActionType.None;

        public LastActionType LastAction
        {
            get => _lastAction;
            set
            {
                _lastAction = value;
                OnPropertyChanged(nameof(LastAction));
                UndoCommand?.RaiseCanExecuteChanged();
            }
        }   

        private NotificationManager _notificationManager = new NotificationManager();

        public NetworkDisplayViewModel()
        {
            AllServers = new ObservableCollection<ServersByType>();
            CanvasServerPairs = new ObservableCollection<CanvasServerPair>();
            Connections = new ObservableCollection<ServerConnection>();
            Lines = new ObservableCollection<Line>();

            MouseDownCommand = new MyICommand<object>(OnMouseDown);
            MouseMoveCommand = new MyICommand<object>(OnMouseMove);
            ResetDragCommand = new MyICommand<object>(delegate { ResetDragState(); });
            DropCommand = new MyICommand<Canvas>(OnDrop);
            ClearCanvasCommand = new MyICommand<Canvas>(canvas => OnClearCanvas(canvas));
            CanvasLoadedCommand = new MyICommand<Canvas>(OnCanvasLoaded);
            MouseDownCanvasCommand = new MyICommand<Canvas>(OnCanvasMouseDown);
            MouseMoveCanvasCommand = new MyICommand<Canvas>(OnCanvasMouseMove);
            ConnectServersCommand = new MyICommand<Tuple<Server, Server>>(OnConnectServers);
            SetLineCanvasCommand = new MyICommand<Canvas>(canvas => _lineCanvas = canvas);
            UndoCommand = new MyICommand(OnUndo, CanUndo);

            Messenger.Default.Register<ObservableCollection<Server>>(this, OnServersReceived);
            Messenger.Default.Register<Server>(this, OnServerReceive);
        }

        #region Drag & Drop

        private void ResetDragState()
        {
            SelectedServer = null;
            _isDragging = false;
        }

        private void OnMouseDown(object parameter)
        {
            if (parameter is Server server)
            {
                SelectedServer = server;
                _dragStartPoint = Mouse.GetPosition(Application.Current.MainWindow);
                _isDragging = false;
            }
        }

        private void OnMouseMove(object parameter)
        {
            if (SelectedServer == null) return;
            if (Mouse.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point currentPos = Mouse.GetPosition(Application.Current.MainWindow);
                Vector diff = _dragStartPoint - currentPos;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop(Application.Current.MainWindow, SelectedServer, DragDropEffects.Move);
                    ResetDragState();
                }
            }
        }

        private void OnDrop(Canvas canvas)
        {
            if (canvas == null || SelectedServer == null) return;

            int canvasId = int.Parse(canvas.Tag.ToString());
            var existingPair = CanvasServerPairs.FirstOrDefault(p => p.CanvasId == canvasId);

            if (existingPair != null) // connecting servers
            {
                bool connectionExists = Connections.Any(c =>
                    (c.From.Server == SelectedServer && c.To.Server == existingPair.Server) ||
                    (c.From.Server == existingPair.Server && c.To.Server == SelectedServer));

                if (existingPair.Server != SelectedServer && !connectionExists)
                {
                    ConnectServersCommand.Execute(Tuple.Create(SelectedServer, existingPair.Server));
                    LastAction = LastActionType.ConnectServers;
                    ResetDragState();
                    return;
                }

                ResetDragState();
                return;
            }

            var serverOnOtherCanvas = CanvasServerPairs.FirstOrDefault(p => p.Server == SelectedServer);
            if (serverOnOtherCanvas != null) // move server between canvases
            {
                if (_canvasDictionary.TryGetValue(serverOnOtherCanvas.CanvasId, out Canvas oldCanvas))
                {
                    oldCanvas.Background = Brushes.LightGray;
                    if (oldCanvas.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock oldTextBlock)
                        oldTextBlock.Text = string.Empty;

                    var newPair = new CanvasServerPair { CanvasId = canvasId, Server = SelectedServer };
                    CanvasServerPairs.Add(newPair);
                    UpdateCanvasUI(canvas, SelectedServer);
                    MoveAllConnections(oldCanvas, canvas);

                    DroppedServerFromCanvas = (oldCanvas, canvas);
                    LastAction = LastActionType.DropFromCanvas;
                }

                CanvasServerPairs.Remove(serverOnOtherCanvas);
                return;
            }

            // New drop from TreeView
            var newCanvasPair = new CanvasServerPair { CanvasId = canvasId, Server = SelectedServer };
            CanvasServerPairs.Add(newCanvasPair);
            UpdateCanvasUI(canvas, SelectedServer);

            RemoveServerFromCollection(SelectedServer);
            DroppedServerFromTreeView = SelectedServer;
            LastAction = LastActionType.DropFromTree;

            ResetDragState();
            RedrawLines();
        }

        private void MoveAllConnections(Canvas oldCanvas, Canvas newCanvas)
        {
            var oldPair = CanvasServerPairs.FirstOrDefault(p => p.CanvasId == int.Parse(oldCanvas.Tag.ToString()));
            var newPair = CanvasServerPairs.FirstOrDefault(p => p.CanvasId == int.Parse(newCanvas.Tag.ToString()));
            if (oldPair == null || newPair == null) return;

            foreach (var connection in Connections.ToList())
            {
                if (connection.From == oldPair) connection.From = newPair;
                if (connection.To == oldPair) connection.To = newPair;
            }

            RedrawLines();
        }

        private void RemoveServerFromCollection(Server server)
        {
            foreach (var group in AllServers)
            {
                if (group.Servers.Remove(server)) break;
            }
        }

        private void OnClearCanvas(Canvas canvas, bool suppressNotification = false)
        {
            if (canvas == null) return;
            int canvasId = int.Parse(canvas.Tag.ToString());
            var pair = CanvasServerPairs.FirstOrDefault(p => p.CanvasId == canvasId);
            if (pair != null) ReturnServerToCollection(pair.Server, canvasId);

            LastAction = LastActionType.ClearCanvas;

            if (!suppressNotification && pair != null)
                _notificationManager.Show("Success", "Canvas cleared successfully!", NotificationType.Success, "WindowNotificationArea");
            ClearCanvasCommand.RaiseCanExecuteChanged();
        }


        private void ReturnServerToCollection(Server server, int canvasId)
        {
            if (server == null) return;

            var group = AllServers.FirstOrDefault(g => g.TypeName == server.ServerTypeProperty.Name);
            if (group != null) group.Servers.Add(server);

            var pair = CanvasServerPairs.FirstOrDefault(p => p.CanvasId == canvasId);
            if (pair != null)
            {
                var relatedConnections = Connections.Where(c => c.From.Server == server || c.To.Server == server).ToList();

                CanvasServerPairs.Remove(pair);
                if (_canvasDictionary.TryGetValue(canvasId, out var canvas))
                {
                    canvas.Background = Brushes.LightGray;
                    if (canvas.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock tb) tb.Text = string.Empty;
                }

                RemoveConnectionsForServer(server);
                LastClearedServer = (server, canvasId, relatedConnections);
            }

            RedrawLines();
        }

        #endregion

        #region Canvas UI

        private void OnCanvasLoaded(Canvas canvas)
        {
            if (canvas?.Tag == null) return;

            if (int.TryParse(canvas.Tag.ToString(), out int canvasId))
            {
                _canvasDictionary[canvasId] = canvas;
                var pair = CanvasServerPairs.FirstOrDefault(p => p.CanvasId == canvasId);
                if (pair != null)
                {
                    UpdateCanvasUI(canvas, pair.Server);
                }
            }
        }

        private void UpdateCanvasUI(Canvas canvas, Server server)
        {
            if (canvas == null || server == null) return;

            if (canvas.Children.Count > 0 && canvas.Children[0] is TextBlock tb)
            {
                tb.Text = $"{server.Id} {server.CurrentValue}%";
                tb.Foreground = server.CurrentValue < 45 || server.CurrentValue > 75 ? Brushes.Red : Brushes.Blue;
                tb.FontWeight = FontWeights.Bold;
                Canvas.SetTop(tb, 5);
                tb.HorizontalAlignment = HorizontalAlignment.Center;
            }

            BitmapImage image = new BitmapImage(new Uri(server.ServerTypeProperty.ImagePath, UriKind.Absolute));
            canvas.Background = new ImageBrush(image);
        }

        private void OnCanvasMouseDown(Canvas canvas)
        {
            if (canvas == null) return;
            var pair = CanvasServerPairs.FirstOrDefault(p => p.CanvasId == int.Parse(canvas.Tag.ToString()));
            if (pair != null && pair.Server != null)
            {
                SelectedServer = pair.Server;
                _dragStartPoint = Mouse.GetPosition(Application.Current.MainWindow);
                _isDragging = false;
            }
        }

        private void OnCanvasMouseMove(Canvas canvas)
        {
            if (SelectedServer == null || canvas == null) return;
            if (Mouse.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point currentPos = Mouse.GetPosition(Application.Current.MainWindow);
                Vector diff = _dragStartPoint - currentPos;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop(Application.Current.MainWindow, SelectedServer, DragDropEffects.Copy | DragDropEffects.Move);
                    ResetDragState();
                }
            }
        }

        #endregion

        #region Connections

        private void OnConnectServers(Tuple<Server, Server> tuple)
        {
            var fromPair = CanvasServerPairs.FirstOrDefault(p => p.Server == tuple.Item1);
            var toPair = CanvasServerPairs.FirstOrDefault(p => p.Server == tuple.Item2);
            if (fromPair == null || toPair == null) return;

            var connection = new ServerConnection { From = fromPair, To = toPair };
            Connections.Add(connection);
            DrawConnection(connection);

            ConnectionBetweenCanvases = connection;
            LastAction = LastActionType.ConnectServers;
        }

        private void DrawConnection(ServerConnection connection)
        {
            if (!_canvasDictionary.TryGetValue(connection.From.CanvasId, out Canvas fromCanvas)) return;
            if (!_canvasDictionary.TryGetValue(connection.To.CanvasId, out Canvas toCanvas)) return;

            var fromCenter = fromCanvas.TranslatePoint(
                new Point(fromCanvas.ActualWidth / 2, fromCanvas.ActualHeight / 2),
                _lineCanvas);

            var toCenter = toCanvas.TranslatePoint(
                new Point(toCanvas.ActualWidth / 2, toCanvas.ActualHeight / 2),
                _lineCanvas);

            Line line = new Line
            {
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                X1 = fromCenter.X,
                Y1 = fromCenter.Y,
                X2 = toCenter.X,
                Y2 = toCenter.Y,
                Tag = connection
            };

            Lines.Add(line);
        }

        private void RemoveConnectionsForServer(Server server)
        {
            var toRemove = Lines.Where(l => l.Tag is ServerConnection sc &&
                (sc.From.Server == server || sc.To.Server == server)).ToList();

            foreach (var line in toRemove)
            {
                if (line.Tag is ServerConnection conn) Connections.Remove(conn);
                Lines.Remove(line);
            }
        }

        private void RedrawLines()
        {
            Lines.Clear();
            foreach (var conn in Connections.ToList())
                DrawConnection(conn);
        }

        #endregion

        #region Terminal

        private void ProcessTerminalInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            var trimmed = input.Trim();

            if (!trimmed.EndsWith("c", StringComparison.OrdinalIgnoreCase)) return;

            var idPart = trimmed.Substring(0, trimmed.Length - 1);

            if (!int.TryParse(idPart, out int serverId)) return;

            var pair = CanvasServerPairs.FirstOrDefault(p => p.Server.Id == serverId);

            if (pair != null && _canvasDictionary.TryGetValue(pair.CanvasId, out var canvas))
            {
                OnClearCanvas(canvas, true);
            }
        }


        #endregion

        #region Server Updates

        private void OnServersReceived(ObservableCollection<Server> servers)
        {
            var serverIds = new HashSet<int>(servers.Select(s => s.Id));
            var toRemove = CanvasServerPairs.Where(p => !serverIds.Contains(p.Server.Id)).ToList();

            foreach (var pair in toRemove)
            {
                if (_canvasDictionary.TryGetValue(pair.CanvasId, out var canvas))
                {
                    canvas.Background = Brushes.LightGray;
                    if (canvas.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock tb)
                        tb.Text = string.Empty;
                }

                RemoveConnectionsForServer(pair.Server);
                CanvasServerPairs.Remove(pair);
            }

            var grouped = new ObservableCollection<ServersByType>();
            var assignedIds = new HashSet<int>(CanvasServerPairs.Select(p => p.Server.Id));

            foreach (var typeGroup in servers.GroupBy(s => s.ServerTypeProperty.Name))
            {
                var group = new ServersByType(typeGroup.Key) { TypeName = typeGroup.Key };
                foreach (var server in typeGroup)
                {
                    if (!assignedIds.Contains(server.Id))
                        group.Servers.Add(server);
                }
                grouped.Add(group);
            }

            AllServers = grouped;
            RedrawLines();
        }

        private void OnServerReceive(Server changedServer)
        {
            var pair = CanvasServerPairs.FirstOrDefault(c => changedServer == c.Server);
            if (pair == null || !_canvasDictionary.TryGetValue(pair.CanvasId, out var canvas)) return;

            if (canvas.Children.Count > 0 && canvas.Children[0] is TextBlock tb)
            {
                tb.Text = $"{changedServer.Id} {changedServer.CurrentValue}%";
                tb.Foreground = changedServer.CurrentValue < 45 || changedServer.CurrentValue > 75 ? Brushes.Red : Brushes.Blue;
            }
        }

        #endregion

        #region Undo

        private bool CanUndo()
        {
            return !LastAction.Equals(LastActionType.None);
        }

        private void OnUndo()
        {
            switch (LastAction)
            {
                case LastActionType.ConnectServers:
                    if (ConnectionBetweenCanvases != null)
                    {
                        Connections.Remove(ConnectionBetweenCanvases);
                        RedrawLines();
                        ConnectionBetweenCanvases = null;
                        _notificationManager.Show("Success", "Action undone successfully!", NotificationType.Success, "WindowNotificationArea");
                    }
                    break;

                case LastActionType.DropFromCanvas:
                    if (DroppedServerFromCanvas.From != null && DroppedServerFromCanvas.To != null)
                    {
                        var toCanvasId = int.Parse(DroppedServerFromCanvas.To.Tag.ToString());
                        var fromCanvasId = int.Parse(DroppedServerFromCanvas.From.Tag.ToString());
                        var pair = CanvasServerPairs.FirstOrDefault(p => p.CanvasId == toCanvasId);

                        if (pair != null)
                        {
                            if (_canvasDictionary.TryGetValue(toCanvasId, out var toCanvas))
                            {
                                toCanvas.Background = Brushes.LightGray;
                                if (toCanvas.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock tb)
                                    tb.Text = string.Empty;
                            }

                            var server = pair.Server;
                            CanvasServerPairs.Add(new CanvasServerPair { CanvasId = fromCanvasId, Server = server });
                            if (_canvasDictionary.TryGetValue(fromCanvasId, out var fromCanvas))
                                UpdateCanvasUI(fromCanvas, server);

                            MoveAllConnections(DroppedServerFromCanvas.To, DroppedServerFromCanvas.From);
                            CanvasServerPairs.Remove(pair);
                        }

                        _notificationManager.Show("Success", "Action undone successfully!", NotificationType.Success, "WindowNotificationArea");
                        DroppedServerFromCanvas = (null, null);
                        ClearCanvasCommand.RaiseCanExecuteChanged();
                    }
                    break;

                case LastActionType.DropFromTree:
                    if (DroppedServerFromTreeView != null)
                    {
                        var pair = CanvasServerPairs.FirstOrDefault(c => c.Server == DroppedServerFromTreeView);
                        if (pair != null)
                            OnClearCanvas(_canvasDictionary[pair.CanvasId], true);

                        DroppedServerFromTreeView = null;
                        _notificationManager.Show("Success", "Action undone successfully!", NotificationType.Success, "WindowNotificationArea");
                    }
                    break;

                case LastActionType.ClearCanvas:
                    if (LastClearedServer.Server != null && LastClearedServer.Connections != null)
                    {
                        var server = LastClearedServer.Server;
                        var canvasId = LastClearedServer.CanvasId;
                        var savedConnections = LastClearedServer.Connections;

                        var restoredPair = new CanvasServerPair { CanvasId = canvasId, Server = server };
                        CanvasServerPairs.Add(restoredPair);

                        var group = AllServers.FirstOrDefault(g => g.TypeName == server.ServerTypeProperty.Name);
                        group?.Servers.Remove(server);

                        if (_canvasDictionary.TryGetValue(canvasId, out var canvas))
                            UpdateCanvasUI(canvas, server);

                        foreach (var conn in savedConnections)
                        {
                            if (conn.From.Server == server)
                                conn.From = restoredPair;
                            if (conn.To.Server == server)
                                conn.To = restoredPair;

                            if (!Connections.Contains(conn))
                                Connections.Add(conn);
                        }

                        RedrawLines();
                        LastClearedServer = (null, -1, null);
                        _notificationManager.Show("Success", "Action undone successfully!", NotificationType.Success, "WindowNotificationArea");
                        ClearCanvasCommand.RaiseCanExecuteChanged();
                    }
                    break;
            }

            LastAction = LastActionType.None;
        }


        #endregion
    }
}
