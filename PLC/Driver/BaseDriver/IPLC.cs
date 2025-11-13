using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PLC.BaseDriver
{
   public interface IPLC
    {

        bool Conneted { get; }

        IBasicClient BasicClient { get; }
        void Close();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="cnt">长度</param>
        /// <param name="IsAsync"></param>
        /// <returns></returns>
        Task<ushort[]> ReadWordsAsync(int mr, int ch, int cnt, bool IsAsync = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="IsAsync"></param>
        /// <returns></returns>
        Task<ushort> ReadWordAsync(int mr, int ch, bool IsAsync = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="cnt">长度</param>
        /// <param name="inData">值</param>
        /// <param name="IsAsync"></param>
        /// <returns></returns>
        Task<bool> WriteWordsAsync(int mr, int ch, int cnt, ushort[] inData, bool IsAsync = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="inData">值</param>
        /// <param name="IsAsync"></param>
        /// <returns></returns>
        Task<bool> WriteWordAsync(int mr, int ch, ushort inData, bool IsAsync = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="IsAsync"></param>
        /// <returns></returns>
        Task<ushort> GetBitStateAsync(int mr, string ch, bool IsAsync = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="bs">值</param>
        /// <param name="IsAsync"></param>
        /// <returns></returns>
        Task<bool> SetBitStateAsync(int mr, string ch, bool bs, bool IsAsync = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="cnt">长度</param>
        /// <returns></returns>
        ushort[] ReadWords(int mr, int ch, int cnt);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <returns></returns>
        ushort ReadWord(int mr, int ch);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="cnt">长度</param>
        /// <param name="inData">值</param>
        /// <returns></returns>
        bool WriteWords(int mr, int ch, int cnt, ushort[] inData);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="inData">值</param>
        /// <returns></returns>
        bool WriteWord(int mr, int ch, ushort inData);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <returns></returns>
        ushort GetBitState(int mr, string ch);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mr">寄存器类型</param>
        /// <param name="ch">起始地址</param>
        /// <param name="bs">值</param>
        /// <returns></returns>
        bool SetBitState(int mr, string ch, bool bs);
        /// <summary>
        /// 获取PLC运行模式
        /// </summary>
        /// <returns></returns>
        string GetModel();
    }
}
