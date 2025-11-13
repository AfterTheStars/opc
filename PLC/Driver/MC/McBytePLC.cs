using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PLC.BaseDriver;

namespace PLC.MC
{
    public class McBytePLC : IPLC
	{
		IBasicClient Client = null;
		readonly object _lock = new object();
		public static McBytePLC Create(bool IsUdp = false)
		{
			var plc = new McBytePLC();
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
	 
			var mr = (PlcMemory)Memory;
		 
			byte[] buffer = McByteClass.Cmd(RorW.Read, mr, MemoryType.Word, ch, cnt);

			if (IsUdp)
			{
				return await UdpReadWordsAsync(buffer, IsAsync);
			}
		 
			byte[] headerBytes = new byte[9];
			byte[] dataBytes = null;
			if (IsAsync)
			{
				await Client.SendDataAsync(buffer, headerBytes);//头文件
				var len = McByteClass.CheckHeadCode(headerBytes);
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
				 
					var len = McByteClass.CheckHeadCode(headerBytes);
					if (len == -1)
					{
						Client.ReceiveData();
						throw new Exception("读取错误");
					}
					dataBytes = new byte[len];
					Client.ReceiveData(dataBytes);
				}
			}
			if (McByteClass.CheckEndCode(dataBytes))
			{
				return  McByteClass.DataToUshorts(dataBytes);
			}
			else
			{
				throw new Exception($"{mr}{ch} len={cnt} Read Fail");
			}
		}
		async Task<ushort[]> UdpReadWordsAsync(byte[] cmd,bool IsAsync)
		{
			byte[] body;
			byte[] dataBytes = null;
			if (IsAsync)
			{

				dataBytes =await Client.SendDataAsync(cmd);
				var len = McByteClass.CheckHeadCode(dataBytes);
				if (len == -1)
				{
					throw new Exception("读取错误");//清空缓存
				}
			}
			else
			{//考虑加锁的问题
				lock (_lock)
				{
					dataBytes=Client.SendData(cmd);//头文件
					var len = McByteClass.CheckHeadCode(dataBytes);
					if (len == -1)
					{
						throw new Exception("读取错误");
					}
				}
			}
			body = new byte[dataBytes.Length - 9];
			Array.Copy(dataBytes,9,body,0, dataBytes.Length-9);
			if (McByteClass.CheckEndCode(body))
			{
				return McByteClass.DataToUshorts(body);
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
			byte[] sd = McByteClass.Cmd(RorW.Write, mr, MemoryType.Word, ch,cnt, inData);
			byte[] array = new byte[11];
			if (IsAsync)
			{
				array = await Client.SendDataAsync(sd, array);
			}
			else
			{
				array = Client.SendData(sd, array);
			}
			 
			var ck = McByteClass.CheckWriteCode(array);
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
			var mtyp = McByteClass.GetMemoryType(Memory);
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
			return (ushort)McByteClass.GetBitValue(value, offset);

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
			var mtyp = McByteClass.GetMemoryType(Memory);
			int offset = 0;
			int num = 0;
			
			byte[] sd = null;
			if (mtyp == MemoryType.Bit)
			{
				num = int.Parse(ch);
				var bitv = bs==true ? 1 : 0;
				sd = McByteClass.Cmd(RorW.Write, mr, MemoryType.Bit, num, 1, new ushort[] { (ushort)bitv});
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
					value=(ushort)McByteClass.SetBitValue(value, offset);
				}
				else
				{
					value = (ushort)McByteClass.ClrBitValue(value, offset);
				}
				sd = McByteClass.Cmd(RorW.Write, mr, MemoryType.Word, num, 1, new ushort[] {value });
			}
			byte[] array = new byte[11];
			if (IsAsync)
			{
				array = await Client.SendDataAsync(sd, array);
			}
			else
			{
				array = Client.SendData(sd, array);

			}
			 
			var ck = McByteClass.CheckWriteCode(array);
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
