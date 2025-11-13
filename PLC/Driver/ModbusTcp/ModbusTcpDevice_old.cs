using PLC.BaseDriver;
using PLC.Tcp;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PLC.ModbusTcp
{
    /// <summary>
    /// 上位链路数据模型
    /// </summary>
    public class ModbusTcpDevice:BaseDevice
    {
        static short id = 0;
        private short taskflag;
        bool run = false;
        public ModbusTcpDevice(SocketTcpClient c=null)
        {
            this.client = c;
             
        }

        #region 方法重载
        public override void Create(string tp, int index, int count, int ms)
        {
            Key = $"{tp}{index}#{count}#"; // Guid.NewGuid().ToString();
            taskflag = ++id;
            this.Id = taskflag;//标识
            ts = DateTime.Now;
            dType = tp;
            StartNum = index;
            len = count;
            Scan = ms;
            //IsBool = CheckBooll();
            if (Scan > 0 && Scan < 10)
            {//最少10ms
                Scan = 10;
            }
            ByteCount = GetByteCount(count);
            if (IsBool)
            {
                WordValues = new ushort[(int)Math.Ceiling(count / 16.0)];
                ByteValues = new byte[(int)Math.Ceiling(count / 8.0)];
            }
            else
            {
                WordValues = new ushort[count];
                ByteValues = new byte[count * 2];
            }
      
            CMD = ReadCMD();
            if (Scan > 0)
            {
                new Thread(new ThreadStart(ScanTask)) { IsBackground = true }.Start();
            }

        }
        /// <summary>
        /// 直接读取当前数据
        /// </summary>
        /// <returns></returns>
        public override byte[] Read()
        {
            try
            {
                List<byte> data = new List<byte>(); 
                int n = 0,mc= CMD.Count;
                for (int i = 0; i < mc; i++)
                { //响应【报文头7】【功能码1】【字节数1】n【字节数据1】
                    var count = BitConverter.ToUInt16(new byte[] { CMD[i][11], CMD[i][10] });
                    var sumbs = GetByteCount(count);
                    var d = client.Send(CMD[i], sumbs);//读取指定字节数
                    if (d[7] != CMD[i][7])
                    {
                        throw new Exception("指令错误");
                    }
                    else if (d[0] != CMD[i][0] || d[1] != CMD[i][1])
                    {
                        throw new Exception("标识错误");
                    }
                    else if (d.Length != sumbs)
                    {
                        throw new Exception("响应字节数错误");
                    }
                    //var ss = new byte[count];
                    var c = d[8];//字节数
                    for (int j = 0; j < d[8]; j++)
                    {
                        data.Add(d[j+9]);
                    }
                    n += count;
                }
                if (!SetValue(data.ToArray()))
                {
                    throw new Exception("解析异常");
                }
                return this.ByteValues;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw e;
            }
        }
 
        /// <summary>
        /// 字节写入PLC
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public override bool Write(byte[] values)
        {

            if (IsBool)
            {
                if (values.Length * 8 != len)
                {
                    byte[] vs = new byte[(int)(Math.Ceiling(len/8.0))];
                    int c = values.Length > vs.Length ? vs.Length : values.Length;
                    Array.Copy(values, vs, c);
                    values = vs;
                }
            }
            else
            {
                if (values.Length != len * 2)
                {
                    byte[] vs = new byte[len*2];
                    int c = values.Length > vs.Length ? vs.Length : values.Length;
                    Array.Copy(values, vs, c);
                    values = vs;
                }
            }
            //Console.WriteLine("数据转耗时 "+DateTime.Now.Subtract(t).TotalMilliseconds) ;
            //t = DateTime.Now;

            bool ck = true;
            var cmds = WriteCMD(values);
            //Console.WriteLine("CMD耗时 " + DateTime.Now.Subtract(t).TotalMilliseconds);
            //t = DateTime.Now;
            foreach (var a in cmds)
            {
                var data = client.Send(a);
                if (data[0] != a[0] || data[1]!=a[1])
                {
                    throw new Exception("标识错误");
                }
                else if (data[7] != a[7])
                {
                    throw new Exception("指令错误");
                }
            }
            //Console.WriteLine("写入耗时 " + DateTime.Now.Subtract(t).TotalMilliseconds);
            //t = DateTime.Now;
            return ck;
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
            for (int i = 0,c= values.Length; i < c; i+=2)
            {//字节倒过来
                if (i + 1 < c)
                {
                    var b = values[i];
                    values[i] = values[i + 1];
                    values[i + 1] = b;
                }
            }
            var ss= Encoding.GetEncoding("gb2312").GetString(values);
            return ss.Replace("\0", "");
         
        }

        public override bool[] ReadToBool(byte[] values = null)
        {
            if (values == null)
            {
                values = Read();
            }
            bool[] list;
            if (IsBool)
            {
                list = new bool[len];
            }
            else
            {
                list = new bool[len*16];
            }

            int row = 0,count= list.Length;
            foreach (var a in values)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (row >= count)
                    {
                        break;
                    }
                    list[row++] = (a >> i & 0x01) > 0 ? true : false;
                }

            }
            return list;
        }

 
 
        #endregion
        #region 私有方法

        /// <summary>
        /// 响应字节数
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private int GetByteCount(int num)
        {//响应【报文头7】【功能码1】【字节数1】n【字节数据1】
            if (IsBool)
            {
                return 7+1+1+(int)Math.Ceiling(num / 8.0);
            }
            return 7 + 1 + 1 + num * 2; 
        }
        /// <summary>
        /// 转换成读取指令,超1000字的分多条
        /// </summary>
        /// <param name="tp">响应类型</param>
        /// <returns></returns>
        private List<byte[]> ReadCMD(string tp = "")
        {
            double max = IsBool ? 100*16 : 100;//bool最大1600，字最大100
            List<byte[]> cmd = new List<byte[]>();
            int index = StartNum;
            var last = (int)(len % max) == 0 ? (int)max : (int)(len % max);
            int c = (int)Math.Ceiling(len / max);
            for (int i = 0; i < c; i++)
            {
                var n = i == (c - 1) ? last : (int)max;
                //报文头:事务元标识符（2个字节）+协议标识符（2个字节）+长度（2个字节）+单元标识符（1个字节）
                //【报文头7】【功能码1】【起始地址2】【地址个数2】
                var head = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x01, 0x00, 0x00, 0x00, 0x10 };
                var h01 = BitConverter.GetBytes(this.taskflag);//事务元标识符
                head[0] = h01[1];
                head[1] = h01[0];
                head[7] = Convert.ToByte(this.dType);//功能码
                var h89 = BitConverter.GetBytes(index);//起始地址2
                head[8] = h89[1];
                head[9] = h89[0];
                var h1011 = BitConverter.GetBytes(n);//数量
                head[10] = h1011[1];
                head[11] = h1011[0];
                cmd.Add(head);
                index += n;
            }
            return cmd;
        }


        /// <summary>
        /// 转换成写入指令
        /// </summary>
        /// <param name="tp">响应类型</param>
        /// <returns></returns>
        private List<byte[]> WriteCMD(byte[] values)
        {
            double max =IsBool?100*16:100;//最大100个字
            List<byte[]> cmd = new List<byte[]>();
            int index = StartNum;
            var last = (int)(len % max) == 0 ? (int)max : (int)(len % max);
            int bc =0;
            for (int i = 0; i < CMD.Count; i++)
            {
                int n = i == CMD.Count - 1 ? last : (int)max;
                var vs = new byte[n*2];
                if (IsBool)
                {
                    vs = new byte[(int)Math.Ceiling(n / 8.0)];
                }
                Array.Copy(values, bc, vs,0,vs.Length);//复制数据
                cmd.Add(SetWrite(Convert.ToByte(dType), (ushort)index,(ushort)n,vs));

                index += n;
                bc += vs.Length;
            }
            return cmd;
        }
        /// <summary>
        /// 写入格式
        /// </summary>
        /// <param name="dtp">地址类型</param>
        /// <param name="index">开始地址</param>
        /// <param name="count">地址个数</param>
        /// <param name="values">值</param>
        /// <returns></returns>
        private byte[] SetWrite(byte dtp,ushort index, ushort count, byte[] values)
        {
            byte[] head=new byte[0];
            ushort hlen = 0;
            byte handel = 0;
            if (dtp == 1)
            {//【报文头7】【功能码1】【起始地址2】【线圈个数2】【写入字节数1=线圈数/8】【字节】
                handel = 15;
                //byte bytec = (byte)Math.Ceiling(count/8.0);
                head = new byte[7 + 1 + 2 + 2 + 1 + values.Length];
                hlen = (ushort)(7 + values.Length);//长度
                var h89 = BitConverter.GetBytes(index);//起始地址2
                head[8] = h89[1];
                head[9] = h89[0];
                var h1011 = BitConverter.GetBytes(count);//地址个数
                head[10] = h1011[1];
                head[11] = h1011[0];
                head[12] = (byte)values.Length;

                for (int i = 0; i < values.Length; i ++)
                {
                    head[i + 13] = values[i];
                }
            }
            else if (dtp == 3)
            {//【报文头7】【功能码1】【起始地址2】【写入长度2】【写入字节数1】【字节】
                handel = 16;
                head = new byte[7 + 1 + 2 + 2 + 1 + values.Length];
                hlen = (ushort)(7 + values.Length);
                var h89 = BitConverter.GetBytes(index);//起始地址2
                head[8] = h89[1];
                head[9] = h89[0];
                var h1011 = BitConverter.GetBytes(count);//写入长度2
                head[10] = h1011[1];
                head[11] = h1011[0];
                head[12] = (byte)(values.Length);
                for (int i = 0; i < values.Length; i += 2)
                {//肯定是双数的
                    head[i + 13] = values[i + 1];
                    head[i + 14] = values[i];
                }
            }
            else
            {
                throw new Exception($"地址类型{dType}不支持写入");
            }
            //头文件
            var h01 = BitConverter.GetBytes(this.taskflag);//事务元标识符
            head[0] = h01[1];
            head[1] = h01[0];
            var h45 = BitConverter.GetBytes(hlen);//长度
            head[4] = h45[1];
            head[5] = h45[0];
            head[6] = 0x01;
            head[7] = handel;//功能码

            return head;
           
        }

        /// <summary>
        /// 解释读到的字节转换成uint16
        /// </summary>
        /// <param name="bs"></param>
        /// <returns></returns>
        private bool SetValue(byte[] data)
        {
            try
            {
                if (IsBool)
                {
                    if (data.Length * 8 < len)
                    {
                        throw new Exception("解析数据异常，长度不正确");
                    }
                    ByteValues = data;
                    var uv = new byte[0];
                    if (data.Length % 2 != 0)
                    {
                        var list = new List<byte>();
                        list.AddRange(data);
                        list.Add(0);
                        uv = list.ToArray();
                    }
                    else
                    {
                        uv = data;
                    }
                    for (int i = 0; i < WordValues.Length; i++)
                    {
                        var w = BitConverter.ToUInt16(uv, i * 2);
                        if (WordValues[i] != w)
                        {
                            DataChange = true;
                        }
                        WordValues[i] = w;
                    }
                }
                else
                {//高低要转换
                    if (data.Length  != this.len*2)
                    {
                        throw new Exception("解析数据异常，长度不正确");
                    }

                    for (int i = 0; i < len; i++)
                    {
                        ByteValues[i * 2] = data[i * 2 + 1];
                        ByteValues[i * 2 + 1] = data[i * 2];
                        var wd = BitConverter.ToUInt16(ByteValues,i*2);
                        if (wd != WordValues[i])
                        {
                            DataChange = true;
                        }
                        WordValues[i] = wd;
 
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            return true;
        }

   
        /// <summary>
        /// 扫描线程
        /// </summary>
        public override void ScanTask()
        {
            while (true)
            {
                if (Scan <= 0)
                {//跳过
                    Thread.Sleep(5000);
                    continue;
                }
                Thread.Sleep(10);
                if (client == null && !client.Connected())
                {
                    continue;
                }
                if (DateTime.Now.Subtract(ts).TotalMilliseconds >= Scan)
                {//执行
                    ts = DateTime.Now;
                    try
                    {
                        Read();
                        if ((DataChange || !run) && delegate_DataChange != null)
                        {
                            Task.Run(() => {
                                delegate_DataChange.Invoke(this);
                            });
                            DataChange = false;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                        Thread.Sleep(3000);
                    }
                    run = true;
                }

            }
        }
        #endregion

    }
}
