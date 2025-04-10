using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Client
    {
        private string IP_client;
        private string IP_server;
        private int port_server;
        private int port_client;
        private Socket udpSocket;
        private const int BUFFER_SIZE = 512;
        private string name;

        public Client(string ipServer, int portServer, string ipClient, int portClient, string clientName)
        {
            this.IP_server = ipServer;
            this.port_server = portServer;
            this.IP_client = ipClient;
            this.port_client = portClient;
            this.name = clientName;

            try
            {
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSocket.Bind(new IPEndPoint(IPAddress.Parse(this.IP_client), this.port_client));
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Ошибка создания сокета: {e.Message}");
                Environment.Exit(1);
            }
        }

        public async Task RunAsync()
        {
            await SendRequest($"init {name}", new IPEndPoint(IPAddress.Parse(IP_server), port_server));

            byte[] buffer = new byte[BUFFER_SIZE];
            SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0)
            };
            receiveArgs.SetBuffer(buffer, 0, buffer.Length);

            while (true)
            {
                try
                {
                    //await Task.Factory.FromAsync(
                      //  (callback, state) => udpSocket.BeginReceiveFrom(receiveArgs.Buffer, 0, BUFFER_SIZE, SocketFlags.None, ref receiveArgs.RemoteEndPoint, callback, state),
                        //udpSocket.EndReceiveFrom,
                        //null
                    //);

                    int received = receiveArgs.BytesTransferred;
                    string data = Encoding.UTF8.GetString(receiveArgs.Buffer, 0, received);
                    Console.WriteLine(data);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Сервер разорвал соединение! {e.Message}");
                    Environment.Exit(1);
                }
            }
        }

        public async Task SendRequest(string data, IPEndPoint client)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs
            {
                RemoteEndPoint = client
            };
            sendArgs.SetBuffer(buffer, 0, buffer.Length);

            try
            {
                await Task.Factory.FromAsync(
                    (callback, state) => udpSocket.BeginSendTo(sendArgs.Buffer, 0, sendArgs.Buffer.Length, SocketFlags.None, sendArgs.RemoteEndPoint, callback, state),
                    udpSocket.EndSendTo,
                    null
                );
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Произошла ошибка при отправке запроса: {e.Message}");
                Environment.Exit(1);
            }
        }

        private static bool IsValidIP(string ip)
        {
            string[] parts = ip.Split('.');
            if (parts.Length != 4)
                return false;

            try
            {
                return parts.All(part => int.TryParse(part, out int value) && value >= 0 && value <= 255);
            }
            catch (FormatException)
            {
                Console.WriteLine("Некорректный ввод IP-адреса");
                return false;
            }
        }

        private static bool IsValidPort(string port)
        {
            try
            {
                if (int.TryParse(port, out int intPort) && intPort >= 1024 && intPort <= 65535)
                    return true;
                return false;
            }
            catch
            {
                Console.WriteLine("Некорректный ввод порта");
                return false;
            }
        }

        public static async Task Main()
        {
            string ip_server = "";
            int port_server = 0;
            string ip_client = "";
            int port_client = 0;
            string name = "";

            while (true)
            {
                Console.WriteLine("Введите IP-адрес сервера: ");
                ip_server = Console.ReadLine();
                if (IsValidIP(ip_server))
                    break;
            }

            while (true)
            {
                Console.WriteLine("Введите порт сервера:");
                string portInput = Console.ReadLine();
                if (IsValidPort(portInput))
                {
                    port_server = int.Parse(portInput);
                    break;
                }
            }

            while (true)
            {
                Console.WriteLine("Введите ваш IP-адрес:");
                ip_client = Console.ReadLine();
                if (IsValidIP(ip_client))
                    break;
            }

            while (true)
            {
                Console.WriteLine("Введите ваш порт:");
                string portInput = Console.ReadLine();
                if (IsValidPort(portInput))
                {
                    port_client = int.Parse(portInput);
                    break;
                }
            }

            Console.WriteLine("Введите ваше имя:");
            name = Console.ReadLine();

            Client client = new Client(ip_server, port_server, ip_client, port_client, name);
            Task clientTask = client.RunAsync();

            string userInput = "";
            while (userInput != "exit")
            {
                userInput = Console.ReadLine();
                userInput = $"{userInput} {name}";
                await client.SendRequest(userInput, new IPEndPoint(IPAddress.Parse(ip_server), port_server));
            }

            await clientTask;
        }
    }
}
