using PLC.BaseDriver;
using PLC.ModbusTcp;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PLC.ModbusTcp
{
	public class ModbusPLC: IPLC
	{
		IBasicClient Client;
		readonly object _lock = new object();
		public static ModbusPLC Create()
		{
			var plc = new ModbusPLC();
			plc.Client = new BasicTCP();
			//fins.Client.ConnectAsync(IP, Port, TimeOut).GetAwaiter();
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
		public async Task<ushort[]> ReadWordsAsync(int mr, int ch, int cnt, bool IsAsync = true)
		{
			//错误响应【报文头6】【单元号1】【错误码2】
			if (ModbusClass.GetMemoryType(mr) == MemoryType.Bit)
			{//线圈 一个字长度为16
				cnt = cnt * 16;
			}
			var buffer = ModbusClass.ModbusTcpCmd(RorW.Read,(PlcMemory)mr,ch,cnt);
			byte[] headerBytes = new byte[6];
			byte[] dataBytes = null;
			if (IsAsync)
			{
				await Client.SendDataAsync(buffer, headerBytes);
				dataBytes = new byte[ModbusClass.GetDataLength(headerBytes)];
				await Client.ReceiveDataAsync(dataBytes);
			}
			else
			{//考虑加锁的问题
				lock (_lock)
				{
					Client.SendData(buffer, headerBytes);
					dataBytes = new byte[ModbusClass.GetDataLength(headerBytes)];
					Client.ReceiveData(dataBytes);
				}
			}
			if (ModbusClass.CheckReadCode(buffer, headerBytes, dataBytes))
			{
				return ModbusClass.ReadAsUShort(dataBytes, mr);
			}
			throw new Exception("响应错误！");

		}
		public async Task<ushort> ReadWordAsync(int mr, int ch, bool IsAsync = true)
		{
			var array = await this.ReadWordsAsync(mr, ch, 1, IsAsync);
			return array[0];
		}
		public async Task<bool> WriteWordsAsync(int mr, int ch, int cnt, ushort[] inData, bool IsAsync = true)
		{//响应格式【报文头6】【单元号1】【功能码1】【起始地址2】【地址个数2】
		 //错误响应【报文头6】【单元号1】【错误码2】

			if (ModbusClass.GetMemoryType(mr) == MemoryType.Bit)
			{//线圈 一个字长度为16
				cnt = cnt * 16;
			}
			var buffer = ModbusClass.ModbusTcpCmd(RorW.Write, (PlcMemory)mr, ch, cnt, inData);
			byte[] headerBytes = new byte[6];
			byte[] dataBytes = null;
			if (IsAsync)
			{
				await Client.SendDataAsync(buffer, headerBytes);
				dataBytes = new byte[ModbusClass.GetDataLength(headerBytes)];
				await Client.ReceiveDataAsync(dataBytes);
			}
			else
			{//考虑加锁的问题
				lock (_lock)
				{
					Client.SendData(buffer, headerBytes);
					dataBytes = new byte[ModbusClass.GetDataLength(headerBytes)];
					Client.ReceiveData(dataBytes);
				}
			}
			if (ModbusClass.CheckWriteCode(buffer, headerBytes, dataBytes))
			{
				return true;
			}
			throw new Exception("响应错误！");
		 
		}

		public async Task<bool> WriteWordAsync(int mr, int ch, ushort inData, bool IsAsync = true)
		{
			return await WriteWordsAsync(mr, ch, 1, new ushort[] { inData }, IsAsync);
		}

		public async Task<ushort> GetBitStateAsync(int mr, string ch, bool IsAsync = true)
		{
			var mtyp = ModbusClass.GetMemoryType(mr);
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
			var buffer = ModbusClass.ModbusTcpCmd(RorW.Read,(PlcMemory) mr, num, 1);
			byte[] headerBytes = new byte[6];
			byte[] dataBytes = null;
			if (IsAsync)
			{
				await Client.SendDataAsync(buffer, headerBytes);
				dataBytes = new byte[ModbusClass.GetDataLength(headerBytes)];
				await Client.ReceiveDataAsync(dataBytes);
			}
			else
			{
				lock (_lock)
				{
					Client.SendData(buffer, headerBytes);
					dataBytes = new byte[ModbusClass.GetDataLength(headerBytes)];
					Client.ReceiveData(dataBytes);
				}
			}
			if (ModbusClass.CheckReadCode(buffer, headerBytes, dataBytes))
			{
				var vs = ModbusClass.ReadAsUShort(dataBytes,mr);
				return (ushort)ModbusClass.GetBitValue(vs[0], offset);
			}
			throw new Exception("响应错误！");

		}
		/// <summary>
		/// 设置位值
		/// </summary>
		/// <param name="mr">地址类型</param>
		/// <param name="ch">地址</param>
		/// <param name="bs">值</param>
		/// <param name="IsAsync"></param>
		/// <returns></returns>
		public async Task<bool> SetBitStateAsync(int mr, string ch, bool bs, bool IsAsync = true)
		{
			var mtyp = ModbusClass.GetMemoryType(mr);
			int offset = 0;
			int num = 0;
			ushort inData = 0;
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
				//先读回来字
				inData = ReadWord(mr, num);
			}
			if (bs)
			{
				inData = (ushort)ModbusClass.SetBitValue(inData, offset);
			}
			else
			{
				inData = (ushort)ModbusClass.ClrBitValue(inData, offset);
			}
			var buffer = ModbusClass.ModbusTcpCmd(RorW.Write,(PlcMemory) mr, num, 1,new ushort[] { inData });
			byte[] headerBytes = new byte[6];
			byte[] dataBytes = null;
			if (IsAsync)
			{
				await Client.SendDataAsync(buffer, headerBytes);
				dataBytes = new byte[ModbusClass.GetDataLength(headerBytes)];
				await Client.ReceiveDataAsync(dataBytes);
			}
			else
			{//考虑加锁的问题
				lock (_lock)
				{
					Client.SendData(buffer, headerBytes);
					dataBytes = new byte[ModbusClass.GetDataLength(headerBytes)];
					Client.ReceiveData(dataBytes);
				}
			}
			if (ModbusClass.CheckWriteCode(buffer, headerBytes, dataBytes))
			{
				return true;
			}
			throw new Exception("响应错误！");
 
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
