using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace PLC.ModbusTcp
{
	internal class ModbusClass
	{

		internal static MemoryType GetMemoryType(int Memory)
		{
			var mr = (PlcMemory)Memory;
			switch (mr)
			{
				case PlcMemory.Coil:
				case PlcMemory.InputCoil:
					return MemoryType.Bit;
				default:
					return MemoryType.Word;
			}
		}
	 
		/// <summary>
		/// 
		/// </summary>
		/// <param name="rw">写或写</param>
		/// <param name="mr">地址类型</param>
		/// <param name="mt">字或位地址</param>
		/// <param name="ch">起始地址</param>
		/// <param name="cnt">长度</param>
		/// <param name="values">写入值</param>
		/// <returns></returns>
		internal static byte[] ModbusTcpCmd(RorW rw, PlcMemory mr, int ch, int cnt, ushort[] values = null)
		{
			byte[] request = null;
			if (rw == RorW.Read)
			{
				//报文头:事务元标识符（2个字节）+协议标识符（2个字节）+长度（2个字节）+单元标识符（1个字节）
				//【报文头7】【功能码1】【起始地址2】【地址个数2】
				var head = GetHead(6);
				byte code = 0x00;
				switch (mr)
				{
					case PlcMemory.Register:
						code = 0x03;
						break;
					case PlcMemory.Coil:
						code = 0x01;
						break;
					case PlcMemory.InputRegister:
						code = 0x04;
						break;
					case PlcMemory.InputCoil:
						code = 0x02;
						break;
					default:
						throw new Exception("不支持的类型" + mr);
				}
				var start = BitConverter.GetBytes(ch);
				var count = BitConverter.GetBytes(cnt);
				var body = new byte[5] { code, start[1], start[0], count[1], count[0] };
				request = head.Concat(body).ToArray();
			}
			else
			{//【报文头7】【功能码1】【起始地址2】【线圈个数2】【写入字节数1=线圈数/8】【字节】
			 
				var head = new byte[7];// GetHead((short)len);

				byte[] body = new byte[6];//【功能码1】【起始地址2】【线圈个数2】【写入字节数1】
				var start = BitConverter.GetBytes(ch);
				var count = BitConverter.GetBytes(cnt);
				byte InBLen = (byte)(cnt * 2);
				switch (mr)
				{
					case PlcMemory.Register:
						body[0] = 16;
						break;
					case PlcMemory.Coil:
						InBLen = (byte)Math.Ceiling(cnt/8.0);//写入字节数
						body[0] =15;
						break;
					default:
						throw new Exception(mr + " 类型不支持写入");
				}
				head = GetHead((short)(7 + InBLen));
				body[1] = start[1];
				body[2] = start[0];
				body[3] = count[1];
				body[4] = count[0];
				body[5] = InBLen;
				//写入值
				List<byte> value = new List<byte>();
				foreach (var a in values)
				{
					var bs = BitConverter.GetBytes(a);
					if (mr == PlcMemory.Coil)
					{
						value.AddRange(bs);
					}
					else
					{
						value.Add(bs[1]);
						value.Add(bs[0]);
					}
				}
				request = head.Concat(body).Concat(value.GetRange(0, InBLen)).ToArray();
			}
			return request;
		}

		static byte[] GetHead(short Len,short Task=1,byte Uint=0x01)
		{
			var bs = BitConverter.GetBytes(Len);
			var bs2 = BitConverter.GetBytes(Task);
			return new byte[] { bs2[1], bs2[0], 0x00, 0x00, bs[1], bs[0], Uint };
		}

		 
		internal static ushort[] ReadAsUShort(byte[] data, int mr)
		{
			var idx = 3;//0=单元节点，1=功能码，2=字节数
			var output = new List<ushort>();
			bool IsBit = GetMemoryType(mr)== MemoryType.Bit?true:false;
			if (IsBit & (data.Length- idx) %2==1)
			{
				data = data.Concat(new byte[1]).ToArray();
			}
			var len = data.Length;
			while (idx < len)
			{
				if (IsBit)
				{
					var value = BitConverter.ToUInt16(data, idx);
					output.Add(value);
				}
				else
				{
					var value = BitConverter.ToUInt16(new byte[] { data[idx+1],data[idx]});
					output.Add(value);
				}
				idx += 2;
			}
			return output.ToArray();
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="Head0">发送头</param>
		/// <param name="head1">接收头</param>
		/// <param name="code">功能码</param>
		/// <returns></returns>
		internal static bool CheckReadCode(byte[] Head0,byte[] head1,byte[] body)
		{
			if (GetDataLength(head1) > 3)
			{
				if (Head0[0] == head1[0] && Head0[1] == head1[1] && Head0[7] == body[1])
				{
					return true;
				}
			}
			return false;
		}
		internal static ushort GetDataLength(byte[] Header)
		{
			return BitConverter.ToUInt16(new byte[] { Header[5], Header[4] });
		}

		/// <summary>
		/// 响应字节转字
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static ushort[] DataToUshorts(byte[] data)
		{
			if (data != null)
			{
				var str = System.Text.Encoding.ASCII.GetString(data,0, data.Length-2);
				var strarr = str.Split(' ');
				var len = strarr.Length;
				var result = new ushort[len];
				for (int i = 0; i < len; i++)
				{
					result[i] = Convert.ToUInt16(strarr[i],16);
				}
				return result;
			}
			return null;
		}

		/// <summary>
		/// 写入较验，-1错误,1写入OK,0写入失败
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static bool CheckWriteCode(byte[] head0,byte[] head1,byte[] body)
		{
			if (head1[5] == 6)
			{
				if (head0[0] == head1[0] && head0[1] == head1[1] && head0[7]== body[1])
				{
					return true;
				}
			}
			return false;
		}

		internal static int GetBitValue(int value, int bit)
		{//获取位信号
			int bit_value = (value >> bit) & 0x01;
			return bit_value;
		}
		internal static int SetBitValue(int value, int bit_sn)
		{//设置位信号
			return (value | (0x01 << bit_sn));
		}

		internal static int ClrBitValue(int value, int bit_sn)
		{//清空位信号
			return  (value & (~(0x01 << bit_sn)));
		}
 

	}
}
