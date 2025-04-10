using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Server
    {
        private const int BUFFER_SIZE = 512;
        private string ip;
        private int port;
        private Socket udpSocket;
        private static List<IPEndPoint> users = new List<IPEndPoint>();

        public Server()
        {
            Start();
        }

        private void Start()
        {
            while (true)
            {
                Console.WriteLine("Введите IP-адрес для сервера: ");
                string ip = Console.ReadLine();
                if (IsValidIP(ip))
                {
                    this.ip = ip;
                    break;
                }

                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                Console.WriteLine("Введите порт для сервера: ");
                string port = Console.ReadLine();
                this.port = int.Parse(port);

                try
                {
                    udpSocket.Bind(new IPEndPoint(IPAddress.Parse(this.ip), this.port));
                    Console.WriteLine($"Сервер успешно запущен ({this.ip}:{this.port})");
                }
                catch (SocketException e)
                {
                    Console.WriteLine("Порт недоступен.");
                    Environment.Exit(1);
                }
            }
        }

        private async Task RunAsync()
        {
            Console.WriteLine($"Сервер успешно запущен ({ip}:{port})");

            byte[] buffer = new byte[BUFFER_SIZE];
            SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.SetBuffer(buffer, 0, buffer.Length);

            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            receiveArgs.RemoteEndPoint = remoteEndPoint;

            while (true)
            {
                //await Task.Factory.FromAsync(
                  //  (callback, state) => udpSocket.BeginReceiveFrom(
                    //receiveArgs.Buffer,
                    //0,
                    //BUFFER_SIZE,
                    //SocketFlags.None,
                    //ref remoteEndPoint,
                    //callback,
                    //state),
                    //udpSocket.EndReceiveFrom,
                    //null
                //);

                receiveArgs.RemoteEndPoint = remoteEndPoint;
                IPEndPoint address = (IPEndPoint)remoteEndPoint;

                int received = receiveArgs.BytesTransferred;
                string data = Encoding.UTF8.GetString(receiveArgs.Buffer, 0, received);

                string[] words = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string name = words[^1];
                data = string.Join(" ", words[..^1]);

                if (!users.Contains(address))
                {
                    if (data == "init")
                    {
                        users.Add(address);
                        await SendRequest($"Количество пользователей в сети: {users.Count}", address);
                        Console.WriteLine($"Новый пользователь в сети: {address.Address}");
                        await SendMessages(users, $"К сети присоединился новый пользователь: {address.Address}", address);
                    }
                    continue;
                }

                if (data == "exit")
                {
                    users.Remove(address);
                    await SendMessages(users, $"Из сети вышел пользователь {address.Address}", address);
                    Console.WriteLine($"Пользователь вышел из сети: {address.Address}");
                    continue;
                }

                data = $"{name}: {data}";
                await SendMessages(users, data, address);
                Console.WriteLine($"{GetCurrentTime()} {data}");
            }
        }

        private string GetCurrentTime()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        private async Task SendRequest(string data, IPEndPoint client)
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
            catch (SocketException)
            {
                Console.WriteLine("Ошибка при отправке запроса");
                Environment.Exit(1);
            }
        }

        private async Task SendMessages(List<IPEndPoint> recipients, string data, IPEndPoint excludeAddress)
        {
            foreach (var user in recipients)
            {
                if (user.Port != excludeAddress.Port)
                {
                    await SendRequest(data, user);
                }
            }
        }

        public static async Task Main()
        {
            Server server = new Server();
            await server.RunAsync();
        }

        static bool IsValidIP(string ip)
        {
            string[] parts = ip.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            try
            {
                return parts.All(part => int.TryParse(part, out int value) && value >= 0 && value <= 255);
            }
            catch (FormatException)
            {
                Console.WriteLine("Некорректный ввод");
                return false;
            }
        }
    }
}
