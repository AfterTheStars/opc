using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PLC.BaseDriver
{
 
	public class BasicTCP:IBasicClient
	{
		public TcpClient Client { get; set; }
		public NetworkStream Stream { get; set; }

		public string IP { get; set; }
		public int Port { get; set; }
		 
		public static int LocalPort { get; set; } = -1;//是否指定固定本地端口

		public int Timeout { get; set; }

		public object _lock { get; set; } = new object();
		public Socket socket
		{
			get {
				if (Client != null)
				{
					return Client.Client;
				}
				return null;
			}
		}
		public  async Task<bool> ConnectAsync(string rIP, int rPort, int timeOut = 5000)
		{
			bool conn = false;
			this.Timeout = timeOut;
			this.IP = rIP;
			this.Port = rPort;
			
			if (await PingCheckAsync(rIP, (int)timeOut,rPort))
			{
				if (LocalPort > 0)
				{
					IPEndPoint ipep = new IPEndPoint(IPAddress.Any, LocalPort);
					Client = new TcpClient(ipep);

				}
				else
				{
					this.Client = new TcpClient();
				
				}
		 
				Client.SendTimeout = timeOut;
				Client.ReceiveTimeout = timeOut;
				await Client.ConnectAsync(rIP, rPort);
				Stream = Client.GetStream();
				conn = true;
				Console.WriteLine(Client.Client.RemoteEndPoint.ToString()+" Tcp连接成功");
			}
			return conn;
		}

		public  async Task<bool> ReConnectAsync()
		{
			this.Close();
			if (!string.IsNullOrEmpty(IP) && Port > 0)
			{
				return await ConnectAsync(this.IP, this.Port, this.Timeout);
			}
			return false;
		}

		public async Task<bool> PingCheckAsync(string ip, int timeOut,int port=0)
		{
			if (port > 0)
			{
				return checkPortEnable(ip, port);
			}
			Ping ping = new Ping();
			PingReply pingReply = await ping.SendPingAsync(ip, timeOut);
			return pingReply.Status == IPStatus.Success;
		}
		/// <summary>
		/// telnet port 
		/// </summary>
		/// <param name="_ip"></param>
		/// <param name="_port"></param>
		/// <returns></returns>
		private bool checkPortEnable(string _ip, int _port)
		{
			//将IP和端口替换成为你要检测的
			string ipAddress = _ip;
			int portNum = _port;
			IPAddress ip = IPAddress.Parse(ipAddress);
			IPEndPoint point = new IPEndPoint(ip, portNum);
			bool _portEnable = false;
			try
			{
				using (Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
				{
					sock.Connect(point);
					//Console.WriteLine("连接{0}成功!", point);
					sock.Close();

					_portEnable = true;
				}
			}
			catch (SocketException e)
			{
				//Console.WriteLine("连接{0}失败", point);
				_portEnable = false;
			}
			return _portEnable;
		}
		public bool Conneted
		{
			get
			{
				if (this.Client == null)
				{
					return false;
				}
				return Client.Connected;
			}
		}

       

        public void Close()
		{
			try
			{
				if (Stream != null && Client != null)
				{
					Stream.Close();
					Client.Close();
					Stream.Dispose();
					Client.Dispose();
				}
			 
			}
			catch
			{

			}
			finally
			{
				Client = null;
			}
		}
		/// <summary>
		/// 发送指令并接收响应数据
		/// </summary>
		/// <param name="sd">发送</param>
		/// <param name="rd">接收</param>
		/// <returns></returns>
		public byte[] SendData(byte[] sd, byte[] rd = null)
		{
			lock (_lock)
			{
				Stream.Write(sd, 0, sd.Length);
				return ReceiveData(rd);
			}
		}
		/// <summary>
		/// 发送并接收数据
		/// </summary>
		/// <param name="SecData">发送数据</param>
		/// <param name="RecData">接收数据</param>
		/// <returns></returns>
		public async Task<byte[]> SendDataAsync(byte[] SecData, byte[] RecData = null)
		{
			await Stream.WriteAsync(SecData, 0, SecData.Length);
			return await ReceiveDataAsync(RecData);
		}

		public byte[] ReceiveData(byte[] rd = null)
		{
			try
			{
				lock (_lock)
				{
					int num = 0;
					if (rd == null)
					{
						var bs = new byte[1024 * 1024];
						num = Stream.Read(bs);
						rd = new byte[num];
						Array.Copy(bs, rd, num);
					}
					else
					{
						while (true)
						{
							int num2 = Stream.Read(rd, num, rd.Length - num);
							if (num2 == 0)
							{
								break;
							}
							num += num2;
							if (num >= rd.Length)
							{
								break;
							}
						}
					}
					if (num == 0)
					{
						throw new Exception("远程服务端无响应");
					}
				}
			}
			catch (Exception e)
			{
				Close();
				throw new Exception(e.Message);
			}
			return rd;
		}
		/// <summary>
		/// 异步接收数据
		/// </summary>
		/// <param name="rd">指定读取字节数</param>
		/// <returns></returns>
		public async Task<byte[]> ReceiveDataAsync(byte[] rd = null)
		{
			try
			{
				int num = 0;
				if (rd == null)
				{
					var bs = new byte[1024 * 1024];
					num = Stream.Read(bs);
					rd = new byte[num];
					Array.Copy(bs, rd, num);
				}
				else
				{
					while (true)
					{
						int num2 = await Stream.ReadAsync(rd, num, rd.Length - num);
						if (num2 == 0)
						{
							break;
						}
						num += num2;
						if (num >= rd.Length)
						{
							break;
						}
					}
				}
				if (num == 0)
				{
					throw new Exception("远程服务端无响应");
				}
			}
			catch (Exception e)
			{
				Close();
				throw new Exception(e.Message);
			}
			return rd;
		}


	}
}
