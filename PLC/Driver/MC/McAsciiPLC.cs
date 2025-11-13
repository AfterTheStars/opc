using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PLC.BaseDriver;

namespace PLC.MC
{
    public class McAsciiPLC:IPLC
	{
		IBasicClient Client = null;
		readonly object _lock = new object();
		 
		public static McAsciiPLC Create(bool IsUdp = false)
		{
			var plc = new McAsciiPLC();
			if (IsUdp)
			{
				plc.Client = new BasicUDP();
			}
			else
			{
				plc.Client = new BasicTCP();
			}
			return plc;
		}

		public IBasicClient BasicClient { get { return this.Client; } }

		private bool IsUdp
		{
			get
			{
				if (Client != null && Client is BasicUDP)
				{
					return true;
				}
				else
				{
					return false;
				}
			}

		}
		 

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
		public async Task<ushort[]> ReadWordsAsync(int  Memory, int ch, int cnt, bool IsAsync = true)
		{
			//var reData = new ushort[cnt];
			var mr = (PlcMemory)Memory;
			//int num = (int)(cnt*4 + 2 + (cnt-1));//计算响应字节
			byte[] buffer = McAsciiClass.Cmd(RorW.Read, mr, MemoryType.Word, ch, cnt);

			if (IsUdp)
			{
				return await UdpReadWordsAsync(buffer, IsAsync);
			}
		 
			byte[] headerBytes = new byte[18];
			byte[] dataBytes = null;
			if (IsAsync)
			{
				await Client.SendDataAsync(buffer, headerBytes);//头文件
				var head = System.Text.Encoding.ASCII.GetString(headerBytes);
				var len = McAsciiClass.CheckHeadCode(head);
				if (len == -1)
				{
					Client.ReceiveData();
					throw new Exception("读取错误");//清空缓存
				}
				dataBytes = new byte[len];
				await Client.ReceiveDataAsync(dataBytes);
			}
			else
			{//考虑加锁的问题
				lock (_lock)
				{
					Client.SendData(buffer, headerBytes);//头文件
					var head = System.Text.Encoding.ASCII.GetString(headerBytes);
					var len = McAsciiClass.CheckHeadCode(head);
					if (len == -1)
					{
						Client.ReceiveData();
						throw new Exception("读取错误");
					}
					dataBytes = new byte[len];
					Client.ReceiveData(dataBytes);
				}
			}
			string body= Encoding.ASCII.GetString(dataBytes);
			if (McAsciiClass.CheckEndCode(body))
			{
				return  McAsciiClass.DataToUshorts(body);
			}
			else
			{
				throw new Exception($"{mr}{ch} len={cnt} Read Fail");
			}
		}
		async Task<ushort[]> UdpReadWordsAsync(byte[] cmd,bool IsAsync)
		{
			string body;
			byte[] dataBytes = null;
			if (IsAsync)
			{

				dataBytes =await Client.SendDataAsync(cmd);
				var head = System.Text.Encoding.ASCII.GetString(dataBytes);
				var len = McAsciiClass.CheckHeadCode(head);
				if (len == -1)
				{
					throw new Exception("读取错误");//清空缓存
				}
				body = head.Substring(18, head.Length - 18);
			}
			else
			{//考虑加锁的问题
				lock (_lock)
				{
					dataBytes=Client.SendData(cmd);//头文件
					var head = System.Text.Encoding.ASCII.GetString(dataBytes);
					var len = McAsciiClass.CheckHeadCode(head);
					if (len == -1)
					{
						throw new Exception("读取错误");
					}
					body = head.Substring(18, head.Length - 18);
				}
			}
			if (McAsciiClass.CheckEndCode(body))
			{
				return McAsciiClass.DataToUshorts(body);
			}
			else
			{
				throw new Exception("响应错误");
			}
		}


		public async Task<ushort> ReadWordAsync(int Memory, int ch, bool IsAsync = true)
		{
			 
			var array = await this.ReadWordsAsync(Memory, ch, 1, IsAsync);
			return array[0];
		}

		public async Task<bool> WriteWordsAsync(int Memory, int ch, int cnt, ushort[] inData, bool IsAsync = true)
		{
			var mr = (PlcMemory)Memory;
			byte[] sd = McAsciiClass.Cmd(RorW.Write, mr, MemoryType.Word, ch,cnt, inData);
			byte[] array = new byte[22];
			if (IsAsync)
			{
				array = await Client.SendDataAsync(sd, array);
			}
			else
			{
				array = Client.SendData(sd, array);
			}
			var ss = Encoding.ASCII.GetString(array);
			var ck = McAsciiClass.CheckWriteCode(ss);
			if (ck == 0)
			{
				return true;
			}
			else
			{
				if (ck > 0)
				{
					Client.ReceiveData();
				}
				throw new Exception($"写入{mr}{ch}发生异常");
			}
		 
		}

