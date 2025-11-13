using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using PLC.BaseDriver;
using PLC.Interface;


namespace PLC.KvHost
{
    public class HostLinkManage: IDriver
    {
        IPLC  PLC;
        Dictionary<string, BaseDevice> DicDevice = new Dictionary<string, BaseDevice>();//PLC地址
        object _lock = new object();
        public IPLC GetClient()
        {
            return this.PLC;
        }
        public bool Conneted
        {
            get
            {
                if (PLC != null)
                {
                    return PLC.Conneted;
                }
                return false;
            }
        }
        #region 构造方法
        public HostLinkManage(string IP,bool IsAuto=true,bool IsUDP=false)
        {
            int port = 8501;
            var ss = IP.Split(':');
            if (ss.Length > 1)
            {
                port = int.Parse(ss[1]);
            }
            PLC = HostLinkPLC.Create(IsUDP);

            if (IsAuto == false|| IsUDP)
            {//短连接
                PLC.BasicClient.ConnectAsync(ss[0], port, 3000).GetAwaiter().GetResult();
                return;
            }
            IsAutoConnet = IsAuto;
            AutoConnet(ss[0], port);
        }
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
            if (!CheckAddress(Address,out string type,out int index,out int bit))
            {
                throw new Exception($"PLC地址错误{Address}");
            }
            var key = $"{type}{index}#{count}#";
            if (!DicDevice.TryGetValue(key, out BaseDevice d))
            {
                d = new HostLinkDevice();
                d.Key = key;
                d._GetTcpClient = GetClient;//PLC接口
                d.Create(type, index, count, ms);
                DicDevice[key] = d;
            }
            else
            {
                if (ms > 0)
                {//需要扫描
                    if(d.Scan<=0)
                    {
                        new Thread(d.ScanTask) { IsBackground = true }.Start();    
                    }
                }
                    d.Scan = ms;
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
        public T Read<T>(string Key)
        {
            if (!DicDevice.TryGetValue(Key, out BaseDevice d))
            {
                throw new Exception("未找到地址" + Key);
            }
            return d.Read<T>();
        }
        /// <summary>
        /// 读取地址字
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="Address">地址编号</param>
        /// <param name="len">返回值数组长度</param>
        /// <returns></returns>
        public T ReadAddress<T>(string Address,int len=1)
        {
            var count = DataHelper.GetAddrLength<T>(len);
            BaseDevice dv = this.CreateDevice(Address, count);
            if (typeof(T) == typeof(bool))
            {
                CheckAddress(Address,out string tp,out int num,out int bit);
                if (bit > -1 || ((HostLinkDevice)dv).memoryType== MemoryType.Bit)
                {//直接返回位值
                    return (T)Convert.ChangeType(dv.ReadBit(bit), typeof(T));
                }
            }
            return dv.Read<T>();
        }
        public bool Write<T>(string Key, T values)
        {
            if (!DicDevice.TryGetValue(Key, out BaseDevice dv))
            {
                throw new Exception("未找到地址" + Key);
            }
            if (!dv.Write(values))
            {
                if (!dv.Write(values))
                {
                    if (!dv.Write(values))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public bool WriteAddress<T>(string Address, T values)
        {
            var count = DataHelper.GetAddrLength<T>(values);
            BaseDevice dv = this.CreateDevice(Address, count);
            if (typeof(T) == typeof(bool))
            {
                CheckAddress(Address, out string tp, out int num, out int bit);
                if (bit > -1 || ((HostLinkDevice)dv).memoryType == MemoryType.Bit)
                {//直接写入位值
                    var value = (bool)(object)values;
                    if (value)
                    {
                        return dv.WriteBit(bit);
                    }
                    else
                    {
                        return dv.ClearBit(bit);
                    }
                }
            }
            if (!dv.Write(values))
            {
                if (!dv.Write(values))
                {
                    if (!dv.Write(values))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public async Task<bool> WriteAddressAsync<T>(string Address, T values)
        {
            var count = DataHelper.GetAddrLength<T>(values);
            BaseDevice dv = this.CreateDevice(Address, count);
            if (typeof(T) == typeof(bool))
            {
                CheckAddress(Address, out string tp, out int num, out int bit);
                if (bit > -1 || ((HostLinkDevice)dv).memoryType == MemoryType.Bit)
                {//直接写入位值
                    var value = (bool)(object)values;
                    if (value)
                    {
                        return dv.WriteBit(bit);
                    }
                    else
                    {
                        return dv.ClearBit(bit);
                    }
                }
            }
            if ( !await dv.WriteAsync(values))
            {
                if (!await dv.WriteAsync(values))
                {
                    if (!await dv.WriteAsync(values))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">字典键或者地址</param>
        /// <param name="bit_sn"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public bool SetBitValue(string key, int bit_sn, bool flag)
        {
            if (!DicDevice.TryGetValue(key, out BaseDevice d))
            {
                d = CreateDevice(key, 1);
            }
            if (flag)
            {
                return d.WriteBit(bit_sn);
            }
            else
            {
                return d.ClearBit(bit_sn);
            }
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
            return false;
        }
        public bool CheckTcp()
        {
            return this.Conneted;
        }

        public void Close()
        {
            IsAutoConnet = false;//停止不重联
            PLC.Close();
        }
        #endregion
        #region 私有方法

        bool IsAutoConnet = false;

        private void AutoConnet(string ip ,int port)
        {
            new Thread (async () => {
                try
                {
                    await PLC.BasicClient.ConnectAsync(ip, port, 3000);
                }
                catch (Exception e)
                {
                   
                }
                finally
                {
                    Console.WriteLine(ip+":"+ port + "连接：" + PLC.Conneted);
                }
                while (IsAutoConnet)
                {//自动连接
                    await Task.Delay(3000);
                    try
                    {
                        if (!PLC.Conneted)
                        {
                            if (await PLC.BasicClient.ReConnectAsync() == false)
                            {
                                await Task.Delay(5000);
                            }
                            else
                            {
                                Console.WriteLine($"{ip}重连成功");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        await Task.Delay(5000);
                    }
                }
            })
            { IsBackground = true }.Start();
        }

        //解析地址
        private bool CheckAddress(string Address, out string type, out int num,out int bit)
        {
            type = "";
            num = -1;
            bit = -1;
            try
            {
                string s = "";
                foreach (var a in Address)
                {
                    if (!Char.IsNumber(a))
                    {
                        s += a;
                    }
                    else
                    {
                        break;
                    }
                }
                type = s;
                var snum = Address.Replace(s, "").Split('.');
                if (snum.Length > 1)
                {
                    bit = int.Parse(snum[1]);
                }
                num = Convert.ToInt32(snum[0]);
                if (!string.IsNullOrEmpty(type) && num > -1)
                {
                    return true;
                }
            }
            catch
            {
                
            }
            return false;
        }

        public Task<T> ReadAddressAsync<T>(string Address, int len = 1)
        {
            throw new NotImplementedException();
        }

      



        #endregion

    }
}
