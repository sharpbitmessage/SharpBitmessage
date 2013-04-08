using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using bitmessage.network;

namespace bitmessage
{
	public class Client
	{
		private void debug(string msg)
		{
			Debug.WriteLine(Thread.CurrentThread.Name + " " + msg);
		}

		private readonly Bitmessage _bitmessage;

		private readonly TcpClient _tcpClient;
		public readonly Node Node;
		
		private Thread _listenerLoopThread;

		private BinaryReader _binaryReader;
		private BinaryWriter _binaryWriter;

		internal BinaryReader BinaryReader
		{
			get { return _binaryReader ?? (_binaryReader = new BinaryReader(_tcpClient.GetStream())); }
		}

		internal BinaryWriter BinaryWriter
		{
			get { return _binaryWriter ?? (_binaryWriter = new BinaryWriter(_tcpClient.GetStream())); }
		}

		public Client(Bitmessage bitmessage, TcpClient tcpClient)
		{
			_bitmessage = bitmessage;
			_tcpClient = tcpClient;
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

		public Client(Bitmessage bitmessage, Node node)
		{
			_bitmessage = bitmessage;
			Node = node;
			_tcpClient = new TcpClient(AddressFamily.InterNetwork);
		}

		internal void Listen()
		{
			debug("Стартую ListenerLoop");
			_listenerLoopThread = new Thread(ListenerLoop) { IsBackground = true, Name = "ListenerLoop "+ Node.HostStreamPort };
			_listenerLoopThread.Start();			
		}

		internal void Stop(string msg = null)
		{
			if (!string.IsNullOrEmpty(msg))
				debug(msg);
			debug("Stop");
			_tcpClient.Close();
		}

		private void ListenerLoop()
		{
			NetworkStream ns = _tcpClient.GetStream();
			while (ns.CanRead)
			{
				Header h = BinaryReader.ReadHeader();
				byte[] payload = BinaryReader.ReadBytes((int) h.Length);

				byte[] sha512 = new SHA512Managed().ComputeHash(payload);

				bool checksum = true;

				for (int i = 0; i < 4; ++i)
					checksum = checksum && (sha512[i] == h.Checksum[i]);

				if (checksum)
				{
					debug("Command=" + h.Command);
					
					#region VERSION

					if (h.Command == "version")
					{
						network.Version v = payload.ToVersion();
						if (v.Value != 1)
							Stop("Version = " + v.Value);
						else if (v.Nonce == network.Version.EightBytesOfRandomDataUsedToDetectConnectionsToSelf)
							Stop("DetectConnectionsToSelf");
						debug("Подключились с " + v.UserAgent);

						BinaryWriter.SendVerack();
					}

					#endregion VERSION

					#region INV

					else if (h.Command == "inv")
					{
						debug("Load inv");
						List<byte[]> buff = new List<byte[]>(payload.Length/32);
						buff.AddRange(payload.ToInv().Where(item => _bitmessage.Inventory.Get(item) == null));
						if (buff.Count > 0)
						{
							debug("SendVerack count=" + buff.Count);
							BinaryWriter.SendGetdata(buff);
						}
					}

					#endregion

					// *************  PUBKEY   *******************

					else if (h.Command == "pubkey")
					{
			
					}	

					// *************  MSG   *******************

					else if (h.Command == "msg")
					{
					}

					// *************  BROADCAST   *******************

					else if ((h.Command == "broadcast") && (payload.Length > 65) && payload.IsValid())
					{
						_bitmessage.Inventory.Insert(MsgType.Broadcast, payload);
					}
				}
				else
					debug("checksum error");
			}
		}

		public bool Connected { get { return _tcpClient.Connected; } }

		public void Connect()
		{
			_tcpClient.Connect(Node.Host, Node.Port);
			BinaryWriter.WriteVersion(new network.Version());
			Listen();
		}
	}
}
