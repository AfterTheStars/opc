using System;

namespace PLC.KvHost
{
	internal class HostLinkClass
	{

		internal static MemoryType GetMemoryType(int Memory)
		{
			var mr = (PlcMemory)Memory;
			switch (mr)
			{
				case PlcMemory.B:
				case PlcMemory.CR:
				case PlcMemory.LR:
				case PlcMemory.MR:
				case PlcMemory.R:
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
		/// <param name="mt">字或位地址.位地址只允许写一个</param>
		/// <param name="ch">起始地址</param>
		/// <param name="cnt">长度</param>
		/// <param name="values">写入值</param>
		/// <returns></returns>
		internal static byte[] HostLinkCmd(RorW rw, PlcMemory mr, MemoryType mt, int ch, int cnt, ushort[] values = null)
		{
			var wr = rw == RorW.Read ? "RDS" : "WRS";
			string cmd = $"{wr} {mr}{ch}.H {cnt}";
			if (mt == MemoryType.Bit)
			{
				cmd = $"{wr} {mr}{ch} {cnt}";
			}
			if (rw == RorW.Write)
			{
				if (mt == MemoryType.Bit)
				{
					var bv = "0";
					if (values!=null && values[0]!=0)
					{
						bv = "1";
					}
					cmd = $"{cmd} {bv}";
				}
				else
				{
					if (values == null || values.Length!= cnt)
					{
						throw new Exception("待写入参数值错误");
					}
					System.Text.StringBuilder builder = new System.Text.StringBuilder();
					foreach (var v in values)
					{
						builder.Append(v.ToString("X4"));
						builder.Append(" ");
					}
					builder.Remove(builder.Length-1,1);
					cmd = $"{cmd} {builder}";
				}
				
			}
			cmd = $"{cmd}\r\n";
			return System.Text.Encoding.GetEncoding("gb2312").GetBytes(cmd);
		}

		/// <summary>
		/// 验证线束符
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static bool CheckEndCode(byte[] data)
		{
			if (data != null && data.Length>2)
			{
				var ss= System.Text.Encoding.GetEncoding("gb2312").GetString(data);
				if (data[data.Length - 2] == 13 && data[data.Length - 1] == 10)
				{
					return true;
				}
			}
			return false;
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
		internal static int CheckWriteCode(byte[] data)
		{
			if (data != null && data.Length == 4)
			{
				if (data[2] == 13 && data[3] == 10)
				{
					if (data[0] == 79 && data[1] == 75)
					{
						return 1;
					}
					return 0;
				}
			}
			return -1;
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
