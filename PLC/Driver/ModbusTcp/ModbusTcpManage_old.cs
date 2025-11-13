using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using PLC.Tcp;
using PLC.BaseDriver;
using PLC.Interface;
using ModbusTcp;

namespace PLC.ModbusTcp

{
    public class ModbusTcpManage: IDriver
    {
        public ModbusClient modbusClient;
        SocketTcpClient Client = null;
        Dictionary<string, BaseDevice> DicDevice = new Dictionary<string, BaseDevice>();//PLC地址
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
        object _lock = new object();
        #region 构造方法
        public ModbusTcpManage(string IP,bool IsAuto=true)
        {
            int port = 8501;
            var ss = IP.Split(':');
            if (ss.Length > 1)
            {
                port = int.Parse(ss[1]);
            }
            Client = new SocketTcpClient(ss[0], port, IsAuto);
            //modbusClient = new ModbusClient(ss[0], port,60000);
            //modbusClient.Init();
        }
        //public KvHostManage(string sip,int sport)
        //{
        //    ConnetPLC(sip, sport);
        //}
        #endregion
        #region 接口方法
        /// <summary>
        /// 设置PLC地址及读取周期
        /// </summary>
        /// <param name="count">字长度</param>
        /// <param name="Address">地址编号</param>
        /// <param name="ms">读取周期毫秒</param>
        /// <returns></returns>
        public BaseDevice CreateDevice(string Address, int count, int ms = 0)
        {
            bool isBool= CheckAddress(Address,out string type,out int index);
            var key = $"{type}{index}#{count}#";
            if (!DicDevice.TryGetValue(key, out BaseDevice d))
            {
                d = new ModbusTcpDevice(Client);
                d.IsBool = isBool;
                d.Create(type, index, count, ms);
                DicDevice[key] = d;
            }
            else
            {
                if (d.Scan == 0 && ms > 0)
                {//需要扫描
                    d.Scan = ms;
                }
                else if (ms > 0 && d.Scan > 0 && ms < d.Scan)
                {//切换频率
                    if (ms < 10)
                    {
                        ms = 10;
                    }
                    d.Scan = ms;
                }
            }
            return d;
        }

        public BaseDevice Device(string key)
        {
            if (DicDevice.TryGetValue(key, out BaseDevice d))
            {
                return d;
            }
            return null;
        }
        /// <summary>
        ///读取已有地址。按字响应
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Key"></param>
        /// <returns></returns>
        public T Read<T>(string Key)
        {
            if (!DicDevice.TryGetValue(Key, out BaseDevice d))
            {
                throw new Exception("未找到地址" + Key);
            }
            return d.Read<T>();
        }
        
        /// <summary>
        /// 读取指定地址
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Address"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        public T ReadAddress<T>(string Address,int len=1)
        {
            var count = DataHelper.GetAddrLength<T>(len);
            BaseDevice dv = this.CreateDevice(Address, count);
            return dv.Read<T>();

        }
      
 
        /// <summary>
        /// 写入值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public bool Write<T>(string Key, T values)
        {
            if (!DicDevice.TryGetValue(Key, out BaseDevice d))
            {
                throw new Exception("未找到地址" + Key);
            }
            return WriteDevice(d,values);
        }
        
        public bool WriteAddress<T>(string Address, T values)
        {
            var count = DataHelper.GetAddrLength<T>(values);
            BaseDevice dv = this.CreateDevice(Address, count);
            return WriteDevice(dv, values);
        }
        
        public bool SetBitValue(string key, int bit_sn, bool flag)
        {
            if (DicDevice.TryGetValue(key, out BaseDevice d))
            {
                if (flag)
                {
                    return d.WriteBit(bit_sn);
                }
                else
                {
                    return d.ClearBit(bit_sn);
                }
            }
            return false;
        }

        public bool ReverseBit(string key, int bit_sn)
        {
            if (DicDevice.TryGetValue(key, out BaseDevice d))
            {
                return d.ReverseBit(bit_sn);
            }
            return false;
        }

        public bool CheckIsBool(string Address)
        {
            return CheckAddress(Address, out string tp, out int num);
        }
        public bool CheckTcp()
        {
            if (this.Client != null)
            {
                return Client.Connected();
            }
            return false;
        }

        public void Close()
        {
            if (this.Client != null)
            {
                Client.Close();
            }
        }
        #endregion
        #region 私有方法
        private bool WriteDevice<T>(BaseDevice dv, T values)
        {
            //var dv = (HostDevice)d;
            byte[] value ;
            value = new byte[dv.len * 2];
            var vs = dv.DataToUShort(values);
            List<byte> s0 = new List<byte>();
            foreach (var a in vs)
            {
                s0.AddRange(BitConverter.GetBytes(a));
            }
            if (s0.Count == value.Length)
            {
                value = s0.ToArray();
            }
            else if (s0.Count < value.Length)
            {
                s0.ToArray().CopyTo(value, 0);
            }
            else
            {
                Array.Copy(s0.ToArray(),0, value,0, value.Length);
            }
            if (!dv.Write(value))
            {
                if (!dv.Write(value))
                {
                    if (!dv.Write(value))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
      
        private bool CheckAddress(string Address, out string type, out int num)
        {//是否bool型地址
           
            type = "";
            num = 0;
            if (Address.Contains("#"))
            {
                var ss = Address.Split("#");
                type = ss[0];
                num = Convert.ToInt32(ss[1]);
            }
            else if (Address.Contains("*"))
            {
                var ss = Address.Split("#");
                type = ss[0];
                num = Convert.ToInt32(ss[1]);
            }
            else
            {
                throw new Exception("地址格式错误。格式=功能码#起始地址");
            }
            int tp = Convert.ToInt32(type);
            if (tp==1||tp==2)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

     

        #endregion

    }
}
