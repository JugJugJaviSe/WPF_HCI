using GalaSoft.MvvmLight.Messaging;
using NetworkService.Model;
using Notification.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NetworkService.ViewModel
{
    public class MeasurementGraphViewModel : BindableBase
    {
        private ObservableCollection<Server> _allServers;
        public ObservableCollection<Server> AllServers
        {
            get => _allServers;
            set
            {
                _allServers = value;
                OnPropertyChanged(nameof(AllServers));
            }
        }

        private Server _selectedServer;
        public Server SelectedServer
        {
            get => _selectedServer;
            set
            {
                if (_selectedServer != value)
                {
                    _lastTwoCommands.Item2 = _lastTwoCommands.Item1;
                    _lastTwoCommands.Item1 = value;
                    UndoCommand?.RaiseCanExecuteChanged();

                    _selectedServer = value;
                    OnPropertyChanged(nameof(SelectedServer));

                    DrawCurrentValue();

                    TerminalText = null;
                }
            }
        }

        private Canvas _graphCanvas;
        public Canvas GraphCanvas
        {
            get => _graphCanvas;
            set
            {
                _graphCanvas = value;
                OnPropertyChanged(nameof(GraphCanvas));
                if (_graphCanvas != null)
                {
                    _graphCanvas.SizeChanged += GraphCanvas_SizeChanged;
                }
            }
        }

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
                    UpdateSelectedServerByName(_terminalText);
                }
            }
        }

        private void UpdateSelectedServerByName(string input)
        {
            if (string.IsNullOrEmpty(input) || AllServers == null)
                return;

            if (!input.EndsWith("g", StringComparison.OrdinalIgnoreCase))
                return;
            
            string numberStr = input.Substring(0, input.Length - 1);

            if (int.TryParse(numberStr, out int serverId))
            {
                var server = AllServers.FirstOrDefault(s => s.Id == serverId);
                if (server != null)
                {
                    SelectedServer = server;
                }
            }
        }

        public MyICommand<Canvas> InitializeGraphCommand { get; private set; }
        public MyICommand UndoCommand { get; private set; }

        public MyICommand SelectServerByIdCommand { get; private set; }

        private (Server, Server) _lastTwoCommands;

        private NotificationManager _notificationManager = new NotificationManager();

        public MeasurementGraphViewModel()
        {
            InitializeGraphCommand = new MyICommand<Canvas>(canvas =>
            {
                GraphCanvas = canvas;
            });

            UndoCommand = new MyICommand(OnUndo, CanUndo);

            SelectServerByIdCommand = new MyICommand(() =>
            {
                if (int.TryParse(TerminalText, out int serverId))
                {
                    var server = AllServers.FirstOrDefault(s => s.Id == serverId);
                    if (server != null)
                    {
                        SelectedServer = server;
                        TerminalText = null;
                    }
                }
            });

            Messenger.Default.Register<ObservableCollection<Server>>(this, OnServersReceived);
            Messenger.Default.Register<CanvasServerPair>(this, OnServerReceive);
        }

        private void GraphCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            DrawCurrentValue();
        }

        public void OnServersReceived(ObservableCollection<Server> servers)
        {
            AllServers = servers;
        }

        public void OnServerReceive(CanvasServerPair pair)
        {
            if (pair.Server == SelectedServer)
            {
                DrawCurrentValue();
            }
        }

        private void DrawCurrentValue()
        {
            if (SelectedServer == null || GraphCanvas == null || GraphCanvas.ActualHeight == 0)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                GraphCanvas.Children.Clear();

                double maxHeight = GraphCanvas.ActualHeight - 18;
                double maxValue = 100;

                var logEntries = new List<LogRow>();
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var path = System.IO.Path.Combine(baseDir, "../..", "Logs", "log.txt");
                foreach (var line in File.ReadAllLines(path).Skip(1)) // skip header
                {
                    var parts = line.Split(',');
                    logEntries.Add(new LogRow
                    {
                        Id = int.Parse(parts[0]),
                        CurrentValue = double.Parse(parts[1]),
                        Date = DateTime.Parse(parts[2])
                    });
                }

                var lastValues = logEntries
                    .Where(l => l.Id == SelectedServer.Id)
                    .OrderByDescending(l => l.Date)
                    .Take(5)
                    .Reverse() // oldest first for left-to-right display
                    .ToList();

                double barWidth = 50;
                double spacing = 30;

                for (int i = 0; i < lastValues.Count; ++i)
                {
                    double value = lastValues[i].CurrentValue;
                    double rectHeight = (value / maxValue) * maxHeight;

                    Rectangle rect = new Rectangle
                    {
                        Width = barWidth,
                        Height = rectHeight,
                        Stroke = Brushes.White,
                        StrokeThickness = 1,
                        Fill = (value < 45 || value > 75) ? Brushes.Red : Brushes.Blue
                    };

                    double x = i * (barWidth + spacing) + 50;
                    double y = maxHeight - rectHeight;

                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);

                    GraphCanvas.Children.Add(rect);

                    TextBlock dateLabel = new TextBlock
                    {
                        Text = lastValues[i].Date.ToString("MM/dd HH:mm"),
                        Foreground = Brushes.White,
                        FontSize = 10,
                        TextAlignment = TextAlignment.Center
                    };

                    double textX = x;
                    double textY = maxHeight + 5;

                    Canvas.SetLeft(dateLabel, textX);
                    Canvas.SetTop(dateLabel, textY);

                    GraphCanvas.Children.Add(dateLabel);
                }

                AddGridLines(maxHeight, maxValue);
            });
        }

        private void AddGridLines(double maxHeight, double maxValue)
        {
            for (int i = 0; i <= 10; i++)
            {
                double yPosition = maxHeight - (i * maxHeight / 10);

                Line line = new Line
                {
                    X1 = 0,
                    X2 = GraphCanvas.ActualWidth,
                    Y1 = yPosition,
                    Y2 = yPosition,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 0.5
                };

                GraphCanvas.Children.Add(line);

                TextBlock label = new TextBlock
                {
                    Text = (i * 10).ToString(),
                    Foreground = Brushes.White,
                    FontSize = 10
                };

                Canvas.SetLeft(label, 5);
                Canvas.SetTop(label, yPosition);
                GraphCanvas.Children.Add(label);
            }
        }

        private bool CanUndo()
        {
            return _lastTwoCommands.Item2 != null;
        }

        private void OnUndo()
        {
            if (_lastTwoCommands.Item2 != null)
            {
                SelectedServer = _lastTwoCommands.Item2;
                TerminalText = null;
                _notificationManager.Show("Success", "Action undone successfully!", NotificationType.Success, "WindowNotificationArea");
            }
        }
    }
}
