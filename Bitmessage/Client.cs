using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
			string prefix = string.IsNullOrEmpty(Thread.CurrentThread.Name)
				                ? Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture)
				                : Thread.CurrentThread.Name;

			Debug.WriteLine("Thread " + prefix + ": " + msg);
		}

		internal readonly Bitmessage _bitmessage;

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
			Send(new Inv(payload.InventoryVector));
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
				#region Read header and payload from network

				Header header;
				Payload payload;
				try
				{
					header = new Header(BinaryReader);
					payload = new Payload(header.Command, BinaryReader.ReadBytes((int)header.Length));
				}
				catch (Exception e)
				{
					Debug.WriteLine("Похоже, что соединение потерено, извещаю об этом поток _bitmessage " + e);
					_bitmessage.MaybeDisconnect.Set();
					break;
				}

				#endregion Read header and payload from network

				byte[] sha512 = payload.Sha512;

				bool checksum = true;

				for (int i = 0; i < 4; ++i)
					checksum = checksum && (sha512[i] == header.Checksum[i]);

				if (checksum && payload.IsValid)
				{
					debug("Command=" + header.Command);

					#region Save payload to Inventory

					if ((header.Command == "msg") || (header.Command == "pubkey") || (header.Command == "broadcast") || (header.Command == "getpubkey"))
					{
						_clientInventory.Insert(payload.InventoryVector);
						payload.SaveAsync(_bitmessage);
					}

					#endregion Save to Inventory

					#region VERSION

					if (header.Command == "version")
					{
						Version v = new Version(payload);
						debug("Подключились с " + v.UserAgent);
						if ((v.Value != 1) && (v.Value != 2))
							Stop("Version = " + v.Value);
						else if (v.Nonce == Version.EightBytesOfRandomDataUsedToDetectConnectionsToSelf)
							Stop("DetectConnectionsToSelf");
						else
							Send(new Verack());
					}

					#endregion VERSION

					#region INV

					else if (header.Command == "inv")
					{
						Inv inputInventory = new Inv(payload.SentData);
						MemoryInventory buff4GetData = new MemoryInventory(inputInventory.Count);

						debug("прислали inv. Inventory.Count=" + inputInventory.Count);

						foreach (byte[] inventoryVector in inputInventory)
						{
							_clientInventory.Insert(inventoryVector);
							if (!_bitmessage.MemoryInventory.Exists(inventoryVector))
								buff4GetData.Insert(inventoryVector);
						}

						if (buff4GetData.Count > 0)
						{
							debug("SendGetdata count=" + buff4GetData.Count);
							Send(new GetData(buff4GetData));
						}
						else
							debug("Всё знаю, не отправляю GetData");
					}

					#endregion

					#region verack

					else if (header.Command == "verack")
					{
						Send(new Inv(_bitmessage.MemoryInventory));
					}

					#endregion

					#region getpubkey

					else if (header.Command == "getpubkey")
					{
					}

					#endregion getpubkey

					#region pubkey

					else if (header.Command == "pubkey")
					{
						Pubkey pubkey = new Pubkey(payload);

						if (pubkey.Status == Status.Valid)
						{
							pubkey.SaveAsync(_bitmessage.DB);
							_bitmessage.OnReceivePubkey(pubkey);
						}
						else
							_bitmessage.OnReceiveInvalidPubkey(pubkey);
					}

					#endregion PUBKEY

					#region msg

					else if (header.Command == "msg")
					{
					}

					#endregion MSG

					#region broadcast

					else if (header.Command == "broadcast")
					{
						Broadcast broadcast = new Broadcast(_bitmessage, payload);

						if (broadcast.Status == Status.Valid)
							_bitmessage.OnReceiveBroadcast(broadcast);
						else
							_bitmessage.OnReceiveInvalidBroadcast(broadcast);
					}

					#endregion BROADCAST

					#region addr

					else if (header.Command == "addr")
					{
						int pos = 0;
						UInt64 numberOfAddressesIncluded = payload.SentData.ReadVarInt(ref pos);
						if ((numberOfAddressesIncluded > 0) && (numberOfAddressesIncluded < 1001))
						{
							if (payload.Length != (pos + (34 * (int)numberOfAddressesIncluded)))
								throw new Exception("addr message does not contain the correct amount of data. Ignoring.");

							//bool needToWriteKnownNodesToDisk = false;

							//for(int i=0;i<=(int)numberOfAddressesIncluded;++i)
							//{
							//	!!!  diffrent for 1 or 2 remoteProtocolVersion
							//}
						}
					}

					#endregion addr

					#region getdata

					else if (header.Command == "getdata")
					{
						debug("Load getdata");
						var getData = new GetData(payload.SentData);

						foreach (byte[] hash in getData.Inventory)
							Payload.SendAsync(this, hash.ToHex(false));
					}

					#endregion getdata

					#region veract

					else if (header.Command == "veract")
					{
						throw new NotImplementedException();
						// отправить Inventory
					}
					#endregion veract

					#region ping

					else if (header.Command == "ping")
					{
						Send(new Pong());
					}

					#endregion ping

					#region pong

					else if (header.Command == "pong")
					{

					}

					#endregion pong

					else debug("unknown command");
				}
				else
					debug("checksum error");
			}
		}

		public bool Connected { get { return _tcpClient.Connected; } }

		public void Connect()
		{
			debug("Подключаюсь к " + Node);
			_tcpClient.Connect(Node.Host, Node.Port);
			Send(new Version());
			Listen();
		}

		public void Send(ICanBeSent message)
		{
			try
			{
				if (BinaryWriter != null)
					lock (BinaryWriter)
					{
						BinaryWriter.Write(message.Magic());
						BinaryWriter.Write(message.СommandBytes());
						BinaryWriter.Write(message.Length());
						BinaryWriter.Write(message.Checksum());
						if (message.SentData != null) BinaryWriter.Write(message.SentData);
						BinaryWriter.Flush();
					}
			}
			catch(Exception e)
			{
				Debug.WriteLine(e);
			}
		}
	}
}
