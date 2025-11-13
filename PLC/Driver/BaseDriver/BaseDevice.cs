
 
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;


namespace PLC.BaseDriver
{
     
    public abstract  class BaseDevice:IDevice
    {
        #region 变量
        public delegate void delegate_DataChange_Func(BaseDevice da);
        public delegate_DataChange_Func delegate_DataChange { get; set; }
        public int plcMemory { get; set; } = -1;//初始一个错误的地址类型

        public delegate IPLC delegate_GetTcpClient();//获取客户端连接实例
        public delegate_GetTcpClient _GetTcpClient { get; set; }
        //public SocketTcpClient client { get; set; }

        public string IPaddress { get; set; }

        public IPLC PlcClient
        {
            get
            {
                if (_GetTcpClient != null)
                {
                    return _GetTcpClient.Invoke();
                }
                return null;
            }
        }

        public object Values { get; set; }

        /// <summary>
        /// 指定关联地址
        /// </summary>
        public string NextTag { get; set; }

        private bool IsRun { get; set; }
        /// <summary>
        /// 主键
        /// </summary>
        public string Key { get; set; }

        public List<string> UID = new List<string>();

        public int Id { get; set; }

        /// <summary>
        /// 自定义值
        /// </summary>
        public int? Num { get; set; }

        //编号
        public string Code { get; set; }
        //名称
        public string Name { get; set; }
        /// <summary>
        /// 是否开关型地址
        /// </summary>
        public bool IsBool { get; set; }
        /// <summary>
        /// 响应耗时毫秒
        /// </summary>
        public double Ms { get; set; }

        /// <summary>
        /// 地址类型
        /// </summary>
        public string dType { get; set; }
        /// <summary>
        /// 起始地址
        /// </summary>
        public int StartNum { get; set; }
        /// <summary>
        /// 地址长度
        /// </summary>
        public int len { get; set; }
        /// <summary>
        /// 扫描周期毫秒
        /// </summary>
        public int Scan { get; set; }
        /// <summary>
        /// 读取指令
        /// </summary>
        public List<byte[]> CMD { get; set; }

        /// <summary>
        /// 下个读取指令
        /// </summary>
        public List<byte[]> NextCMD { get; set; }

        /// <summary>
        /// 上次扫描时间
        /// </summary>
        public DateTime ts { get; set; }

        /// <summary>
        /// 读到字节
        /// </summary>
        public byte[] ByteValues { get; set; }
        /// <summary>
        /// 读到字
        /// </summary>
        public ushort[] WordValues { get; set; }
        /// <summary>
        /// 新数据
        /// </summary>
        public bool DataChange { get; set; }
        /// <summary>
        /// 响应字节数
        /// </summary>
        public int ByteCount { get; set; }


        /// <summary>
        /// 数据类型
        /// </summary>
        public Type dataType { get; set; } = typeof(ushort[]);
        public List<int[]> AddressItems { get; set; } = new List<int[]>();

        #endregion
        #region 虚构方法

        public virtual void Create(string type, int num, int len, int ms) 
        {
            AddressItems.Add(new int[] { num, len });
        }
        public virtual  void ScanTask()
        {
            var err = 0;
            while (true)
            {
                if (Scan <= 0)
                {//跳过
                    //await Task.Delay(3000);
                    break;
                }
                //await Task.Delay(50);
                System.Threading.Thread.Sleep(50);
                if (PlcClient == null || !PlcClient.Conneted)
                {
                    continue;
                }
                if (DateTime.Now.Subtract(ts).TotalMilliseconds >= Scan)
                {//执行
                    try
                    {
                        if (ReadFormPLC() != null)
                        {//读成功了才报
                            if ((DataChange || !IsRun) && delegate_DataChange != null)
                            {
                                delegate_DataChange.Invoke(this);
                                DataChange = false;
                                IsRun = true;
                            }
                            err = 0;
                            if (string.IsNullOrEmpty(IPaddress))
                            {
                                IPaddress = PlcClient.BasicClient.socket.RemoteEndPoint.ToString();
                            }
                        }
                        
                    }
                    catch (Exception e)
                    {
                        if (err == 0)
                        {
                            Console.WriteLine($"{IPaddress}>ScanTask>ReadFormPLC>{this.dType}{this.StartNum},len={this.len} " + e.Message);
                        }
                        err++;
                        //await Task.Delay(3000);
                        System.Threading.Thread.Sleep(5000);
                    }
                }
            }
        }
    
