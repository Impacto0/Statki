using System.Net;
using System.Net.Sockets;

namespace Statki
{
    static class Program
    {
        private static Server _server;

        public static async Task Main(string[] args)
        {
            string ip = "0.0.0.0";
            // ip = Console.ReadLine();
            
            _server = new Server(ip);
            await _server.Run();
        }
    }

    public class Server
    {
        private IPEndPoint _ip;
        private TcpListener _listener;
        private bool _running = false;
        List<Client> _clients = new List<Client>();
        CancellationTokenSource _token = new CancellationTokenSource();

        public Server(string host)
        {
            _ip = new IPEndPoint(IPAddress.Parse(host), 25565);
            
        }

        public async Task Run()
        {
            _listener = new TcpListener(_ip);
            _listener.Start();
            _running = true;
            
            while (_running)
            {
                TcpClient c = await _listener.AcceptTcpClientAsync();
                Client client = new Client(c);
                _clients.Add(client);
                Task clientTask = client.Run();
                clientTask.ContinueWith(t => _clients.Remove(client));
            }
        }
    }

    public class Client
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private int[,] _ships = new int[10, 10];
        private bool _clientSetupComplete = false;
        private bool _clientTurn = false;
        private int[,] _serverShips = new int[10, 10];
        private bool _serverSetupComplete = false;

        public Client(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
        }
        
        public async Task Run()
        {
            StreamReader r = new StreamReader(_stream);
            StreamWriter w = new StreamWriter(_stream);

            while (_client.Connected)
			{
				if (!_clientSetupComplete)
				{
					Task.Run(ServerSetup);
					await w.WriteLineAsync("Setup");
					for (int i = 0; i < 10; i++)
					{
						if (i <= 3)
						{
							await w.WriteLineAsync("Podaj koordynaty " + (i + 1) + " jednomasztowca");
							await w.WriteAsync("> ");
							await w.FlushAsync();
							
							string shipInput = await r.ReadLineAsync();

							(int, int) cords = ParseCordiante(shipInput);
							
							_ships[cords.Item1, cords.Item2] = 1;
						}
						else if(i <= 6)
						{
							await w.WriteLineAsync("Podaj koordynaty " + (i - 3) + " dwumasztowca");
							await w.WriteAsync("> ");
							await w.FlushAsync();
							
							for(int j = 0; j < 2; j++)
							{
								string shipInput = await r.ReadLineAsync();

								(int, int) cords = ParseCordiante(shipInput);
								
								_ships[cords.Item1, cords.Item2] = 2;
							}
						}
						else if(i <= 8)
						{
							await w.WriteLineAsync("Podaj koordynaty " + (i - 6) + " trzymasztowca");
							await w.WriteAsync("> ");
							await w.FlushAsync();
							
							for(int j = 0; j < 3; j++)
							{
								string shipInput = await r.ReadLineAsync();

								(int, int) cords = ParseCordiante(shipInput);
								
								_ships[cords.Item1, cords.Item2] = 3;
							}
						}
						else
						{
							await w.WriteLineAsync("Podaj koordynaty czteromasztowca");
							await w.WriteAsync("> ");
							await w.FlushAsync();
							
							for(int j = 0; j < 4; j++)
							{
								string shipInput = await r.ReadLineAsync();

								(int, int) cords = ParseCordiante(shipInput);
								
								_ships[cords.Item1, cords.Item2] = 4;
							}
						}
					}
					
					_clientSetupComplete = true;

					if (!_serverSetupComplete)
					{
						await w.WriteLineAsync("Czekam na serwer...");
						await w.FlushAsync();

						while (!_serverSetupComplete)
						{
							await Task.Delay(100);
						}
					}
				}

				int win = CheckForWin();

				if (win == 1)
				{
					Console.WriteLine("Wygrałeś!");
					
					await w.WriteLineAsync("Przegrałeś!");
					await w.FlushAsync();
					
					break;
				}
				else if (win == 2)
				{
					Console.WriteLine("Przegrałeś!");
					
					await w.WriteLineAsync("Wygrałeś!");
					await w.FlushAsync();

					break;
				}

				if (_clientTurn)
				{
					await w.WriteLineAsync("Twoja tura");
					
					await w.WriteLineAsync("Twoja plansza:");
					ClientDisplay(w, false);
					await w.WriteLineAsync("Plansza przeciwnika:");
					ServerDisplay(w, true);
					
					await w.WriteAsync("> ");
					
					Console.WriteLine("Tura przeciwnika...");
					
					string input = await r.ReadLineAsync();
					
					if(input == "exit")
					{
						_client.Close();
						break;
					}
					
					(int, int) cords = ParseCordiante(input);
					
					if (_serverShips[cords.Item1, cords.Item2] != 0)
					{
						await w.WriteLineAsync("Trafiony!");
						await w.FlushAsync();

						_serverShips[cords.Item1, cords.Item2] = -1;
					}
					else if(_serverShips[cords.Item1, cords.Item2] == 0)
					{
						await w.WriteLineAsync("Pudło!");
						await w.FlushAsync();
						
						_serverShips[cords.Item1, cords.Item2] = -10;
						_clientTurn = false;
					}
					else if (_serverShips[cords.Item1, cords.Item2] < 0)
					{
						await w.WriteLineAsync("Już tutaj strzelałeś!");
						await w.FlushAsync();
					}
				}
				else
				{
					Console.WriteLine("Twoja tura");

					Console.WriteLine("Twoja plansza:");
					ServerDisplay(w, false);
					
					Console.WriteLine("Plansza przeciwnika:");
					ClientDisplay(w, true);
					
					Console.Write("> ");
					
					await w.WriteLineAsync("Tura przeciwnika...");
					await w.FlushAsync();
					
					string input = Console.ReadLine();

					if (input == "exit")
					{
						_client.Close();
						break;
					}

					(int, int) cords = ParseCordiante(input);
					
					if (_ships[cords.Item1, cords.Item2] != 0)
					{
						Console.WriteLine("Trafiony!");
						
						_ships[cords.Item1, cords.Item2] = -1;
					}
					else if(_ships[cords.Item1, cords.Item2] == 0)
					{
						Console.WriteLine("Pudło!");
						
						_ships[cords.Item1, cords.Item2] = -10;
						_clientTurn = true;
					}
					else if (_ships[cords.Item1, cords.Item2] < 0)
					{
						Console.WriteLine("Już tutaj strzelałeś!");
					}
				}
			}
        }

