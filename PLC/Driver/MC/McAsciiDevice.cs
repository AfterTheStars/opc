using PLC.BaseDriver;

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PLC.MC
{
    /// <summary>
    /// 欧母龙不支持读取多个位地址，但可以按字读取再转换。
    /// </summary>
    public class McAsciiDevice : BaseDevice
    {
        public MemoryType memoryType { get { return McAsciiClass.GetMemoryType(this.plcMemory); } }

        #region 方法重载
        public override void Create(string tp, int index, int count, int ms)
        {
            Key = $"{tp}{index}#{count}#"; 
            ts = DateTime.Now;
            dType = tp;
            StartNum = index;
            len = count;//字长
            Scan = ms;
            int max = 950;
            if (count > max)
            {//指令单次最大1000字
                var last = count % max;
                for (int i = 0, c = (int)Math.Ceiling(count / (double)max); i < c; i++)
                {
                    if (i == c - 1 && last > 0)
                    {
                        AddressItems.Add(new int[] { index + i * max, last });
                    }
                    else
                    {
                        AddressItems.Add(new int[] { index+i* max, max });
                    }
                }
            }
            else
            {
                AddressItems.Add(new int[] { index, count });
            }
            switch (tp.ToUpper())
            {
                case "D":
                    plcMemory = (int)PlcMemory.D;
                    break;
                case "M":
                    plcMemory = (int)PlcMemory.M;
                    break;
                case "ZR":
                    plcMemory = (int)PlcMemory.ZR;
                    break;
                case "R":
                    plcMemory = (int)PlcMemory.R;
                    break;
                case "X":
                    plcMemory = (int)PlcMemory.X;
                    break;
                case "Y":
                    plcMemory = (int)PlcMemory.Y;
                    break;
                case "B":
                    plcMemory = (int)PlcMemory.B;
                    break;
                case "L":
                    plcMemory = (int)PlcMemory.L;
                    break;
                case "SM":
                    plcMemory = (int)PlcMemory.SM;
                    break;
                case "SD":
                    plcMemory = (int)PlcMemory.SD;
                    break;
               
                case "W":
                    plcMemory = (int)PlcMemory.W;
                    break;
                case "TN":
                    plcMemory = (int)PlcMemory.TN;
                    break;
                case "TS":
                    plcMemory = (int)PlcMemory.TS;
                    break;
                case "CN":
                    plcMemory = (int)PlcMemory.CN;
                    break;
                case "CS":
                    plcMemory = (int)PlcMemory.CS;
                    break;
                default:
                    throw new Exception($"未知地址类型【{dType}】");
            }

            if (Scan > 0 && Scan < 10)
            {//最少10ms
                Scan = 10;
            }
            WordValues = new ushort[count];
            ByteValues = new byte[count * 2];

            if (Scan > 0)
            {
                new Thread(new ThreadStart(ScanTask)) { IsBackground = true }.Start();
            }

           

        }

        ///// <summary>
        ///// 读取字符串，需要高低位转换
        ///// </summary>
        ///// <param name="values"></param>
        ///// <returns></returns>
        //public override string ReadToString(byte[] values = null)
        //{
        //    if (values == null)
        //    {
        //        values = Read();
        //    }
        //    for (int i = 0, c = values.Length; i < c; i += 2)
        //    {//字节倒过来
        //        if (i + 1 < c)
        //        {
        //            var b = values[i];
        //            values[i] = values[i + 1];
        //            values[i + 1] = b;
        //        }
        //    }
        //    return base.ReadToString(values);
        //}

        #endregion

    }
}