        public virtual ushort[] ReadFormPLC()
        {//读取字
            ts = DateTime.Now;
            if (PlcClient != null)
            {
                var data = new ushort[len];
                int n = 0;
                if (AddressItems.Count == 0)
                {
                    AddressItems.Add(new int[] { StartNum, len });
                }
                foreach (var adr in AddressItems)
                {
                    var d = PlcClient.ReadWords(plcMemory, adr[0], adr[1]);
                    d.CopyTo(data,n);
                    n += d.Length;
                }
                if (n != len)
                {
                    throw new Exception($"响应长度错误。响应长度{n}，请求长度{len},{this.dType}{this.StartNum}");
                }
                //data = PlcClient.ReadWords(plcMemory, (short)this.StartNum, (short)this.len);

                for (int i = 0; i < len; i++)
                {
                    var old = WordValues[i];
                    this.WordValues[i] = data[i];
                    if (old != this.WordValues[i])
                    {
                        DataChange = true;
                    }
                    var bs = BitConverter.GetBytes(data[i]);
                    this.ByteValues[i * 2] = bs[0];
                    this.ByteValues[i * 2 + 1] = bs[1];
                }
                this.Ms = DateTime.Now.Subtract(ts).TotalMilliseconds;
              
                return data;
            }
            this.Ms = double.MaxValue;
            return null;
        }
        public virtual byte[] Read()
        {
            var data = ReadFormPLC();
            if (data != null)
            {
                return this.ByteValues;
            }
            return null;
        }
        public virtual bool Write(byte[] values) 
        {
            if (PlcClient != null)
            {
                var clen = values.Length;
                var data = new ushort[len];
                for (int i = 0; i < len; i++)
                {
                    if (i * 2 < clen)
                    {
                        data[i] = BitConverter.ToUInt16(values, i * 2);
                    }
                }
                return PlcClient.WriteWords(plcMemory, this.StartNum, this.len, data);
            }
            return false;
        }
        public virtual string ReadToString(byte[] values = null) 
        {
            if (values == null)
            {
                values = Read();
            }
            return Encoding.GetEncoding("gb2312").GetString(values);
        }
        public virtual byte[] ReadToByte(byte[] values = null)
        {
            if (values == null)
            {
                values = Read();
            }
            return values;
        }
       
        public virtual bool[] ReadToBool(byte[] values = null)
        {
            if (values == null)
            {
                values = Read();
            }
            bool[] list = new bool[values.Length * 8];
            int row = 0;
            foreach (var a in values)
            {
                for (int i = 0; i < 8; i++)
                {
                    list[row++] = (a >> i & 0x01) > 0 ? true : false;
                }
            }
            return list;
        }
      
        public virtual ushort[] ReadToUshort(byte[] values = null)
        {
            if (values == null)
            {
                return ReadFormPLC();
            }
            int c = values.Length / 2;
            var vs = new ushort[c];
            for (int i = 0; i < c; i++)
            {
                vs[i] = BitConverter.ToUInt16(values, i * 2);
            }
            return vs;
        }
  
        public virtual short[] ReadToShort(byte[] values = null)
        {
            if (values == null)
            {
                values = Read();
            }
            int c = values.Length / 2;
            var vs = new short[c];
            for (int i = 0; i < c; i++)
            {
                vs[i] = BitConverter.ToInt16(values, i * 2);
            }
            return vs;
        }
 
