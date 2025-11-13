using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PLC.BaseDriver
{
	public class BasicUDP:IBasicClient
	{
	 
		//public Socket Client;
		public UdpClient Client { get; set; }
		//public NetworkStream Stream;
		System.Net.IPEndPoint remoteEP = null;

		public static int LocalPort { get; set; }=-1;

		public string IP { get; set; }
		public int Port { get; set; }
 
		public int Timeout { get; set; }

		public  object _lock { get; set; } = new object() ;
		public Socket socket
		{
			get
			{
				if (Client != null)
				{
					return Client.Client;

				}
				return null;
			}

		}
		public  async Task<bool> ConnectAsync(string rIP, int rPort, int timeOut = 5000)
		{
			 
			this.Timeout = timeOut;
			this.IP = rIP;
			this.Port = rPort;
			//this.Client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			//Client.SendTimeout = timeOut;
			//Client.ReceiveTimeout = timeOut;
			if (LocalPort >0)
			{
				IPEndPoint ipep = new IPEndPoint(IPAddress.Any, LocalPort);
				Client = new UdpClient(ipep);
			}
			else
			{
				this.Client = new UdpClient();
			}
			
			Client.Client.SendTimeout = timeOut;
			Client.Client.ReceiveTimeout = timeOut;
			remoteEP = new IPEndPoint(IPAddress.Parse(rIP), rPort);
			Client.Connect(rIP, rPort);
			//Console.WriteLine(remoteEP.ToString() + " Udp连接成功");
			return true;
		}

		public  async Task<bool> ReConnectAsync()
		{
			this.Close();
			if (!string.IsNullOrEmpty(IP) && Port > 0)
			{
				return await ConnectAsync(IP, Port, Timeout);
			}
			return false;
		}
	 
		public async Task<bool> PingCheckAsync(string ip, int timeOut,int port=0)
		{
			return true;
			//Ping ping = new Ping();
			//PingReply pingReply = await ping.SendPingAsync(ip, timeOut);
			//UdpStatus= pingReply.Status == IPStatus.Success;
			//return UdpStatus;
		}
		public bool Conneted
		{
			get
			{
				if (this.Client == null)
				{
					return false;
				}
				return true;
			}
		}
		public void Close()
		{
			try
			{
				Client.Close();
				Client.Dispose();
				 
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
		public byte[] SendData(byte[] sd,byte[] rd=null)
		{
			lock (_lock)
			{
				int n= Client.Send(sd, sd.Length);
				return ReceiveData(rd);
			}
		}
		/// <summary>
		/// 发送并接收数据
		/// </summary>
		/// <param name="SecData">发送数据</param>
		/// <param name="RecData">接收数据</param>
		/// <returns></returns>
		public async Task<byte[]>SendDataAsync(byte[] SecData,byte[] RecData=null)
		{
            int n = await Client.SendAsync(SecData, SecData.Length);
            return await ReceiveDataAsync(RecData);

        }
		int ConnetErr = 0;
		bool UdpStatus = true;
		public byte[] ReceiveData(byte[] rd=null)
		{
			try
			{
                if (UdpStatus == false)
                {
                    if (PingCheckAsync(this.IP, 100).GetAwaiter().GetResult() == false)
                    {
                        throw new Exception($"{this.IP}:{this.Port}UDP连接异常");
                    }
                }
                lock (_lock)
				{
					//byte[] buffer = new byte[1024 * 1024];
					//int length = Client.Receive(buffer);//接收数据报
					//rd = new byte[length];
					//Array.Copy(buffer,0,rd,0, length);
					rd = Client.Receive(ref remoteEP);
				}
				ConnetErr = 0;
			}
			catch (Exception e)
			{
				if (UdpStatus)
				{
					PingCheckAsync(this.IP, 100).GetAwaiter();
				}
				if (ConnetErr++ > 5)
				{
					this.Close();
					this.ReConnectAsync().GetAwaiter().GetResult();
				}

				throw new Exception(e.Message);
			}
			return rd;
		}
		/// <summary>
		/// 异步接收数据
		/// </summary>
		/// <param name="rd">指定读取字节数</param>
		/// <returns></returns>
		public async Task<byte[]> ReceiveDataAsync(byte[] rd=null)
		{
			try
			{
				if (UdpStatus == false)
				{
					if (await PingCheckAsync(this.IP, 100) == false)
					{
						throw new Exception("UDP连接异常");
					}
				}
				var data = await Client.ReceiveAsync();
                rd = data.Buffer;
				ConnetErr = 0;


			}
			catch(Exception e)
			{
				if (UdpStatus)
				{
					await PingCheckAsync(this.IP, 100);
				}
				if (ConnetErr++ > 5)
				{
					this.Close();
					await this.ReConnectAsync();
				}
				throw e;
			}
			return rd;
		}
 
 
		public static string GetIp { get; } = Dns.GetHostEntry(Dns.GetHostName()).
				AddressList.FirstOrDefault(p => p.AddressFamily.ToString() == "InterNetwork")?.ToString();

	}
}
