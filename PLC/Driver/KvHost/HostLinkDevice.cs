using PLC.BaseDriver;

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PLC.KvHost
{
    /// <summary>
    /// 欧母龙不支持读取多个位地址，但可以按字读取再转换。
    /// </summary>
    public class HostLinkDevice : BaseDevice
    {
        public MemoryType memoryType { get { return HostLinkClass.GetMemoryType(this.plcMemory); } }

        #region 方法重载
        public override void Create(string tp, int index, int count, int ms)
        {
            Key = $"{tp}{index}#{count}#"; 
            ts = DateTime.Now;
            dType = tp;
            StartNum = index;
            len = count;//字长
            Scan = ms;
            int max = 1000;
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
                case "DM":
                    plcMemory = (int)PlcMemory.DM;
                    break;
                case "EM":
                    plcMemory = (int)PlcMemory.EM;
                    break;
                case "CM":
                    plcMemory = (int)PlcMemory.CM;
                    break;
                case "ZF":
                    plcMemory = (int)PlcMemory.ZF;
                    break;
                case "R":
                    plcMemory = (int)PlcMemory.R;
                    break;
                case "MR":
                    plcMemory = (int)PlcMemory.MR;
                    break;
                case "LR":
                    plcMemory = (int)PlcMemory.LR;
                    break;
                case "CR":
                    plcMemory = (int)PlcMemory.CR;
                    break;
                case "B":
                    plcMemory = (int)PlcMemory.B;
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

        /// <summary>
        /// 读取字符串，需要高低位转换
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public override string ReadToString(byte[] values = null)
        {
            if (values == null)
            {
                values = Read();
            }
            for (int i = 0, c = values.Length; i < c; i += 2)
            {//字节倒过来
                if (i + 1 < c)
                {
                    var b = values[i];
                    values[i] = values[i + 1];
                    values[i + 1] = b;
                }
            }
            return base.ReadToString(values);
     
        }
        public override bool ReadBit(int sn, byte[] data = null)
        {
            if (PlcClient != null && data == null)
            {

                if (HostLinkClass.GetMemoryType(plcMemory) == MemoryType.Bit)
                {//位地址
                    sn += StartNum % 100;
                    var num = StartNum / 100 + (sn / 16);
                    num = num * 100 + sn % 16;
                    return PlcClient.GetBitState(plcMemory, $"{num}")>0?true:false;
                }
                else
                {
                    var addnum = StartNum + (sn / 16);
                    return PlcClient.GetBitState(plcMemory, $"{addnum}.{sn % 16}") > 0 ? true : false; ;
                }
            }
            return base.ReadBit(sn, data);
        }

        public override bool WriteBit(int sn, byte[] data = null)
        {
            if (PlcClient != null && data == null)
            {

                if (HostLinkClass.GetMemoryType(plcMemory) == MemoryType.Bit)
                {//位地址
                    sn += StartNum % 100;
                    var num = StartNum / 100 + (sn / 16);
                    num = num * 100 + sn % 16;
                    return PlcClient.SetBitState(plcMemory, $"{num}",true);
                }
                else
                {
                    var addnum = StartNum + (sn / 16);
                    return PlcClient.SetBitState(plcMemory, $"{addnum}.{sn % 16}", true);
                }
            }
            return base.WriteBit(sn, data);
        }
        public override bool ClearBit(int sn, byte[] data = null)
        {
            if (PlcClient != null && data==null)
            {
                if (HostLinkClass.GetMemoryType(plcMemory) == MemoryType.Bit)
                {//位地址
                    sn += StartNum % 100;
                    var num = StartNum / 100 + (sn / 16);
                    num = num * 100 + sn % 16;
                    return PlcClient.SetBitState(plcMemory, $"{num}", false);
                }
                else
                {
                    var addnum = StartNum + (sn / 16);
                    return PlcClient.SetBitState(plcMemory, $"{addnum}.{sn % 16}", false);
                }
            }
            return base.ClearBit(sn, data);
        }

      
        #endregion




    }
}