        public virtual int[] ReadToInt(byte[] values = null)
        {
            if (values == null)
            {
                values = Read();
            }
            int c = values.Length / 4;
            var vs = new int[c];
            for (int i = 0; i < c; i++)
            {
                vs[i] = BitConverter.ToInt32(values, i * 4);
            }
            return vs;
        }
 
        public virtual uint[] ReadToUInt(byte[] values = null)
        {
            if (values == null)
            {
                values = Read();
            }
            int c = values.Length / 4;
            var vs = new uint[c];
            for (int i = 0; i < c; i++)
            {
                vs[i] = BitConverter.ToUInt32(values, i * 4);
            }
            return vs;
        }
 
        public virtual float[] ReadToFloat(byte[] values = null)
        {
            if (values == null)
            {
                values = Read();
            }
            int c = values.Length / 4;
            var vs = new float[c];
            for (int i = 0; i < c; i++)
            {
                vs[i] = BitConverter.ToSingle(values, i * 4);
            }
            return vs;
        }
 
        public virtual bool ReadBit(int sn,byte[] data=null)
        {
            if (data == null)
            {
                data = Read();
            }
            int bit_sn = sn % 8;
            if ((data[sn / 8] >> bit_sn & 0x01) > 0)
            {
                return true;
            }
            return false;
        }

 
        public virtual bool WriteBit(int sn, byte[] data = null)
        {

            
            bool send = false;
            if (data == null)
            {
                data = Read();
                send = true;
            }
            int bit_sn = sn % 8;
            data[sn / 8] = (byte)(data[sn / 8] | (0x01 << bit_sn));
            if (send)
            {
                return Write(data);
            }
            else
            {
                return true;
            }
        }
 
        public virtual bool ClearBit(int sn, byte[] data = null)
        {
           
            bool send = false;
            if (data == null)
            {
                data = Read();
                send = true;
            }
            int bit_sn = sn % 8;

            data[sn / 8] = (byte)(data[sn / 8] & (~(0x01 << bit_sn)));
            if (send)
            {
                return Write(data);
            }
            else
            {
                return true;
            }
        }

   
        public virtual bool ReverseBit(int sn, byte[] data = null)
        {
            if (data == null)
            {
                data = Read();
            }
            int bit_sn = sn % 8;

            data[sn / 8] = (byte)(data[sn / 8] ^ (0x01 << bit_sn));
            return Write(data);
        }
      
