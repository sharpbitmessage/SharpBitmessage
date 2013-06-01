using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using bitmessage.network;
using Version = bitmessage.network.Version;

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

		private readonly MemoryInventory _clientInventory = new MemoryInventory();

		public Client(Bitmessage bitmessage, TcpClient tcpClient)
		{
			_bitmessage = bitmessage;
			_bitmessage.NewPayload += BitmessageNewPayload;
			_tcpClient = tcpClient;
			IPEndPoint ipep = (IPEndPoint) tcpClient.Client.RemoteEndPoint;
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
			_bitmessage.NewPayload += BitmessageNewPayload;
			Node = node;
			_tcpClient = new TcpClient(AddressFamily.InterNetwork);
		}

		private void BitmessageNewPayload(Payload payload)
		{
			if (_clientInventory.Exists(payload.InventoryVector)) return;
			_clientInventory.Insert(payload.InventoryVector);
			BinaryWriter.Send(new Inv(payload.InventoryVector));
		}

		internal void Listen()
		{
			debug("Стартую ListenerLoop");
			_listenerLoopThread = new Thread(ListenerLoop) {IsBackground = true, Name = "ListenerLoop " + Node.HostStreamPort};
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
				Payload payload = new Payload(h.Command, BinaryReader.ReadBytes((int) h.Length));

				byte[] sha512 = payload.Sha512;

				bool checksum = true;

				for (int i = 0; i < 4; ++i)
					checksum = checksum && (sha512[i] == h.Checksum[i]);

				if (checksum && payload.IsValid)
				{
					debug("Command=" + h.Command);

					#region VERSION

					if (h.Command == "version")
					{
						Version v = new Version(payload);
						debug("Подключились с " + v.UserAgent);
						if ((v.Value != 1) && (v.Value != 2))
							Stop("Version = " + v.Value);
						else if (v.Nonce == Version.EightBytesOfRandomDataUsedToDetectConnectionsToSelf)
							Stop("DetectConnectionsToSelf");
						else
							BinaryWriter.Send(new Verack());
					}

						#endregion VERSION

						#region INV

					else if (h.Command == "inv")
					{
						debug("Load inv");
						List<byte[]> buff = new List<byte[]>(payload.Length/32);

						Inv inv = new Inv(payload.SentData);

						foreach (byte[] bytes in inv.Inventory)
							if (!_bitmessage.MemoryInventory.Exists(bytes))
								if (!_clientInventory.Exists(bytes))
								{
									_clientInventory.Insert(bytes);
									buff.Add(bytes);
								}

						if (buff.Count > 0)
						{
							debug("SendGetdata count=" + buff.Count);
							BinaryWriter.Send(new GetData(buff));							
						}
					}

						#endregion

						// *************  PUBKEY   *******************

					else if (h.Command == "getpubkey")
					{
					}

					else if (h.Command == "pubkey")
					{
						payload.SaveAsync(_bitmessage);
						_bitmessage.MemoryInventory.Insert(payload.InventoryVector);

						Pubkey pubkey = new Pubkey(payload);

						if (pubkey.Status == Status.Valid)
							_bitmessage.OnReceivePubkey(pubkey);
						else
							_bitmessage.OnReceiveInvalidPubkey(pubkey);
					}

						// *************  MSG   *******************

					else if (h.Command == "msg")
					{
					}

						// *************  BROADCAST   *******************

					else if (h.Command == "broadcast")
					{
						payload.SaveAsync(_bitmessage);
						_bitmessage.MemoryInventory.Insert(payload.InventoryVector);

						Broadcast broadcast = new Broadcast(payload);

						if (broadcast.Status == Status.Valid)
							_bitmessage.OnReceiveBroadcast(broadcast);
						else
							_bitmessage.OnReceiveInvalidBroadcast(broadcast);
					}

					else if (h.Command == "addr")
					{
						int pos = 0;
						UInt64 numberOfAddressesIncluded = payload.SentData.ReadVarInt(ref pos);
						if ((numberOfAddressesIncluded > 0) && (numberOfAddressesIncluded < 1001))
						{
							if (payload.Length != (pos + (34*(int) numberOfAddressesIncluded)))
								throw new Exception("addr message does not contain the correct amount of data. Ignoring.");

							//bool needToWriteKnownNodesToDisk = false;

							//for(int i=0;i<=(int)numberOfAddressesIncluded;++i)
							//{
							//	!!!  diffrent for 1 or 2 remoteProtocolVersion
							//}
						}
					}
					else if (h.Command == "getdata")
					{
						debug("Load getdata");
						var getData = new GetData(payload.SentData);

						foreach (byte[] hash in getData.Inventory)
							try
							{
								Payload payload4Send = Payload.Find(hash);
								if (payload4Send != null)
									BinaryWriter.Send(payload4Send);
							}
							catch (IOException)
							{
							} // игнорируем проблеммы с сетью. while (ns.CanRead) - завершит поток.

					}
					else if (h.Command == "veract")
					{
						throw new NotImplementedException();
						// отправить Inventory
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
			BinaryWriter.Send(new Version());
			Listen();
		}
	}
}
