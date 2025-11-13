using System;
using System.Collections.Generic;

namespace PLC.BaseDriver
{
    public interface IDevice
    {

        /// <summary>
        /// 创建地址
        /// </summary>
        /// <param name="type">寄存器类型</param>
        /// <param name="num">起始地址</param>
        /// <param name="len">长度</param>
        /// <param name="ms">扫描周期毫秒</param>
        void Create(string type, int num, int len, int ms);
        byte[] Read();
        bool Write(byte[] values);
        bool Write<T>(T values);
        string ReadToString(byte[] values = null);
        bool[] ReadToBool(byte[] values = null);
        ushort[] ReadToUshort(byte[] values = null);
        short[] ReadToShort(byte[] values = null);
        int[] ReadToInt(byte[] values = null);
        uint[] ReadToUInt(byte[] values = null);
        float[] ReadToFloat(byte[] values = null);

        bool ReadBit(int sn, byte[] data = null);
        bool WriteBit(int sn,byte[] data=null);
        bool ClearBit(int sn, byte[] data = null);
        bool ReverseBit(int sn, byte[] data = null);

        ushort[] DataToUShort(object data);
       

    }
}
