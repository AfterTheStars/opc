using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace PLC.Tcp
{
    public class SocketTcpClient
    {
        private Socket socket { get; set; }
        readonly object  _lock = new object();
        string _ip;
        int _port=8501;
        bool AutoConnet = true;
        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="sip">ip地址</param>
        /// <param name="sport">端口</param>
        /// <param name="IsAutoConnet">是否异步自动连接</param>
        public SocketTcpClient(string sip, int sport,bool IsAutoConnet=true)
        {
            _ip = sip;
            _port = sport;
            if (IsAutoConnet)
            {//异步连接 自动连接
                new Thread(new ThreadStart(CheckConnet)) { IsBackground = true }.Start();
            }
            else
            {
                if (!Conn())
                {
                    throw new Exception($"{_ip}:{_port}" + " 连接失败");
                }
            }
        }
        private bool Conn()
        {
            IPEndPoint remoteEP = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress address = IPAddress.Parse(_ip);
                remoteEP = new IPEndPoint(address, _port);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000);//设置接收超时
                this.socket.Connect(remoteEP);
                Console.WriteLine($"{_ip}:{_port}" + " 连接成功");
                return true;
            }
            catch
            {
                Console.WriteLine($"{_ip}:{_port}" + " 连接失败");
                return false;
            }
        }
        public void Close()
        {//关闭连接
            try
            {
                AutoConnet = false;
                socket.Close();
                socket.Dispose();
            }
            catch(Exception e)
            {
                throw e;
            }
        }
        /// <summary>
        /// 重新连接tcp
        /// </summary>
        /// <returns></returns>
        public bool ReConnet()
        {
            try
            {
                return Conn();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return false;
        }
        /// <summary>
        /// 发送&响应
        /// </summary>
        /// <param name="bs"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        public byte[] Send(byte[] bs, int len = 65536,bool CheckLength=false)
        {
            if (socket == null || !socket.Connected)
            {
                throw new Exception(this._ip + ":" + this._port + $" 未连接");
            }

            byte[] buf = new byte[len];
            int c = 0;
            lock (_lock)
            {
                this.socket.Send(bs);
                c = socket.Receive(buf);
                if (CheckLength && c < len)
                {//强制读回指定长度
                    try
                    {
                        while (c != len)
                        {
                            var d = new byte[len - c];
                            int n = socket.Receive(d);
                            Array.Copy(d, 0, buf, c, n);
                            c += n;
                        }
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }
                  
                    return buf;
                }
            }
            byte[] data = new byte[c];
            Array.Copy(buf, data, c);
            return data;
        }

        /// <summary>
        /// 单读取缓存数据
        /// </summary>
        /// <returns></returns>
        public byte[] Recive()
        {
            if (socket == null || !socket.Connected)
            {
                throw new Exception(this._ip + ":" + this._port + $" 未连接");
            }
            byte[] data = new byte[65536];
            try 
            {
                lock (_lock)
                {
                    socket.Receive(data);
                }
                return data;
            }
            catch (Exception e)
            {
                Console.WriteLine("接收超时"+e.Message);
            }
            return new byte[0];
        }
        /// <summary>
        /// 单提交数据
        /// </summary>
        /// <returns></returns>
        public void Post(byte[] bs)
        {
            if (socket == null || !socket.Connected)
            {
                throw new Exception(this._ip + ":" + this._port + $" 未连接");
            }
            socket.Send(bs);
        }

        /// <summary>
        /// 连接状态
        /// </summary>
        /// <returns></returns>
        public bool Connected()
        {
            if (socket == null)
            {
                return false;
            }
            return socket.Connected;
        }
        int maxrecon = 5;
        /// <summary>
        /// 自动重连服务器
        /// </summary>
        private void CheckConnet()
        {//重连接机制
            Conn();
            int count = 0;
            int nowc = -1;
            int hour = DateTime.Now.Hour;
            while (AutoConnet)
            {
                if (maxrecon > 180)
                {
                    maxrecon = 180;
                }
                if (!Connected() && count <= 0)
                {
                    lock (_lock)
                    {
                        if (!ReConnet())
                        {//重连接失败
                            count = maxrecon;
                            nowc = count;
                            maxrecon += 5;
                        }
                        else
                        {
                            count = 0;
                        }
                    }
                }
                if ((count > 0 && count<=3)|| count == nowc)
                {
                    Console.WriteLine(this._ip+":"+this._port+ $" 重连失败,{count}秒后重试");
                }
                if (count > 0)
                {
                    count--;
                }
                if (DateTime.Now.Hour == 1 && hour != DateTime.Now.Hour)
                {//每天重连一次
                    ReConnet();
                }
                hour = DateTime.Now.Hour;
                Thread.Sleep(1000);
            }
        }

    }
}
