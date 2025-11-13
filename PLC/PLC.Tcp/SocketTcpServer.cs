using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PLC.Tcp
{
    public class SocketTcpServer
    {
        private byte[] result = new byte[1024 * 1024];
        public static int myProt = 9100;   //端口
        Socket serverSocket;
        public bool IsStart = false;
        public delegate void delegate_ReceiveMessage(object Client, string msg);
        public delegate_ReceiveMessage ReceiveMsg;
        public List<Socket> clients = new List<Socket>();

        Dictionary<string, string> dicSocketMsg = new Dictionary<string, string>();//接收到的每个端口的数据包
        string Poid = string.Empty;
        object _lock = new object();
        
        public SocketTcpServer(int port,string poid ="")
        {
            myProt = port;
            Poid = poid;
            StartServer();
        }
        public SocketTcpServer()
        {
            StartServer();
        }
        /// <summary>
        /// 设置定界符
        /// </summary>
        public void SetPoid(string poid)
        {
            Poid = poid;
        }
        private void StartServer()
        {
            try
            {
                //服务器IP地址
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, myProt));  //绑定IP地址：端口
                serverSocket.Listen(10);    //设定最多10个排队连接请求
                Console.WriteLine("socket启动监听{0}成功", serverSocket.LocalEndPoint.ToString());
                //通过Clientsoket发送数据
                Thread myThread = new Thread(ListenClientConnect);
                myThread.IsBackground = true;
                myThread.Start();
                IsStart = true;
                new Thread(new ThreadStart(CheckLink)) { IsBackground = true }.Start();
            }
            catch
            {
                //throw;
            }
        }


        /// <summary>
        /// 监听客户端连接
        /// </summary>
        private void ListenClientConnect()
        {
            while (true)
            {
                try
                {
                    Socket clientSocket = serverSocket.Accept();
                    Thread receiveThread = new Thread(ReceiveMessage);
                    receiveThread.Start(clientSocket);
                    lock (_lock)
                    {
                        clients.Add(clientSocket);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    break;
                }
            }
        }
        /// <summary>
        /// 接收消息
        /// </summary>
        /// <param name="clientSocket"></param>
        private void ReceiveMessage(object clientSocket)
        {
            Socket myClientSocket = (Socket)clientSocket;
            string id = myClientSocket.RemoteEndPoint.ToString();
            byte[] temp = new byte[1024 * 1024];
            int rec = 0;
            while (true)
            {
                try
                {
                    int receiveNumber = myClientSocket.Receive(temp);
                    if (receiveNumber == 0)
                    {
                        continue;
                    }
                    string msg = Encoding.ASCII.GetString(temp, 0, receiveNumber);

                    if (ReceiveMsg != null)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(Poid))
                            {
                                ReceiveMsg.Invoke(myClientSocket, msg);
                            }
                            else
                            {
                                string newms;
                                if (dicSocketMsg.TryGetValue(id, out string oldmsg))
                                {
                                    newms = oldmsg + msg;
                                }
                                else
                                {
                                    newms = msg;
                                    dicSocketMsg.Add(id, "");
                                }

                                string[] ss = newms.Split(new string[] { Poid }, StringSplitOptions.None);
                                if (ss.Length == 1)
                                {
                                    dicSocketMsg[id] = ss[0];//没有定界符
                                    if (rec++ > 3)
                                    {//连接发送无定界符号的数据
                                        ReceiveMsg.Invoke(myClientSocket, newms + " Receive err data with no poid :" + Poid);
                                        dicSocketMsg[id] = string.Empty;//清除不然内存爆了
                                        //Send(myClientSocket, "Receive err data with no poid :" + Poid);
                                    }
                                }
                                else
                                {
                                    for (int i = 0; i < ss.Length - 1; i++)
                                    {
                                        ReceiveMsg.Invoke(myClientSocket, ss[i]);
                                    }
                                    dicSocketMsg[id] = ss[ss.Length - 1];
                                    rec = 0;
                                }

                            }
                        }
                        catch
                        { }
                    }
                }
                catch
                {
                    ClientClose(myClientSocket);
                    break;
                }
            }
        }
        private void ClientClose(Socket sc)
        {
            try
            {
                lock (_lock)
                {
                    clients.Remove(sc);
                    dicSocketMsg.Remove(sc.RemoteEndPoint.ToString());
                }
                sc.Shutdown(SocketShutdown.Both);
                sc.Close();
            }
            catch
            { }
        }
        public bool Send(object client, string msg)
        {
            try
            {
                Socket sc = (Socket)client;
                lock (client)
                {
                    sc.Send(Encoding.ASCII.GetBytes(msg));
                }
                return true;
            }
            catch
            {//连接断开
                ClientClose((Socket)client);
                return false;
            }
        }
        public bool Close()
        {
            try
            {
                GC.Collect();

                GC.WaitForFullGCComplete();
                serverSocket.Close();
                clients.Clear();
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void CheckLink()
        {//检查连接是否正常
            while (true)
            {
                Thread.Sleep(1000);
                try
                {
                    if (clients.Count > 0)
                    {
                        for (int i = clients.Count - 1; i >= 0; i--)
                        {
                            if (clients[i].Poll(1000, SelectMode.SelectRead))
                            {
                                ClientClose(clients[i]);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }

    }
}
