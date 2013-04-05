using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SQLite;
using bitmessage.network;

namespace bitmessage
{
    public class Bitmessage:IDisposable
    {
		private TcpListener _listener;
		private readonly List<Client> _clients = new List<Client>(8);
	    internal Inventory Inventory;

		private readonly Thread _listenerLoopThread;
		private readonly Thread _clientsControlThread;

		private string _baseName;
		private int _port;

		public SQLiteAsyncConnection GetConnection()
		{
			return new SQLiteAsyncConnection(_baseName);
		}

		public Bitmessage(string baseName = "bitmessage.sqlite", int port = 8444)
		{
			_baseName = baseName;
			_port = port;

			if (!File.Exists(baseName))
			{
				var db = GetConnection();
				db.CreateTableAsync<Node>().Wait();
				db.CreateTableAsync<Inv>().Wait();

				db.InsertAsync(new Node("127.0.0.1", 8444)).Wait();

				//db.InsertAsync(new Node("80.69.173.220", 443)).Wait();
				//db.InsertAsync(new Node("109.95.105.15", 8443)).Wait();
				//db.InsertAsync(new Node("66.65.120.151", 8080)).Wait();
				//db.InsertAsync(new Node("76.180.233.38", 8444)).Wait();
				//db.InsertAsync(new Node("84.48.88.42", 443)).Wait();
				//db.InsertAsync(new Node("74.132.73.137", 443)).Wait();
				//db.InsertAsync(new Node("60.242.109.18", 443)).Wait();
			}

			Inventory = new Inventory(this);

			_listener = new TcpListener(IPAddress.Any, _port);
			try
			{
				_listener.Start();
			}
			catch (Exception e)
			{
				Debug.WriteLine("Порт занят? "+e);
				Debug.WriteLine("Пытаюсь стартовать прослушивание на другом порту");
				_listener = new TcpListener(IPAddress.Any, 0);
				_listener.Start();
				Debug.WriteLine("Сервер стартовал нормально");
			}

			_listenerLoopThread = new Thread(new ThreadStart(ListenerLoop)) { IsBackground = true, Name = "ListenerLoop Server" };
			_listenerLoopThread.Start();

			ClientsControlLoop();
			
				_clientsControlThread = new Thread(new ThreadStart(ClientsControlLoop)) { IsBackground = true, Name = "ClientsControlLoop" };
			_clientsControlThread.Start();
		}

	    #region Dispose

		private bool _disposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (_listener != null)
					_listener.Stop();
				_listener = null;

				foreach (Client client in _clients)
					client.Stop();
				
				_disposed = true;
			}
		}

		~Bitmessage() { Dispose(false); }

		#endregion Dispose

		private void ListenerLoop()
		{
			while (_listener != null)
			{
				Client c = new Client(this, _listener.AcceptTcpClient());
				Debug.WriteLine("Подключение к серверу c " + c.Node.HostStreamPort);
				_clients.Add(c); // TODO проверка, что данного клиента ещё нет в списке, иначе заменять существующего?	
				GetConnection().InsertAsync(c.Node).Wait();

				c.Listen();
			}
		}

		private void ClientsControlLoop()
		{
			// проверяю _listener, т.к. по такому же условию заканчивается цикл ListenerLoop
			while (_listener != null)
			{
				// Удаляю неподключенные клиенты
				for (int i = _clients.Count - 1; i >= 0; --i)
					if (!_clients[i].Connected)
						_clients.RemoveAt(i);
				// Довожу количество подключенных клиентов до 8
				if (_clients.Count<8)
				{
					var conn = GetConnection();
					var task = conn.Table<Node>().OrderBy(x => x.NumberOfBadConnection).ToListAsync();
					task.Wait();
					var nodes = task.Result;
					foreach (Node node in nodes)
					{
						bool find = _clients.Any(c => c.Node.HostStreamPort == node.HostStreamPort);
						if(!find)
						{
							Client c = new Client(this, node);
							try
							{
								Debug.WriteLine("Пытаюсь подключиться к "+node.HostStreamPort);
								c.Connect();
							}
							catch(Exception e)
							{
								Debug.WriteLine(node.HostStreamPort + " " + e);
							}
							if (c.Connected)
							{
								Debug.WriteLine("Подключился к " + node.HostStreamPort);
								_clients.Add(c);
							}
							if (_clients.Count >= 8) break;
						}
					}
				}
				// Сплю минуту, или если уведомят из Helper.ReadHeaderMagic
				Helper.MaybeDisconnect.WaitOne(60*1000);
			}
		}

		#region see https://bitmessage.org/wiki/API_Reference

		public IEnumerable<Address> listAddresses()
		{
			throw new NotImplementedException();
		}

		public Address createRandomAddress(string label, bool eighteenByteRipe = false)
		{
			throw new NotImplementedException();
		}

	    public IEnumerable<Message> getAllInboxMessages()
	    {
			throw new NotImplementedException();
	    }

		public void trashMessage(string msgid)
		{
			throw new NotImplementedException();
		}

		public void sendMessage(Address toAddress, Address fromAddress,string subject,string message, int encodingType = 2)
		{
			throw new NotImplementedException();
		}

		public void sendBroadcast(Address toAddress, Address fromAddress, string subject, string message, int encodingType = 2)
		{
			throw new NotImplementedException();
		}

		#endregion see https://bitmessage.org/wiki/API_Reference
	}
}
