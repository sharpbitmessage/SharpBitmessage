using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using SQLite;

namespace bitmessage.network
{
	public class NetworkAddress
	{
	    private DateTime _timeLastSeen = Helper.Epoch;
	    private string _streamHostPort;
	    public NetworkAddress() { }

		public NetworkAddress(string host, UInt16 port)
		{
			Stream = 1;
			Services = 0;
			Host = host;
			Port = port;			
		}

	    [PrimaryKey]
	    public string StreamHostPort
	    {
	        get
	        {
	            if (string.IsNullOrEmpty(_streamHostPort))
	            {
	                _streamHostPort = Stream + " " + Host + ":" + Port;
	            }
	            return _streamHostPort;
	        }
	        set { _streamHostPort = value; }
	    }

	    public DateTime TimeLastSeen
	    {
	        get { return _timeLastSeen; }
	        set
	        {
	            if (value > DateTime.UtcNow)
	                _timeLastSeen = Helper.Epoch;
	            else
	                _timeLastSeen = value;
	        }
	    }

	    public UInt32 Stream { get; set; }

		[Ignore]
		public UInt64 Services { get; set; }

		[MaxLength(20)]
		public string Services4DB
		{
			get { return Services.ToString(CultureInfo.InvariantCulture); }
			set { Services = UInt64.Parse(value); }
		}


		public string Host { get; set; }
		public UInt16 Port { get; set; }
		//public int NumberOfBadConnection { get; set; }

		public override string ToString()
		{
            return StreamHostPort;
		}

		internal static NetworkAddress GetFromAddrList(byte[] addrList, ref int pos)
		{
			var result = new NetworkAddress
				             {
					             TimeLastSeen = addrList.ReadUInt64(ref pos).FromUnix(),
					             Stream = addrList.ReadUInt32(ref pos),
					             Services = addrList.ReadUInt64(ref pos)
				             };
			byte[] ip = addrList.ReadBytes(ref pos, 16);
			result.Host = new IPAddress(ip).ToString();
			if (result.Host.StartsWith("::ffff:"))
			{
				byte[] ipv4 = new byte[4];
				Buffer.BlockCopy(ip, 12, ipv4, 0, 4);
				result.Host = new IPAddress(ipv4).ToString();
			}
			result.Port = addrList.ReadUInt16(ref pos);

			return result;
		}

        public Task<int> SaveAsync(SQLiteAsyncConnection db)
        {
            return db.InsertOrReplaceAsync(this);
        }

	}
}