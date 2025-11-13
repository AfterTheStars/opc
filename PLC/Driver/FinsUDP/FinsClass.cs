using System;

namespace PLC.FinsUDP
{
	internal class FinsClass
	{
		private static byte GetMemoryCode(PlcMemory mr, MemoryType mt)
		{
			//byte result;
			if (mt == MemoryType.Bit)
			{
				return (byte)mr;
				//switch (mr)
				//{
				//	case PlcMemory.CIO:
				//		result = 0x30;
				//		break;
				//	case PlcMemory.WR:
				//		result = 0x31;
				//		break;
				//	case PlcMemory.DM:
				//		result = 0x02;
				//		break;
				//	default:
				//		result = 0;
				//		break;
				//}
			}
			else
			{
				return (byte)((ushort)mr+128);
				//switch (mr)
				//{
				//	case PlcMemory.CIO:
				//		result = 176;
				//		break;
				//	case PlcMemory.WR:
				//		result = 177;
				//		break;
				//	case PlcMemory.DM:
				//		result = 130;
				//		break;
				//	default:
				//		result = 0;
				//		break;
				//}
			}
			//return result;
		}

		internal static bool CheckCode(byte[] data)
		{
			//if (data[0]>0 && data.Length >= 14 && data[12] == 0 && data[13] == 0)//出现dada[12]较验不通过
			if (data[0] > 0 && data.Length >= 14)
			{
				return true;
			}
			return false;

		}
		internal static byte[] FinsCmd(RorW rw, PlcMemory mr, MemoryType mt, short ch, short offset, int cnt,byte plcNode,byte pcNode)
		{
			byte[] array = new byte[18];
			array[0] = 128;
			array[1] = 0;
			array[2] = 2;
			array[3] = 0;
			array[4] = 255;//plcNode;通用255
			array[5] = 0;
			array[6] = 0;
			array[7] = pcNode;
			array[8] = 0;
			array[9] = 0;
			array[10] = 1;
			array[11] = (byte)(rw == RorW.Read?0x1:0x2);
			array[12] = GetMemoryCode(mr, mt);
			var lc= BitConverter.GetBytes(ch);
			array[13] =lc[1] ;
			array[14] =lc[0] ;
			array[15] = (byte)offset;
			var len = BitConverter.GetBytes(cnt);
			array[16] = len[1];
			array[17] = len[0];
			return array;
		}

//128 0 2 0 72 0 0 75 0 0 1 1 130 39 16 0 0 1 read D10000*1 请求

//192 0 2 0 75 0 0 72 0 0 1 1 0 0 0 2 read D10000*1 响应

//128 0 2 0 72 0 0 75 0 0 1 2 130 39 16 0 0 1 0 1 wirte req
//192 0 2 0 75 0 0 72 0 0 1 2 0 0 write ack

	}
}
