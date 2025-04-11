using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    public static bool IsValidIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            Console.WriteLine("Некорректный ввод IP-адреса.");
            return false;
        }

        string[] parts = ip.Split('.');
        if (parts.Length != 4)
        {
            Console.WriteLine("Некорректный ввод IP-адреса.");
            return false;
        }

        try
        {
            foreach (string part in parts)
            {
                int value = int.Parse(part);
                if (value < 0 || value > 255)
                {
                    Console.WriteLine("Некорректный ввод IP-адреса.");
                    return false;
                }
            }
            return true;
        }
        catch
        {
            Console.WriteLine("Некорректный ввод IP-адреса.");
            return false;
        }
    }

    public static bool IsValidPort(string port, out int portNumber)
    {
        portNumber = 0;
        try
        {
            portNumber = int.Parse(port);
            if (portNumber < 1024 || portNumber > 65535)
            {
                Console.WriteLine("Некорректный ввод порта: порт должен принимать значения от 1024 до 65535.");
                return false;
            }
            else return true;
        }
        catch
        {
            Console.WriteLine("Некорректный ввод порта.");
            return false;
        }
    }

    class User
    {
        private const int BUFFER_SIZE = 512;
        private UdpClient udpSocket;
        private string ipUser;
        private string ipServer;
        private int portServer;
        private int portUser;
        private string name;

        public User(string ipUser, string ipServer, int portServer, int portUser, string name)
        {
            this.ipUser = ipUser;
            this.ipServer = ipServer;
            this.portServer = portServer;
            this.portUser = portUser;
            this.name = name;

            try
            {
                udpSocket = new UdpClient(portUser);
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Ошибка создания сокета: {e.Message}");
                Environment.Exit(1);
            }
        }

        public void Start()
        {
            // прием сообщений на фоне
            Thread receiveThread = new Thread(ReceiveMessages);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            // авторизация нового пользователя на сервере
            SendRequest($"init {name}");
        }

        private void ReceiveMessages()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    byte[] data = udpSocket.Receive(ref remoteEndPoint); // ожидание получения сообщения
                    string message = Encoding.UTF8.GetString(data);
                    if (!message.StartsWith($"{name}: "))
                    {
                        Console.WriteLine(message);
                    };
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Удаленный хост разорвал соединение: {e.Message}");
                    Environment.Exit(1);
                }
            }
        }

        public void SendRequest(string data)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                udpSocket.Send(bytes, bytes.Length, ipServer, portServer);
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Ошибка при отправке запроса - недопустимый порт: {e.Message}");
                Environment.Exit(1);
            }
        }

        public void RunInputLoop()
        {
            string userInput;
            while ((userInput = Console.ReadLine()) != "exit")
            {
                SendRequest($"{userInput} {name}");
            }
            if (userInput == "exit") SendRequest($"{userInput} {name}");
        }
    }

    class Server
    {
        private const int BUFFER_SIZE = 512;
        private UdpClient udpSocket;
        private string ip;
        private int port;
        private List<IPEndPoint> users = new List<IPEndPoint>();

        public Server(string ip, int port)
        {
            this.ip = ip;
            this.port = port;

            try
            {
                udpSocket = new UdpClient(port);
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Порт недоступен: {e.Message}");
                Environment.Exit(1);
            }
        }

        public void Start()
        {
            Console.WriteLine($"{GetCurrentTime()}: Сервер запущен: {ip}:{port}");
            ReceiveMessages();
        }

        private void ReceiveMessages()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    byte[] data = udpSocket.Receive(ref remoteEndPoint);
                    string message = Encoding.UTF8.GetString(data);
                    string[] words = message.Split(' ');
                    string name = words[words.Length - 1];
                    string command = string.Join(" ", words, 0, words.Length - 1);

                    if (!users.Contains(remoteEndPoint))
                    {
                        if (command == "init")
                        {
                            users.Add(remoteEndPoint);
                            SendRequest($"Количество пользователей в сети: {users.Count}", remoteEndPoint);
                            Console.WriteLine($"{GetCurrentTime()} Новый пользователь в сети: {name} ({remoteEndPoint.Address})");
                            SendMessages(users, $"Новый пользователь подключился к сети: {name} ({remoteEndPoint.Address})", remoteEndPoint);
                        }
                        continue;
                    }
                     
                    if (command == "exit")
                    {
                        users.Remove(remoteEndPoint);
                        SendMessages(users, $"Пользователь отключился от сети: {name} ({remoteEndPoint.Address})", remoteEndPoint);
                        Console.WriteLine($"{GetCurrentTime()} Пользователь отключился: {name} ({remoteEndPoint.Address})");
                        continue;
                    }

                    string formattedMessage = $"{name}: {command}";
                    SendMessages(users, formattedMessage, remoteEndPoint);
                    Console.WriteLine($"{GetCurrentTime()} {formattedMessage}");
                }
                catch (SocketException)
                {
                    Console.WriteLine("Ошибка при получении запроса!");
                    Environment.Exit(1);
                }
            }
        }

        private void SendMessages(List<IPEndPoint> users, string data, IPEndPoint sender)
        {
            foreach (var user in users)
            {
                if (user.Port != sender.Port || user.Address.Equals(sender.Address))
                {
                    SendRequest(data, user);
                }
            }
        }

        private void SendRequest(string data, IPEndPoint client)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                udpSocket.Send(bytes, bytes.Length, client);
            }
            catch (SocketException)
            {
                Console.WriteLine("Ошибка при отправке запроса!");
                Environment.Exit(1);
            }
        }

        private string GetCurrentTime()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }
    }

    static void Main(string[] args)
    {
        Console.WriteLine("Запустить как сервер (1) или как пользователь (2)?");
        string choice = Console.ReadLine();

        if (choice == "1")
        {
            string ip;
            int port;

            while (true)
            {
                Console.WriteLine("Введите IP-адрес сервера:");
                ip = Console.ReadLine();
                if (IsValidIp(ip))
                    break;
            }

            while (true)
            {
                Console.WriteLine("Введите порт сервера:");
                if (IsValidPort(Console.ReadLine(), out port))
                    break;
            }

            Server server = new Server(ip, port);
            server.Start();
        }
        else if (choice == "2")
        {
            string ipServer, ipUser, name;
            int portServer, portUser;

            while (true)
            {
                Console.WriteLine("Введите IP-адрес сервера::");
                ipServer = Console.ReadLine();
                if (IsValidIp(ipServer))
                    break;
            }

            while (true)
            {
                Console.WriteLine("Введите порт сервера:");
                if (IsValidPort(Console.ReadLine(), out portServer))
                    break;
            }

            while (true)
            {
                Console.WriteLine("Введите ваш IP-адрес:");
                ipUser = Console.ReadLine();
                if (IsValidIp(ipUser))
                    break;
            }

            while (true)
            {
                Console.WriteLine("Введите ваш порт:");
                if (IsValidPort(Console.ReadLine(), out portUser))
                    break;
            }

            Console.WriteLine("Введите ваше имя:");
            name = Console.ReadLine();

            User client = new User(ipUser, ipServer, portServer, portUser, name);
            client.Start();
            client.RunInputLoop();
        }
        else
        {
            Console.WriteLine("Неправильный ввод. Выход...");
            Environment.Exit(1);
        }
    }
}