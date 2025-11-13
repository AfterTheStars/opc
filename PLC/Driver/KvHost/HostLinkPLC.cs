using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PLC.BaseDriver;

namespace PLC.KvHost
{
    public class HostLinkPLC:IPLC
	{
		IBasicClient Client = null;
		public static HostLinkPLC Create(bool IsUdp = false)
		{
			var plc = new HostLinkPLC();
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
			var reData = new ushort[cnt];
			int num = (int)(cnt*4 + 2 + (cnt-1));//"HHHH HHHH\r\n"
			byte[] array = new byte[num];
			byte[] sd = HostLinkClass.HostLinkCmd(RorW.Read, mr, MemoryType.Word, ch, cnt);
			short result = -1;
			if (IsAsync)
			{//异步调用不加锁
				array = await Client.SendDataAsync(sd, array);
			}
			else
			{//同步调用加锁
				array = Client.SendData(sd, array);
			}
			if (HostLinkClass.CheckEndCode(array))
			{
				reData = HostLinkClass.DataToUshorts(array);
				result = 0;
			}
			else
			{
				Client.ReceiveData();//清空缓存 不然会一直错下去
			}
			if (result == 0)
			{
				return reData;
			}
			else
			{
				throw new Exception($"{mr}{ch} len={cnt} Read Fail");
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
			byte[] sd = HostLinkClass.HostLinkCmd(RorW.Write, mr, MemoryType.Word, ch,cnt, inData);
			byte[] array = new byte[4];
			if (IsAsync)
			{
				array = await Client.SendDataAsync(sd, array);

			}
			else
			{
				array = Client.SendData(sd, array);

			}
			//var ss = Encoding.ASCII.GetString(array);
			var ck = HostLinkClass.CheckWriteCode(array);
			if (ck == 1)
			{
				return true;
			}
			else if (ck == -1)
			{
				throw new Exception($"写入{mr}{ch}发生异常");
			}
			return false;

		}

		public async Task<bool> WriteWordAsync(int Memory, int ch, ushort inData, bool IsAsync = true)
		{
			return await WriteWordsAsync(Memory, ch, 1, new ushort[] { inData }, IsAsync);
		}

		public async Task<ushort> GetBitStateAsync(int Memory, string ch, bool IsAsync = true)
		{
			var mr = (PlcMemory)Memory;
			var mtyp = HostLinkClass.GetMemoryType(Memory);
			int offset = 0;
			int num = 0;
			//byte[] array = null;
			//byte[] sd = null;
			
			if (mtyp == MemoryType.Bit)
			{
				num = int.Parse(ch);
				//sd = HostLinkClass.HostLinkCmd(RorW.Read, mr, MemoryType.Bit, num, 1);
				//array = new byte[3];
			}
			else
			{
				var sr = ch.Split('.');
				num = int.Parse(sr[0]);
				if (sr.Length > 1)
				{
					offset = int.Parse(sr[1]);
				}
				//sd = HostLinkClass.HostLinkCmd(RorW.Read, mr, MemoryType.Word, num, 1);//读一个字回来
				//array = new byte[6];//"HHHH\r\n"
			}
			ushort value = await ReadWordAsync(Memory, num, IsAsync);
			return (ushort)HostLinkClass.GetBitValue(value, offset);

			//if (IsAsync)
			//{
			//	await SendDataAsync(sd, array);
			//}
			//else
			//{
			//	SendData(sd, array);
			//}
			//if (HostLinkClass.CheckEndCode(array))
			//{
			//	var data = HostLinkClass.DataToUshorts(array);
			//	if (mtyp == MemoryType.Bit)
			//	{
			//		return data[0];
			//	}
			//	else
			//	{
			//		return (ushort)HostLinkClass.GetBitValue(data[0],offset);
			//	}
			//}
 
			//throw new Exception($"读取位{mr}{ch}失败");

			 
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
			var mtyp = HostLinkClass.GetMemoryType(Memory);
			int offset = 0;
			int num = 0;
			byte[] array = new byte[4];
			byte[] sd = null;
			if (mtyp == MemoryType.Bit)
			{
				num = int.Parse(ch);
				var bitv = bs==true ? 1 : 0;
				sd = HostLinkClass.HostLinkCmd(RorW.Write, mr, MemoryType.Bit, num, 1, new ushort[] { (ushort)bitv});
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
				//先读回整个字。修改后再写入PLC
				var value = this.ReadWord(Memory, num);
 
				if (bs)
				{
					value=(ushort)HostLinkClass.SetBitValue(value, offset);
				}
				else
				{
					value = (ushort)HostLinkClass.ClrBitValue(value, offset);
				}
				sd = HostLinkClass.HostLinkCmd(RorW.Write, mr, MemoryType.Word, num, 1, new ushort[] { (ushort)value });
			}
			if (IsAsync)
			{
				array = await Client.SendDataAsync(sd, array);
			}
			else
			{
				array = Client.SendData(sd, array);

			}
			var ck = HostLinkClass.CheckWriteCode(array);

			if (ck == 1)
			{
				return true;
			}
			else if (ck == -1)
			{
				throw new Exception($"写入{mr}{ch}发生异常");
			}
			return false;
 
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
			try
			{
                var CMD = "?M\r\n";
                var array = Client.SendData(Encoding.ASCII.GetBytes(CMD));
				var d= Encoding.ASCII.GetString(array).Replace("\r\n","");
				return d;
            }
			catch (Exception e)
			{
				return e.Message;
			}
		 
		}
	}
}
