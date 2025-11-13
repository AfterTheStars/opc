using System;
using System.Linq;

namespace PLC.MC
{
	internal class McByteClass
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
			var cmd = GetCommand(rw,  mr,  mt,  ch,  cnt);
 
			string valueText = string.Empty;
			if (rw == RorW.Write)
			{
				if (values == null)
				{
					throw new Exception("值不能为空");
				}
				if (mt == MemoryType.Bit)
				{//位地址转换值
					byte[] bv = new byte[cnt];
					foreach (var a in values)
					{
						for (int i = 0,n=0; i < 16; i++,n++)
						{
							if (n < cnt)
							{
								bv[n] = (byte)GetBitValue(a, i);
							}
							else
							{
								break;
							}
						}
					}
					cmd=cmd.Concat(bv).ToArray();
					var len = 0x0C + bv.Length;
					var bslen = BitConverter.GetBytes(len);
					//读取长度,字节序反转
					cmd[8] = bslen[1];
					cmd[7] = bslen[0];
				}
				else
				{//字地址
					if (values.Length!= cnt)
					{
						throw new Exception("待写入参数值长度错误");
					}
					byte[] bv = new byte[cnt*2];
					for (int i = 0; i < values.Length; i++)
					{
						var bs = BitConverter.GetBytes(values[i]);
						if (i < cnt)
						{
							bv[i*2+1] = bs[1];
							bv[i*2] = bs[0];
						}
						else
						{
							break;
						}
					}
					cmd = cmd.Concat(bv).ToArray();
					var len = 0x0C + bv.Length;
					var bslen = BitConverter.GetBytes(len);
					//请求长度
					cmd[8] = bslen[1];
					cmd[7] = bslen[0];

				}
			}
			 
			return cmd;
		}

		static byte[] GetCommand(RorW rw, PlcMemory mr, MemoryType mt, int ch, int cnt)
		{
			byte[] command = new byte[21];
			//发起指令
			command[0] = 0x50;
			command[1] = 0x00;
			//网路编号
			command[2] = 0x00;
			//PLC编号
			command[3] = 0xFF;
			//请求目标模块IO编号
			command[4] = 0xFF;
			command[5] = 0x03;
			//请求目标模块站编号
			command[6] = 0x00;
			//应答数据物理长度
			command[7] = 0x0C;
			command[8] = 0x00;
			//cpu监视定时器
			command[9] = 0x10;
			command[10] = 0x00;
			//命令
			command[11] = 0x01;
			command[12] = 0x04;
			if (rw == RorW.Write)
			{
				command[11] = 0x01;
				command[12] = 0x14;
			}
			//子命令
			command[13] =  0x00;
			if (mt == MemoryType.Bit)
			{
				command[13] = 0x01;
			}
			command[14] = 0x00;
			//首地址,字节序反转
			var bsch = BitConverter.GetBytes(ch);
			command[17] = bsch[2];
			command[16] = bsch[1];
			command[15] = bsch[0];
			//软元件
			command[18] = (byte)mr;
			//软元件长度,字节序反转
			var bscnt = BitConverter.GetBytes(cnt);
			command[20] = bscnt[1];
			command[19] = bscnt[0];

			return command;
		}


		/// <summary>
		/// 响应验证头，返回数据长度
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static int CheckHeadCode(byte[] data)
		{
			if (data!=null)
			{
				if (data[0]==0xD0 && data[1] == 0x00)
				{
					return BitConverter.ToUInt16(data, 7); 
				}
			}
			return -1;
		}
		/// <summary>
		///  较验数据
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static bool CheckEndCode(byte[] data)
		{
			if (data!=null)
			{
				if (data[0]==0x00 && data[1] == 0x00)
				{
					return true;
				}
			}
			return false;
		}
		internal static int CheckWriteCode(byte[] data)
		{
			if (data[9] == 0x00 && data[10] == 0x00)
			{
				return 0;
			}
			else
			{//错误，再计算还有没有多的字符需要清除
				int c = BitConverter.ToUInt16(data, 7);
				return c - 4;
			}
			
		}

		/// <summary>
		/// 响应字节转字
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		internal static ushort[] DataToUshorts(byte[] data)
		{//读取的都是字
			if (data != null)
			{
				var len = data.Length/2-1;//去掉0000结束符
				var result = new ushort[len];
				for (int i = 0; i < len; i++)
				{
					result[i] = BitConverter.ToUInt16(data,2+i*2);
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
