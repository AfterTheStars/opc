using PLC.BaseDriver;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PLC.FinsUDP
{
	public class FinsUDP : IPLC
	{
		IBasicClient Client;
		public static FinsUDP Create(string IP,int Port,int TimeOut=5000)
		{
			var fins = new FinsUDP();
			fins.Client = new BasicUDP();
			fins.plcNode = byte.Parse(IP.Split('.')[3]);
			fins.pcNode = byte.Parse(GetIp.Split('.')[3]);
			fins.Client.ConnectAsync(IP, Port, TimeOut).GetAwaiter();
			return fins;
		}
		public IBasicClient BasicClient { get { return this.Client; } }
		public void Close()
		{
			if (Client != null)
			{
				Client.Close();
			}
		}
		public bool Conneted
		{ 
			get 
			{
				if (Client != null)
				{
					return Client.Conneted;
				}
				return false;
			} 
		}
		public byte pcNode { get; set; }

		public byte plcNode { get; set; } = 255;
 
		public async Task<ushort[]> ReadWordsAsync(int mr, int ch, int cnt,bool IsAsync=true)
		{
			var reData = new ushort[(int)cnt];
			int num = (int)(14 + cnt * 2);
			byte[] array = new byte[num];
		 
			byte[] sd = FinsClass.FinsCmd(RorW.Read, (PlcMemory)mr, MemoryType.Word, (short)ch, 0, cnt, plcNode, pcNode);
		
			if (IsAsync)
			{//异步调用不加锁
				array=await Client.SendDataAsync(sd, array);
			}
			else
			{//同步调用加锁
				array= Client.SendData(sd, array);
			}
			if (!FinsClass.CheckCode(array))
			{
				System.Text.StringBuilder bs = new System.Text.StringBuilder();
				foreach (var a in array)
				{
					bs.Append(a.ToString());
					bs.Append(" ");
				}
				throw new Exception($"{Client.socket.RemoteEndPoint} Fins {(PlcMemory)mr}{ch} Read Err {bs}");
			}
			
			for (int i = 0; i < (int)cnt; i++)
			{
				byte[] value = new byte[]
				{
									array[14+i * 2 + 1],
									array[14+i * 2]
				};
				reData[i] = BitConverter.ToUInt16(value, 0);
			}//192 0 2 0 75 0 0 72 0 0 1 2 0 0
			//foreach (var a in sd)
			//{
			//	Console.Write(a + " ");
			//}
			//Console.WriteLine();
			//foreach (var a in array)
			//{
			//	Console.Write(a + " ");
			//}
			//Console.WriteLine("---------------------------");
			return reData;
		}

		public async Task<ushort> ReadWordAsync(int mr, int ch, bool IsAsync = true)
		{
			var  array= await this.ReadWordsAsync(mr, ch, 1, IsAsync);
			return array[0];
		}
		public async Task<bool> WriteWordsAsync(int mr, int ch, int cnt, ushort[] inData, bool IsAsync = true)
		{//128 0 2 0 72 0 0 75 0 0 1 2 130 39 16 0 0 1 0 1 wirte req
			byte[] array = new byte[14];//响应
			byte[] array2 = FinsClass.FinsCmd(RorW.Write, (PlcMemory)mr, MemoryType.Word, (short)ch, 0, cnt, plcNode, pcNode);
			byte[] array3 = new byte[(int)(cnt * 2)];
			for (int i = 0; i < (int)cnt; i++)
			{
				byte[] bytes = BitConverter.GetBytes(inData[i]);
				array3[i * 2] = bytes[1];
				array3[i * 2 + 1] = bytes[0];
			}
			byte[] array4 = new byte[(int)(cnt * 2 + array2.Length)];

			array2.CopyTo(array4, 0);
			array3.CopyTo(array4, array2.Length);
			 
			if (IsAsync)
			{
				array=await Client.SendDataAsync(array4, array);
			}
			else
			{
				array= Client.SendData(array4, array);
			}
			return FinsClass.CheckCode(array);
			 
		}

		public async Task<bool> WriteWordAsync(int mr, int ch, ushort inData, bool IsAsync = true)
		{
			return await WriteWordsAsync(mr, ch, 1, new ushort[] { inData }, IsAsync);
		}

		public async Task<ushort> GetBitStateAsync(int mr, string ch, bool IsAsync = true)
		{//192 0 2 0 75 0 0 72 0 0 1 1 0 0 1 
		 
			byte[] array = new byte[15];
			short ch2 = short.Parse(ch.Split(new char[]
			{
				'.'
			})[0]);
			short offset = short.Parse(ch.Split(new char[]
			{
				'.'
			})[1]);
			byte[] sd = FinsClass.FinsCmd(RorW.Read,(PlcMemory) mr, MemoryType.Bit, ch2, offset, 1, plcNode, pcNode);
			
			if (IsAsync)
			{
				array=await Client.SendDataAsync(sd, array);
			}
			else
			{
				array= Client.SendData(sd, array);
			}
			if (FinsClass.CheckCode(array))
			{
				return array[14];
			}
			else
			{
				throw new Exception($"{mr}{ch} Read Error!");
			}
		 
		}

		public async Task<bool> SetBitStateAsync(int mr, string ch, bool bs, bool IsAsync = true)
		{
			byte[] array = new byte[30];
			short ch2 = short.Parse(ch.Split(new char[]
			{
				'.'
			})[0]);
			short offset = short.Parse(ch.Split(new char[]
			{
				'.'
			})[1]);
			byte[] array2 = FinsClass.FinsCmd(RorW.Write,(PlcMemory) mr, MemoryType.Bit, ch2, offset, 1, plcNode, pcNode);
			byte[] array3 = new byte[array2.Length+1];
			array2.CopyTo(array3, 0);
			array3[array2.Length] =(byte)( bs?1:0);
			if (IsAsync)
			{
				array=await Client.SendDataAsync(array3, array);
			}
			else
			{
				array= Client.SendData(array3, array);
			}
			return FinsClass.CheckCode(array);
 
		}

		////同步
		public ushort[] ReadWords(int mr, int ch, int cnt)
		{
			return ReadWordsAsync(mr, ch, cnt, false).GetAwaiter().GetResult();
		}

		public ushort ReadWord(int mr, int ch)
		{
			return  ReadWordsAsync(mr, ch,1, false).GetAwaiter().GetResult()[0];
		}
		public bool WriteWords(int mr, int ch, int cnt, ushort[] inData)
		{
			return WriteWordsAsync(mr, ch, cnt, inData,false).GetAwaiter().GetResult();
			 
		}
		public bool WriteWord(int mr, int ch, ushort inData)
		{
			return WriteWordsAsync(mr, ch, 1, new ushort[] { inData },false).GetAwaiter().GetResult();
		}

		public ushort GetBitState(int mr, string ch)
		{
			return GetBitStateAsync(mr, ch,false).GetAwaiter().GetResult();
		}

		public bool SetBitState(int mr, string ch, bool bs)
		{
			return SetBitStateAsync(mr,  ch,  bs,false).GetAwaiter().GetResult();
		}

		public string GetModel()
		{
			throw new NotImplementedException();
		}

		static string GetIp { get; } = Dns.GetHostEntry(Dns.GetHostName()).
			   AddressList.FirstOrDefault(p => p.AddressFamily.ToString() == "InterNetwork")?.ToString();




	}
}
