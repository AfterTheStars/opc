using Opcua.Service;
using System;
using Newtonsoft.Json;

using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Opcua.Common;

namespace Opcua.App
{
    class Program
    {
        private string[] Addresses = { "opc.tcp://localhost:8120" };//, "https://localhost:8121/"
        static void Main(string[] args)
        {
            var param = args.Select(p => Convert.ToInt32(p)).ToArray();
            Console.WriteLine(param.ToJson());
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            runxml();
            var sPort = 11033;
            var count = 1;
            if (param.Count() > 1)
            {
                sPort = param[0];
                count = param[1];
            }
            //new DiscoveryManagement().StartDiscovery();
            for (int i = 0; i < count; i++)
            {

                var add = $"opc.tcp://localhost:{sPort + i}";
                Task.Run(() => 
                {

                    OpcuaManagement server = new OpcuaManagement(new string[] { add });
                    server.CreateServerInstance();
                });

            }
            Console.WriteLine("退出:exit");
            while (true)
            {
                if (Console.ReadLine() == "exit")
                {
                    break;
                }
            }
            
           
        }
        static void runxml()
        {
            try
            {
                string bat= @"E:\SC\内部资料\OPC服务端\Opcua.Server\Opcua.App\copyxml.bat";
                if (System.IO.File.Exists(bat))
                {
                    var process = new Process();
                    process.StartInfo.FileName = bat;
                    process.Start(); //启动选择的exe文件
                }
             
            }
            catch (Exception e)
            { 
                
            }
        }

 
   


    }
}
