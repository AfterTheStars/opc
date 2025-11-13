using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace PLC.BaseDriver
{
    public class DataHelper
    {
       /// <summary>
       /// 计算读取对应数据类型所需字数
       /// </summary>
       /// <typeparam name="T"></typeparam>
       /// <param name="len">数组长度/字符长度</param>
       /// <returns></returns>
        public static int GetAddrLength<T>(int len)
        {
            int count = len;
            Type p = typeof(T);
            if (p == typeof(short) || p == typeof(ushort))
            {
                count = 1;//这些类型一个字够了
            }
            else if (p == typeof(int) || p == typeof(uint) || p == typeof(float))
            {//最少需要两个字
                count = 2;
            }
            else if (p == typeof(long) || p == typeof(ulong) || p == typeof(double))
            {//最少需要4个字
                count = 4;
            }
            else if (p == typeof(short[])|| p == typeof(ushort[]))
            {
                count = len * 1;
            }
            else if (p == typeof(long[]) || p == typeof(ulong[]) || p == typeof(double[]))
            {
                count = len * 4;
            }
            else if (p == typeof(int[]) || p == typeof(uint[]) || p == typeof(float[]))
            {
                count = len * 2;
            }
            else if (p == typeof(bool[]))
            {
                count = (int)Math.Ceiling(len / 16.0);
            }
            else if (p == typeof(string))
            {
                count = (int)Math.Ceiling(len / 2.0);
            }
            else
            {
                throw new Exception("不支持的类型:" + p.Name);
            }
            //bool[]形的不清楚是要几个字
            return count;
        }
        /// <summary>
        /// 计算写入数据所占字数
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values">写入类型</param>
        /// <returns></returns>
        public static int GetAddrLength<T>(T values)
        {
            int count = 1;
            Type p = typeof(T);
            if (p == typeof(short)
                 || p == typeof(ushort)
                 )
            {
                count = 1;//这些类型一个字够了
            }
            else if (p == typeof(int)
                  || p == typeof(uint)
                  || p == typeof(float)
                  )
            {//最少需要两个字
                count = 2;
            }
            else if (p == typeof(long)
                 || p == typeof(ulong)
                 || p == typeof(double)
                 )
            {//最少需要4个字
                count = 4;
            }
            else if (p == typeof(short[]))
            {
                count = ((short[])(object)values).Length * 1;
            }
            else if (p == typeof(ushort[]))
            {
                count = ((ushort[])(object)values).Length * 1;
            }
            else if (p == typeof(int[]))
            {
                count = ((int[])(object)values).Length * 2;
            }
            else if (p == typeof(uint[]))
            {
                count = ((uint[])(object)values).Length * 2;
            }
            else if (p == typeof(float[]))
            {
                count = ((float[])(object)values).Length * 2;
            }
            else if (p == typeof(long[]))
            {
                count = ((long[])(object)values).Length * 4;
            }
            else if (p == typeof(ulong[]))
            {
                count = ((ulong[])(object)values).Length * 4;
            }
            else if (p == typeof(double[]))
            {
                count = ((double[])(object)values).Length * 4;
            }

            else if (p == typeof(string))
            {
                count = (int)Math.Ceiling(values.ToString().Length / 2.0);
            }
            else if (p == typeof(byte[]))
            {
                count = (int)Math.Ceiling(((byte[])(object)values).Length / 2.0);
            }
            else
            {
                throw new Exception("不支持的类型:" + p.Name);
            }
 
            return count;
        }


     

    }
}
