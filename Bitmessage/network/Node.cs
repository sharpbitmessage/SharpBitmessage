using System;
using SQLite;

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
		}

		public DateTime TimeLastSeen { get ; set; }
		public int Stream { get; set; }
		public int Services { get; set; }  // ???
		public string Host { get; set; }
		public int Port { get; set; }
		public int NumberOfBadConnection { get; set; } 
	}
}