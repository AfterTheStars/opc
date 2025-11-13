using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PLC.BaseDriver;

namespace PLC.Interface
{
    public interface IDriver
    {
        bool Conneted { get; }

        IPLC GetClient();
  
        /// <summary>
        /// 创建PLC地址
        /// </summary>
        /// <param name="Address">地址</param>
        /// <param name="count">长度</param>
        /// <param name="ms">扫描周期</param>
        /// <param name="IsAsync">是否异步扫描</param>
        /// <returns></returns>
        BaseDevice CreateDevice(string Address, int count, int ms = 0);
    
        BaseDevice Device(string key);
        T Read<T>(string key);
        T ReadAddress<T>(string Address, int len = 1);
        Task<T> ReadAddressAsync<T>(string Address, int len = 1);
        //bool[] ReadBool(string Address, int len = 1);
        bool Write<T>(string key,T values);
        bool WriteAddress<T>(string Address, T values);

        Task<bool> WriteAddressAsync<T>(string Address, T values);

        //bool WriteBool(string Address, bool[] value, int len = 0);
        bool SetBitValue(string key, int bit_sn, bool flag);

        bool ReverseBit(string key, int bit_sn);
        /// <summary>
        /// 判断是否bool型地址
        /// </summary>
        /// <param name="Address"></param>
        /// <returns></returns>
        bool CheckIsBool(string Address);
        bool CheckTcp();
        void Close();
    }
}