        #endregion
        public virtual T Read<T>()
        {
            Type t = typeof(T);
            object value = null;
            if (t == typeof(ushort))
            {
                value = ReadToUshort()[0];
            }
            else if (t == typeof(short))
            {
                value = ReadToShort()[0];
            }
            else if (t == typeof(int))
            {
                value = ReadToInt()[0];
            }
            else if (t == typeof(uint))
            {
                value = ReadToUInt()[0];
            }
            else if (t == typeof(float))
            {
                value = ReadToUInt()[0];
            }
            else if (t == typeof(ushort[]))
            {
                value = ReadToUshort();
            }
            else if (t == typeof(short[]))
            {
                value = ReadToShort();
            }

            else if (t == typeof(bool[]))
            {
                value = ReadToBool();
            }
            else if (t == typeof(int[]))
            {
                value = ReadToInt();
            }
            else if (t == typeof(uint[]))
            {
                value = ReadToUInt();
            }
            else if (t == typeof(float[]))
            {
                value = ReadToFloat();
            }
            else if (t == typeof(string))
            {
                value = ReadToString();
            }
            else
            {
                throw new Exception("不支持的格式" + t.Name);
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public virtual async Task<bool> WriteAsync<T>(T values)
        {
            if (PlcClient != null)
            {
                ushort[] uv = DataToUShort(values);
                if (uv.Length != this.len)
                {
                    var vs = new ushort[this.len];
                    if (uv.Length > this.len)
                    {
                        Array.Copy(uv, 0, vs, 0, this.len);
                    }
                    else
                    {
                        Array.Copy(uv, 0, vs, 0, uv.Length);
                    }
                    uv = vs;
                }
                //return await PlcClient.WriteWordsAsync(plcMemory, this.StartNum, this.len, uv);
                return await WritePLCAsync(uv);
            }
            return false;
        }

        public virtual bool Write<T>(T values)
        {
            if (PlcClient != null)
            {
                ushort[] uv = DataToUShort(values);
                if (uv.Length != this.len)
                {
                    var vs = new ushort[this.len];
                    if (uv.Length > this.len)
                    {
                        Array.Copy(uv, 0, vs, 0, this.len);
                    }
                    else
                    {
                        Array.Copy(uv, 0, vs, 0, uv.Length);
                    }
                    uv = vs;
                }
                //return PlcClient.WriteWords(plcMemory, this.StartNum, this.len, uv);
                return WritePLC(uv);
            }
            return false;
        }

        public bool WritePLC(ushort[] values)
        {
            var result = true;
            if (AddressItems.Count == 0)
            {
                AddressItems.Add(new int[] { StartNum, len });
            }
            int index = 0;
            foreach (var adr in AddressItems)
            {
                int start = adr[0],len= adr[1];
                var uv = new ushort[len];
                Array.Copy(values, index, uv, 0, len);
                index += len;
                if (!PlcClient.WriteWords(plcMemory, start, len, uv))
                {
                    result = false;
                    break;
                }

            }
            return result;
        }
        public async Task<bool> WritePLCAsync(ushort[] values)
        {
            var result = true;
            if (AddressItems.Count == 0)
            {
                AddressItems.Add(new int[] { StartNum, len });
            }
            int index = 0;
            foreach (var adr in AddressItems)
            {
                int start = adr[0], len = adr[1];
                var uv = new ushort[len];
                Array.Copy(values, index, uv, 0, len);
                index += len;
                if (! await PlcClient.WriteWordsAsync(plcMemory, start, len, uv))
                {
                    result = false;
                    break;
                }

            }
            return result;
        }
        /// <summary>
        /// 写入数据转成ushort数组
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public virtual ushort[] DataToUShort(object values)
        {//用于写入
            Type p = values.GetType();
            ushort[] vs = null;
            if (p == typeof(short[]) || p == typeof(short))
            {
                if (p == typeof(short))
                {
                    vs = new ushort[] { (ushort)(short)values };
                }
                else
                {
                    var s1 = (short[])values;
                    var c = s1.Length;
                    vs = new ushort[c];
                    for (int i = 0; i < c; i++)
                    {
                        vs[i] = (ushort)s1[i];
                    }
                }
            }
            else if (p == typeof(ushort[]) || p == typeof(ushort))
            {
                if (p == typeof(ushort))
                {
                    vs = new ushort[] { (ushort)values };
                }
                else
                {
                    vs = (ushort[])values;
                }
            }
            else if (p == typeof(int) || p == typeof(int[]))
            {
                if (p == typeof(int))
                {
                    var bs = BitConverter.GetBytes((int)values);
                    vs = new ushort[] { BitConverter.ToUInt16(bs, 0), BitConverter.ToUInt16(bs, 2) };
                }
                else
                {
                    var s1 = (int[])values;
                    vs = new ushort[s1.Length * 2];
                    int n = 0;
                    foreach (var a in s1)
                    {
                        var bs = BitConverter.GetBytes(a);
                        vs[n++] = BitConverter.ToUInt16(bs, 0);
                        vs[n++] = BitConverter.ToUInt16(bs, 2);
                    }
                }
            }
            else if (p == typeof(uint) || p == typeof(uint[]))
            {
                if (p == typeof(uint))
                {
                    var bs = BitConverter.GetBytes((uint)values);
                    vs = new ushort[] { BitConverter.ToUInt16(bs, 0), BitConverter.ToUInt16(bs, 2) };
                }
                else
                {
                    var s1 = (uint[])values;
                    vs = new ushort[s1.Length * 2];
                    int n = 0;
                    foreach (var a in s1)
                    {
                        var bs = BitConverter.GetBytes(a);
                        vs[n++] = BitConverter.ToUInt16(bs, 0);
                        vs[n++] = BitConverter.ToUInt16(bs, 2);
                    }
                }
            }
            else if (p == typeof(float) || p == typeof(float[]))
            {
                if (p == typeof(float))
                {
                    var bs = BitConverter.GetBytes((float)values);
                    vs = new ushort[] { BitConverter.ToUInt16(bs, 0), BitConverter.ToUInt16(bs, 2) };
                }
                else
                {
                    var s1 = (float[])values;
                    vs = new ushort[s1.Length * 2];
                    int n = 0;
                    foreach (var a in s1)
                    {
                        var bs = BitConverter.GetBytes(a);
                        vs[n++] = BitConverter.ToUInt16(bs, 0);
                        vs[n++] = BitConverter.ToUInt16(bs, 2);
                    }
                }
            }
            else if (p == typeof(long) || p == typeof(long[]))
            {
                if (p == typeof(long))
                {
                    var bs = BitConverter.GetBytes((long)values);
                    vs = new ushort[] { BitConverter.ToUInt16(bs, 0), BitConverter.ToUInt16(bs, 2),
                                            BitConverter.ToUInt16(bs, 4) ,BitConverter.ToUInt16(bs, 6) };
                }
                else
                {
                    var s1 = (long[])values;
                    vs = new ushort[s1.Length * 4];
                    int n = 0;
                    foreach (var a in s1)
                    {
                        var bs = BitConverter.GetBytes(a);
                        vs[n++] = BitConverter.ToUInt16(bs, 0);
                        vs[n++] = BitConverter.ToUInt16(bs, 2);
                        vs[n++] = BitConverter.ToUInt16(bs, 4);
                        vs[n++] = BitConverter.ToUInt16(bs, 6);

                    }
                }
            }
            else if (p == typeof(double) || p == typeof(double[]))
            {
                if (p == typeof(double))
                {
                    var bs = BitConverter.GetBytes((double)values);
                    vs = new ushort[] { BitConverter.ToUInt16(bs, 0), BitConverter.ToUInt16(bs, 2),
                                            BitConverter.ToUInt16(bs, 4) ,BitConverter.ToUInt16(bs, 6) };
                }
                else
                {
                    var s1 = (double[])values;
                    vs = new ushort[s1.Length * 4];
                    int n = 0;
                    foreach (var a in s1)
                    {
                        var bs = BitConverter.GetBytes(a);
                        vs[n++] = BitConverter.ToUInt16(bs, 0);
                        vs[n++] = BitConverter.ToUInt16(bs, 2);
                        vs[n++] = BitConverter.ToUInt16(bs, 4);
                        vs[n++] = BitConverter.ToUInt16(bs, 6);

                    }
                }
            }
            else if (p == typeof(string))
            {
                var str = values.ToString();
                var c = str.Length;
                if (c % 2 == 1)
                {
                    str += "\0";
                    c++;
                }
                vs = new ushort[c / 2];
                var bs = System.Text.Encoding.GetEncoding("gb2312").GetBytes(str);
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i] = BitConverter.ToUInt16(new byte[] { bs[i * 2 + 1], bs[i * 2] });//默认字节转换
                }
            }
            else if (p == typeof(byte[]))
            {
                var s1 = (byte[])values;
                var c = s1.Length;
                if (c % 2 == 1)
                {
                    var s2 = new List<byte>();
                    s2.AddRange(s1);
                    s2.Add(0);
                    s1 = s2.ToArray();
                }
                c = s1.Length;

                vs = new ushort[c / 2];
                var bs = s1;
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i] = BitConverter.ToUInt16(new byte[] { bs[i * 2 + 1], bs[i * 2] });//默认字节转换
                }
            }
            else
            {
                throw new Exception("不支持的数据类型");
            }
            return vs;
        }
 
    }

     
}