        private async Task ServerSetup()
        {
	        if (!_serverSetupComplete)
	        {
		        Console.WriteLine("Server setup");
		        for (int i = 0; i < 10; i++)
		        {
			        if (i <= 3)
			        {
				        Console.WriteLine("Podaj koordynaty " + (i + 1) + " jednomasztowca");
				        Console.Write("> ");

				        string shipInput = Console.ReadLine();

				        (int, int) cords = ParseCordiante(shipInput);
							
				        _serverShips[cords.Item1, cords.Item2] = 1;
			        }
			        else if(i <= 6)
			        {
				        Console.WriteLine("Podaj koordynaty " + (i - 3) + " dwumasztowca");
				        Console.Write("> ");
							
				        for(int j = 0; j < 2; j++)
				        {
					        string shipInput = Console.ReadLine();

					        (int, int) cords = ParseCordiante(shipInput);
								
					        _serverShips[cords.Item1, cords.Item2] = 2;
				        }
			        }
			        else if(i <= 8)
			        {
				        Console.WriteLine("Podaj koordynaty " + (i - 6) + " trzymasztowca");
				        Console.Write("> ");
							
				        for(int j = 0; j < 3; j++)
				        {
					        string shipInput = Console.ReadLine();

					        (int, int) cords = ParseCordiante(shipInput);
								
					        _serverShips[cords.Item1, cords.Item2] = 3;
				        }
			        }
			        else
			        {
				        Console.WriteLine("Podaj koordynaty czteromasztowca");
				        Console.Write("> ");
							
				        for(int j = 0; j < 4; j++)
				        {
					        string shipInput = Console.ReadLine();

					        (int, int) cords = ParseCordiante(shipInput);
								
					        _serverShips[cords.Item1, cords.Item2] = 4;
				        }
			        }
		        }
		        
		        _serverSetupComplete = true;
			        
		        if (!_clientSetupComplete)
		        {
			        Console.WriteLine("Czekam na klienta");
				        
			        while (!_clientSetupComplete)
			        {
				        await Task.Delay(100);
			        }
		        }
	        }
        }

