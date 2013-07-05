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
		private readonly List<NodeConnection> _nodeConnections = new List<NodeConnection>(8);
		public MemoryInventory MemoryInventory;

		private Thread _listenerLoopThread;
		private Thread _clientsControlThread;

		private readonly int _port;
		private readonly string _baseName;
	    internal SQLiteAsyncConnection DB;

		public Bitmessage(bool start = true, string baseName = "bitmessage.sqlite", int port = 8444)
		{
			_baseName = baseName;
			_port = port;
			if (start) Start();
		}

		public void Start()
		{
			bool newDBFile = !File.Exists(_baseName);
			DB = new SQLiteAsyncConnection(_baseName);

			if (newDBFile)
			{
				DB.CreateTableAsync<NetworkAddress>().Wait();
				DB.CreateTableAsync<Payload>().Wait();
				DB.CreateTableAsync<Pubkey>().Wait();
				DB.CreateTableAsync<PrivateKey>().Wait();
				DB.CreateTableAsync<Broadcast>().Wait();
				DB.CreateTableAsync<Msg>().Wait();

				DB.InsertOrReplaceAsync(new NetworkAddress("127.0.0.1", 8444)).Wait();

				//db.InsertAsync(new NetworkAddress("80.69.173.220", 443)).Wait();
				//db.InsertAsync(new NetworkAddress("109.95.105.15", 8443)).Wait();
				//db.InsertAsync(new NetworkAddress("66.65.120.151", 8080)).Wait();
				//db.InsertAsync(new NetworkAddress("76.180.233.38", 8444)).Wait();
				//db.InsertAsync(new NetworkAddress("84.48.88.42", 443)).Wait();
				//db.InsertAsync(new NetworkAddress("74.132.73.137", 443)).Wait();
				//db.InsertAsync(new NetworkAddress("60.242.109.18", 443)).Wait();
			}

			MemoryInventory = new MemoryInventory(DB);

			_listener = new TcpListener(IPAddress.Any, _port);
			try
			{
				_listener.Start();
			}
			catch (Exception e)
			{
				Debug.WriteLine("Порт занят? " + e);
				Debug.WriteLine("Пытаюсь стартовать прослушивание на другом порту");
				_listener = new TcpListener(IPAddress.Any, 0);
				_listener.Start();
				Debug.WriteLine("Сервер стартовал нормально");
			}

			_listenerLoopThread = new Thread(ListenerLoop) { IsBackground = true, Name = "ListenerLoop Server" };
			_listenerLoopThread.Start();

			_clientsControlThread = new Thread(ClientsControlLoop) { IsBackground = true, Name = "ClientsControlLoop" };
			_clientsControlThread.Start();
		}

		internal void DeleteNode(NetworkAddress networkAddress)
		{
			throw new NotImplementedException();
		}

		internal void AddNode(NetworkAddress networkAddress)
		{
			throw new NotImplementedException();
		}

	    #region Dispose

		private bool _disposed;

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

				lock (_nodeConnections)
					foreach (NodeConnection client in _nodeConnections)
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
				NodeConnection incoming = new NodeConnection(this, _listener.AcceptTcpClient());
				Debug.WriteLine("Подключение к серверу c " + incoming.NetworkAddress.HostStreamPort);

				lock (_nodeConnections)
				{
					foreach (var client in _nodeConnections)
						if (client.NetworkAddress.HostStreamPort == incoming.NetworkAddress.HostStreamPort)
							client.Stop();

					_nodeConnections.Add(incoming);
				}
				incoming.Listen();
			}
		}

		private void ClientsControlLoop()
		{
			// проверяю _listener, т.к. по такому же условию заканчивается цикл ListenerLoop
			while (_listener != null)
			{
				lock (_nodeConnections)
				{
					// Удаляю неподключенные клиенты
					for (int i = _nodeConnections.Count - 1; i >= 0; --i)
						if (!_nodeConnections[i].Connected)
							_nodeConnections.RemoveAt(i);
					// Довожу количество подключенных клиентов до 8
					if (_nodeConnections.Count < 8)
					{
						var nodes = DB.Table<NetworkAddress>().OrderBy(x => x.NumberOfBadConnection).ToListAsync().Result;
						foreach (NetworkAddress node in nodes)
						{
							bool find = _nodeConnections.Any(c => c.NetworkAddress.HostStreamPort == node.HostStreamPort);
							if (!find)
							{
								NodeConnection c = new NodeConnection(this, node);
								try
								{
									Debug.WriteLine("Пытаюсь подключиться к " + node.HostStreamPort);
									c.Connect();
								}
								catch (Exception e)
								{
									Debug.WriteLine(node.HostStreamPort + " " + e);
								}
								if (c.Connected)
								{
									Debug.WriteLine("Подключился к " + node.HostStreamPort);
									_nodeConnections.Add(c);
								}
								if (_nodeConnections.Count >= 8) break;
							}
						}
					}
				}
				// Сплю минуту, или если уведомят из Helper.ReadHeaderMagic
				NodeIsDisconnected.WaitOne(60*1000);
			}
		}

	    public AutoResetEvent NodeIsDisconnected = new AutoResetEvent(false);

		#region Events

		public event Payload.EventHandler   NewPayload;
		public event Broadcast.EventHandler ReceiveBroadcast;
		public event Broadcast.EventHandler ReceiveInvalidBroadcast;
		public event Pubkey.EventHandler    ReceivePubkey;
		public event Pubkey.EventHandler    ReceiveInvalidPubkey;
		public event Msg.EventHandler       ReceiveMsg;
		public event Msg.EventHandler       ReceiveInvalidMsg;

		public void OnNewPayload(Payload payload)
		{
			lock (_nodeConnections)
				for (int i = _nodeConnections.Count - 1; i >= 0; --i)
					if (_nodeConnections[i].Connected)
						_nodeConnections[i].OnBitmessageNewPayload(payload);
					else
						_nodeConnections.RemoveAt(i);

			Payload.EventHandler handler = NewPayload;
			if (handler != null) handler(payload);
		}

	    public void OnReceiveBroadcast(Broadcast broadcast)
	    {
		    Broadcast.EventHandler handler = ReceiveBroadcast;
		    if (handler != null) handler(broadcast);
	    }

		public void OnReceiveInvalidBroadcast(Broadcast broadcast)
		{
			Broadcast.EventHandler handler = ReceiveInvalidBroadcast;
			if (handler != null) handler(broadcast);
		}

		public void OnReceivePubkey(Pubkey pubkey)
		{
			Pubkey.EventHandler handler = ReceivePubkey;
			if (handler != null) handler(pubkey);
		}

		public void OnReceiveInvalidPubkey(Pubkey pubkey)
		{
			Pubkey.EventHandler handler = ReceiveInvalidPubkey;
			if (handler != null) handler(pubkey);
		}

		public void OnReceiveMsg(Msg msg)
		{
			Msg.EventHandler handler = ReceiveMsg;
			if (handler != null) handler(msg);
		}

		public void OnReceiveInvalidMsg(Msg msg)
		{
			Msg.EventHandler handler = ReceiveInvalidMsg;
			if (handler != null) handler(msg);
		}

		#endregion Events

		#region see https://bitmessage.org/wiki/API_Reference

		public PrivateKey GeneratePrivateKey(string label = null, bool eighteenByteRipe = false, bool send = false, bool save = true)
		{
			PrivateKey privateKey = new PrivateKey(label, eighteenByteRipe);
			if (save) privateKey.SaveAsync(DB).Wait();
			if (send) privateKey.SendAsync(this);

			return privateKey;
		}

		public IEnumerable<PrivateKey> ListMyAddresses()
		{
			return PrivateKey.GetAll(DB);
		}

		public IEnumerable<Pubkey> ListOtherAddresses()
		{
			return Pubkey.GetAll(DB);
		}

		public PrivateKey CreateRandomAddress(string label, bool eighteenByteRipe = false)
		{
			PrivateKey privateKey = new PrivateKey(label, eighteenByteRipe);
			privateKey.SaveAsync(DB);
			return privateKey;
		}

	    public IEnumerable<Broadcast> GetAllInboxMessages()
	    {
			throw new NotImplementedException();
	    }

		public void TrashMessage(string msgid)
		{
			throw new NotImplementedException();
		}

		public void SendMessage(string fromAddress, string toAddress, string subject, string body, int encodingType = 2)
		{
			Msg msg = new Msg(this)
				          {
							  KeyFrom = fromAddress,
							  KeyTo = toAddress,
					          Body = body,
					          Subject = subject
				          };
			msg.SaveAsync(DB);
			msg.Send();
		}

		public void SendBroadcast(string fromAddress, string subject, string body, int encodingType = 2)
		{
			Broadcast broadcast = new Broadcast(this)
			{
				Key = fromAddress,
				Body = body,
				Subject = subject
			};
			broadcast.SaveAsync(DB);
			broadcast.Send();
		}

		public IEnumerable<Pubkey> Subscriptions(string stream)
		{
			var query =
				DB.Table<Pubkey>()
					.Where(v => v.SubscriptionIndex > 0)
					.Where(v => v.Stream4DB == stream)
					.OrderByDescending(v => v.SubscriptionIndex);
			return query.ToListAsync().Result;
		}

	    public void AddSubscription(Pubkey pubkey)
		{
			if (pubkey.SubscriptionIndex == 0)
			{
				pubkey.SubscriptionIndex = 1;
				pubkey.SaveAsync(DB);
			}
		}

		public void DeleteSubscription(Pubkey pubkey)
		{
			if (pubkey.SubscriptionIndex != 0)
			{
				pubkey.SubscriptionIndex = 0;
				pubkey.SaveAsync(DB);
			}
		}

		#endregion see https://bitmessage.org/wiki/API_Reference
    }
}