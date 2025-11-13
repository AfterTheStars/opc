using System;

namespace PLC.MC
{
	internal class McAsciiClass
	{
		
		internal static MemoryType GetMemoryType(int Memory)
		{
			var mr = (PlcMemory)Memory;
			switch (mr)
			{
				case PlcMemory.Y:
				case PlcMemory.X:
				case PlcMemory.B:
				case PlcMemory.M:
				case PlcMemory.L:
				case PlcMemory.SM:
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
		internal static byte[] Cmd(RorW rw, PlcMemory mr, MemoryType mt, int ch, int cnt, ushort[] values = null)
		{

			 string head = "500000FF03FF00";//报文头,
			 string length = "";
			 string time = "0010";
			 string function_code = rw == RorW.Read ? "0401" : "1401";
			 string different_code = mt == MemoryType.Bit?"0001":"0000";
			 string data = mr.ToString().PadRight(2, '*');//地址类型
			 string start = ch.ToString().PadLeft(6, '0');
			 string count = ((ushort)cnt).ToString("X4").ToUpper();
		 
			System.Text.StringBuilder vs;
			string valueText = string.Empty;
			if (rw == RorW.Write)
			{
				if (values == null)
				{
					throw new Exception("值不能为空");
				}
				vs= new System.Text.StringBuilder();//写入的值
				if (mt == MemoryType.Bit)
				{//位地址转换值
					int n = 0;
					foreach (var a in values)
					{
						for (int i = 0; i < 16; i++)
						{
							if (n < cnt)
							{
								if (GetBitValue(a, i) == 1)
								{
									vs.Append('1');
								}
								else
								{
									vs.Append('0');
								}
							}
							else
							{
								break;
							}
							n++;
						}
					}
					valueText = vs.ToString().PadRight(cnt, '0');
				}
				else
				{//字地址
					if (values.Length!= cnt)
					{
						throw new Exception("待写入参数值长度错误");
					}
					vs = new System.Text.StringBuilder();
					foreach (var v in values)
					{
						vs.Append(v.ToString("X4").ToUpper());
					}
					valueText = vs.ToString().PadRight(cnt*4, '0');
				}
			}
			//
			//length = (time.Length + function_code.Length + different_code.Length + data.Length + start.Length + count.Length + valueText.Length).ToString("x4").ToUpper();
			length = (4 + 4 + 4 + 2 + 6 + 4 + valueText.Length).ToString("X4").ToUpper();
			var cmd = string.Format("{0}{1}{2}{3}{4}{5}{6}{7}{8}", head, length, time, function_code, different_code, data, start, count, valueText);
			 
			return System.Text.Encoding.ASCII.GetBytes(cmd);
		}

		/// <summary>
		/// 响应验证头，返回数据长度
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static int CheckHeadCode(string data)
		{
			if (!string.IsNullOrEmpty(data))
			{
				if (data.Length>=18 && data.Substring(0,4)=="D000")
				{
					return Convert.ToUInt16(data.Substring(14,4),16);
				}
			}
			return -1;
		}
		/// <summary>
		///  较验数据
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static bool CheckEndCode(string data)
		{
			if (!string.IsNullOrEmpty(data))
			{
				if (data.Length >=4 && data.Substring(0, 4) == "0000")
				{
					return true;
				}
			}
			return false;
		}
		internal static int CheckWriteCode(string data)
		{
			if (data.Length >= 22 && data.Substring(18, 4) == "0000")
			{
				return 0;
			}
			else
			{//错误，再计算还有没有多的字符需要清除
				int c = Convert.ToUInt16(data.Substring(14, 4),16);
				return c - 4;
			}
			
		}

		/// <summary>
		/// 响应字节转字
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static ushort[] DataToUshorts(string data)
		{//读取的都是字
			if (data != null)
			{
				var strarr = data.Substring(4, data.Length-4);//去掉验证
				var len = strarr.Length/4;
				var result = new ushort[len];
				for (int i = 0; i < len; i++)
				{
					result[i] = Convert.ToUInt16(strarr.Substring(i*4,4), 16);
				}
				return result;
			}
			return null;
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
