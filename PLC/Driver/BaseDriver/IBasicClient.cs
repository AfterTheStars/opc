using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PLC.BaseDriver
{
    public interface IBasicClient
    {
		bool Conneted { get; }

		Socket socket { get; }

		Task<bool> ConnectAsync(string rIP, int rPort, int timeOut = 5000);


		Task<bool> ReConnectAsync();


		Task<bool> PingCheckAsync(string ip, int timeOut,int port=0);
		

		void Close();

		byte[] SendData(byte[] sd, byte[] rd = null);


		Task<byte[]> SendDataAsync(byte[] SecData, byte[] RecData = null);


		byte[] ReceiveData(byte[] rd = null);


		Task<byte[]> ReceiveDataAsync(byte[] rd = null);
	  

	}
}
