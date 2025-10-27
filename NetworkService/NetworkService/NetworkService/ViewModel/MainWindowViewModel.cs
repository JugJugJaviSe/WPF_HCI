using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Messaging;
using NetworkService.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NetworkService.ViewModel
{
    public class MainWindowViewModel : BindableBase
    {
        private NetworkDisplayViewModel networkDisplayViewModel;
        private NetworkEntitiesViewModel networkEntitiesViewModel;
        private MeasurementGraphViewModel measurementGraphViewModel;
        private BindableBase currentViewModel;

        public BindableBase CurrentViewModel
        {
            get {  return currentViewModel; }
            set
            {
                SetProperty(ref currentViewModel, value);
            }
        }

        public MyICommand<string> NavCommand { get; private set; }
        public MyICommand ExitCommand { get; }

        public MainWindowViewModel()
        {
            networkDisplayViewModel = new NetworkDisplayViewModel();
            networkEntitiesViewModel = new NetworkEntitiesViewModel();
            measurementGraphViewModel = new MeasurementGraphViewModel();

            createListener(); //Povezivanje sa serverskom aplikacijom
            CurrentViewModel = networkEntitiesViewModel;
            NavCommand = new MyICommand<string>(OnNav);
            ExitCommand = new MyICommand(ExecuteExit);

            Messenger.Default.Send(networkEntitiesViewModel.Servers);
        }

        private void ExecuteExit()
        {
            MessageBoxResult result = MessageBox.Show("Are you sure you want to exit?", "ConfirmExit", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if(result == MessageBoxResult.Yes)
                System.Windows.Application.Current.Shutdown();
        }

        private void OnNav(string destination)
        {
            switch (destination)
            {
                case "NetworkEntities":
                    CurrentViewModel = networkEntitiesViewModel;
                    break;
                case "NetworkDisplay":
                    CurrentViewModel = networkDisplayViewModel;
                    break;
                case "MeasurementGraph":
                    CurrentViewModel = measurementGraphViewModel;
                    break;
                case "Exit":
                    ExecuteExit();
                    break;
            }
        }

        private void createListener()
        {
            var tcp = new TcpListener(IPAddress.Any, 25675);
            tcp.Start();

            var listeningThread = new Thread(() =>
            {
                while (true)
                {
                    var tcpClient = tcp.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(param =>
                    {
                        //Prijem poruke
                        NetworkStream stream = tcpClient.GetStream();
                        string incomming;
                        byte[] bytes = new byte[1024];
                        int i = stream.Read(bytes, 0, bytes.Length);
                        //Primljena poruka je sacuvana u incomming stringu
                        incomming = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        //Ukoliko je primljena poruka pitanje koliko objekata ima u sistemu -> odgovor
                        if (incomming.Equals("Need object count"))
                        {
                            //Response
                            /* Umesto sto se ovde salje count.ToString(), potrebno je poslati 
                             * duzinu liste koja sadrzi sve objekte pod monitoringom, odnosno
                             * njihov ukupan broj (NE BROJATI OD NULE, VEC POSLATI UKUPAN BROJ)
                             * */
                            int serverCount = networkEntitiesViewModel.Servers.Count;
                            Byte[] data = System.Text.Encoding.ASCII.GetBytes(serverCount.ToString());
                            stream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            Console.WriteLine(incomming); // Example: "Entitet_1:272"

                            string[] parts = incomming.Split(':');

                                string entityPart = parts[0]; // "Entitet_1"
                                string valuePart = parts[1];  // "272"

                                if (entityPart.StartsWith("Entitet_"))
                                {
                                    string idString = entityPart.Substring(8);
                                    if (int.TryParse(idString, out int entityId) && double.TryParse(valuePart, out double newValue))
                                    {
                                    UpdateServerValue(entityId, newValue);
                                    }
                                }

                        }

                    }, null);
                }
            });

            listeningThread.IsBackground = true;
            listeningThread.Start();
        }

        private void UpdateServerValue(int entityId, double newValue)
        {
            if (entityId >= 0 && entityId < networkEntitiesViewModel.Servers.Count)
            {
                var serverToUpdate = networkEntitiesViewModel.Servers[entityId];

                if (serverToUpdate != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        serverToUpdate.CurrentValue = newValue;
                        NewLog(serverToUpdate);
                        CanvasServerPair helper = new CanvasServerPair { CanvasId = 0, Server = serverToUpdate }; 
                        Messenger.Default.Send(helper);
                    });
                }
            }
            else
            {
                Console.WriteLine($"Server with ID {entityId} not found!");
            }
        }

        private void NewLog(Server updatedServer)
        {
            string relativePath = @"../../Logs/Log.txt";
            string filePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath));

            string logDirectory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            string newLine = $"{updatedServer.Id},{updatedServer.CurrentValue},{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            bool fileExists = File.Exists(filePath);

            using (StreamWriter sw = new StreamWriter(filePath, append: true))
            {
                if (!fileExists)
                {
                    string firstLine = "Id,CurrentValue,Date";
                    sw.WriteLine(firstLine);
                }

                sw.WriteLine(newLine);
            }

        }
    }
}