		public async Task<bool> WriteWordAsync(int Memory, int ch, ushort inData, bool IsAsync = true)
		{
			return await WriteWordsAsync(Memory, ch, 1, new ushort[] { inData }, IsAsync);
		}

		public async Task<ushort> GetBitStateAsync(int Memory, string ch, bool IsAsync = true)
		{
			var mr = (PlcMemory)Memory;
			var mtyp = McAsciiClass.GetMemoryType(Memory);
			int offset = 0;
			int num = 0;
			if (mtyp == MemoryType.Bit)
			{
				num = int.Parse(ch);
			}
			else
			{
				var sr = ch.Split('.');
				num = int.Parse(sr[0]);
				if (sr.Length > 1)
				{
					offset = int.Parse(sr[1]);
				}
			}
			ushort value = await ReadWordAsync(Memory, num, IsAsync);
			return (ushort)McAsciiClass.GetBitValue(value, offset);

		}
		/// <summary>
		/// 设置位值
		/// </summary>
		/// <param name="mr">地址类型</param>
		/// <param name="ch">地址</param>
		/// <param name="bs">值</param>
		/// <param name="IsAsync"></param>
		/// <returns></returns>
		public async Task<bool> SetBitStateAsync(int Memory, string ch, bool bs, bool IsAsync = true)
		{
			var mr = (PlcMemory)Memory;
			var mtyp = McAsciiClass.GetMemoryType(Memory);
			int offset = 0;
			int num = 0;
			
			byte[] sd = null;
			if (mtyp == MemoryType.Bit)
			{
				num = int.Parse(ch);
				var bitv = bs==true ? 1 : 0;
				sd = McAsciiClass.Cmd(RorW.Write, mr, MemoryType.Bit, num, 1, new ushort[] { (ushort)bitv});
			}
			else
			{
				var sr = ch.Split('.');
				num = int.Parse(sr[0]);
				if (sr.Length > 1)
				{
					offset = int.Parse(sr[1]);
					if (offset > 15)
					{
						throw new Exception("指定位不能大于15");
					}
				}
				var value = this.ReadWord(Memory, num);
				if (bs)
				{
					value=(ushort)McAsciiClass.SetBitValue(value, offset);
				}
				else
				{
					value = (ushort)McAsciiClass.ClrBitValue(value, offset);
				}
				sd = McAsciiClass.Cmd(RorW.Write, mr, MemoryType.Word, num, 1, new ushort[] {value });
			}
			byte[] array = new byte[22];
			if (IsAsync)
			{
				array = await Client.SendDataAsync(sd, array);
			}
			else
			{
				array = Client.SendData(sd, array);

			}
			var ss = Encoding.ASCII.GetString(array);
			var ck = McAsciiClass.CheckWriteCode(ss);
			if (ck == 0)
			{
				return true;
			}
			else
			{
				if (ck > 0)
				{
					Client.ReceiveData();
				}
				throw new Exception($"写入{mr}{ch}发生异常");
			}
		}

		////同步
		public ushort[] ReadWords(int mr, int ch, int cnt)
		{
			return ReadWordsAsync(mr, ch, cnt, false).GetAwaiter().GetResult();
		}

		public ushort ReadWord(int mr, int ch)
		{
			return ReadWordsAsync(mr, ch, 1, false).GetAwaiter().GetResult()[0];
		}

		public bool WriteWords(int mr, int ch, int cnt, ushort[] inData)
		{
			return WriteWordsAsync(mr, ch, cnt, inData, false).GetAwaiter().GetResult();
		}
		public bool WriteWord(int mr, int ch, ushort inData)
		{
			return WriteWordsAsync(mr, ch, 1, new ushort[] { inData }, false).GetAwaiter().GetResult();
		}

		public ushort GetBitState(int mr, string ch)
		{
			return GetBitStateAsync(mr, ch, false).GetAwaiter().GetResult();
		}

		public bool SetBitState(int mr, string ch, bool bs)
		{
			return SetBitStateAsync(mr, ch, bs, false).GetAwaiter().GetResult();
		}

		public string GetModel()
		{
			throw new NotImplementedException();
		}
	}
}