        private async Task ClientDisplay(StreamWriter w, bool serverTurn)
        {
	        if (!serverTurn)
	        {
		        await w.WriteLineAsync("   A B C D E F G H I J");
		        await w.WriteLineAsync("   -------------------");

		        for (int i = 0; i < 10; i++)
		        {
			        string line = "";

			        if (i < 9)
				        line += " ";
				        
			        line += (i + 1) + "|";

			        for (int j = 0; j < 10; j++)
			        {
				        if (_ships[j, i] == 0)
				        {
					        line += "  ";
				        }
				        else if (_ships[j, i] > 0)
				        {
					        line += $"{_ships[j, i]} ";
				        }
				        else if (_ships[j, i] < 0 && _ships[j, i] > -10)
				        {
					        line += "X ";
				        }
				        else if (_ships[j, i] == -10)
				        {
					        line += "O ";
				        }
			        }

			        await w.WriteLineAsync(line);
		        }

		        await w.FlushAsync();
	        }
	        else
	        {
		        Console.WriteLine("   A B C D E F G H I J");
		        Console.WriteLine("   -------------------");

		        for (int i = 0; i < 10; i++)
		        {
			        if(i < 9)
				        Console.Write(" ");
			        
			        Console.Write((i + 1) + "|");

			        for (int j = 0; j < 10; j++)
			        {
				        if (_ships[j, i] == 0)
				        {
					        Console.Write("  ");
				        }
				        else if (_ships[j, i] < 0 && _ships[j, i] > -10)
				        {
					        Console.Write("X ");
				        }
				        else if (_ships[j, i] == -10)
				        {
					        Console.Write("O ");
				        }
			        }

			        Console.WriteLine();
		        }
	        }
        }

        private async Task ServerDisplay(StreamWriter w, bool clientTurn)
        {
	        if (!clientTurn)
	        {
		        Console.WriteLine("   A B C D E F G H I J");
		        Console.WriteLine("   -------------------");

		        for (int i = 0; i < 10; i++)
		        {
			        if(i < 9)
				        Console.Write(" ");

			        Console.Write((i + 1) + "|");

			        for (int j = 0; j < 10; j++)
			        {
				        if (_serverShips[j, i] == 0)
				        {
					        Console.Write("  ");
				        }
				        else if (_serverShips[j, i] > 0)
				        {
					        Console.Write($"{_serverShips[j, i]} ");
				        }
				        else if (_serverShips[j, i] < 0 && _serverShips[j, i] > -10)
				        {
					        Console.Write("X ");
				        }
				        else if (_serverShips[j, i] == -10)
				        {
					        Console.Write("O ");
				        }
			        }

			        Console.WriteLine();
		        }
	        }
	        else
	        {
		        await w.WriteLineAsync("   A B C D E F G H I J");
		        await w.WriteLineAsync("   -------------------");

		        for (int i = 0; i < 10; i++)
		        {
			        string line = "";
			        
			        if (i < 9)
				        line += " ";
			        
			        line += (i + 1) + "|";
		        
			        for (int j = 0; j < 10; j++)
			        {
				        if (_serverShips[j, i] == 0)
				        {
					        line += " ";
				        }
				        else if (_serverShips[j, i] < 0 && _serverShips[j, i] > -10)
				        {
					        line += "X";
				        }
				        else if (_serverShips[j, i] == -10)
				        {
					        line += "O";
				        }
			        }
		        
			        await w.WriteLineAsync(line);
		        }

		        await w.FlushAsync();
	        }
        }

        private int CheckForWin()
        {
	        bool serverWin = true;
	        
	        for(int x = 0; x < 10; x++)
	        {
		        for(int y = 0; y < 10; y++)
		        {
			        if (_ships[x, y] > 0)
				        serverWin = false;
		        }
	        }
	        
	        if(serverWin == true)
		        return 1;

	        bool clientWin = true;
	        
	        for(int x = 0; x < 10; x++)
	        {
		        for(int y = 0; y < 10; y++)
		        {
			        if (_serverShips[x, y] > 0)
				        clientWin = false;
		        }
	        }

	        if (clientWin == true)
		        return 2;

	        return 0;
        }

        private (int, int) ParseCordiante(string cordiante)
        {
	        cordiante = cordiante.ToUpper();
            int x = cordiante[0] - 'A';
            int y = int.Parse(cordiante.Substring(1));
            return (x, y - 1);
        }
    }
}

