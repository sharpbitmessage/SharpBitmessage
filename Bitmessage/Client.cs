using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SQLite;
using bitmessage.network;

namespace bitmessage
{
	public class Node
	{
		public Node() { }

		public Node(string host, int port)
		{
			TimeLastSeen = DateTime.MinValue;
			Stream = 1;
			Services = 0; // ???
			Host = host;
			Port = port;
			NumberOfBadConnection = 0;
		}

		[PrimaryKey]
		public string HostStreamPort
		{
			get { return Host + " " + Stream + " " + Port; }
			set { }
		}

		public DateTime TimeLastSeen { get ; set; }
		public int Stream { get; set; }
		public int Services { get; set; }  // ???
		public string Host { get; set; }
		public int Port { get; set; }
		public int NumberOfBadConnection { get; set; } 
	}

	public class Client
	{
		private readonly TcpClient TcpClient;
		public readonly Node Node;
		
		private Thread _listenerLoopThread;

		private BinaryReader _binaryReader;
		private BinaryWriter _binaryWriter;

		internal BinaryReader BinaryReader
		{
			get { return _binaryReader ?? (_binaryReader = new BinaryReader(TcpClient.GetStream())); }
		}

		internal BinaryWriter BinaryWriter
		{
			get { return _binaryWriter ?? (_binaryWriter = new BinaryWriter(TcpClient.GetStream())); }
		}

		public Client(TcpClient tcpClient)
		{
			TcpClient = tcpClient;
			IPEndPoint ipep = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
			Node = new Node
				        {
					        TimeLastSeen = DateTime.UtcNow,
					        Stream = 1,
					        Services = 0, // ???
					        Host = (ipep.Address.ToString()),
					        Port = ipep.Port,
					        NumberOfBadConnection = 0
				        };
		}

		public Client(Node node)
		{
			Node = node;
			TcpClient = new TcpClient(AddressFamily.InterNetwork);
		}

		internal void Listen()
		{
			Debug.WriteLine("Стартую ListenerLoop");
			_listenerLoopThread = new Thread(new ThreadStart(ListenerLoop)) { IsBackground = true, Name = "ListenerLoop "+ Node.HostStreamPort };
			_listenerLoopThread.Start();			
		}

		internal void Stop(string msg = null)
		{
			if (!string.IsNullOrEmpty(msg))
				Debug.WriteLine(msg);
			Debug.WriteLine("Stop");
			TcpClient.Close();
		}

		private void ListenerLoop()
		{
			NetworkStream ns = TcpClient.GetStream();
			while (ns.CanRead)
			{
				Header h = BinaryReader.ReadHeader();
				Debug.WriteLine("ListenerLoop Command=" + h.Command);
				if (h.Command == "version")
				{
					network.Version v = BinaryReader.ReadVersion();
					if (v.Value != 1)
						Stop("Version = " + v.Value);
					else if (v.Nonce == network.Version.EightBytesOfRandomDataUsedToDetectConnectionsToSelf)
						Stop("DetectConnectionsToSelf");
				}
			}
		}

		public bool Connected { get { return TcpClient.Connected; } }

		public void Connect()
		{
			TcpClient.Connect(Node.Host, Node.Port);
			BinaryWriter.WriteVersion(new network.Version());
			Listen();
		}
	}
}
