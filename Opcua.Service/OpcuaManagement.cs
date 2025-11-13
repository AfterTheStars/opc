using System;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using Opc.Ua.Gds.Server;
using System.Collections.Generic;
using System.Xml;

namespace Opcua.Service
{
    public class OpcuaManagement
    {
        private string ServerName = "OpcServer";
        private string[] Addresses = { "opc.tcp://localhost:8120" };//, "https://localhost:8121/"
        public OpcuaManagement()
        {
            
          // SysInit();
        }
        public OpcuaManagement(string[] addresse)
        {
            Addresses = addresse;
        }
        public void CreateServerInstance()
        {
            try
            {
                var config = new ApplicationConfiguration()
                {
                    ApplicationName = ServerName,
                    ApplicationUri = Utils.Format(@"urn:{0}:{1}", System.Net.Dns.GetHostName(), ServerName),
                    ApplicationType = ApplicationType.Server,
                    ServerConfiguration = new ServerConfiguration()
                    {
                        BaseAddresses = Addresses,
                        MinRequestThreadCount = 100,
                        MaxRequestThreadCount = 1000,
                        MaxQueuedRequestCount = 200,
                        RegistrationEndpoint = new EndpointDescription()
                        {
                            EndpointUrl = ServerName,
                            SecurityLevel = ServerSecurityPolicy.CalculateSecurityLevel(MessageSecurityMode.SignAndEncrypt, SecurityPolicies.Basic256Sha256),
                            SecurityMode = MessageSecurityMode.SignAndEncrypt,
                            SecurityPolicyUri = SecurityPolicies.Basic256Sha256,
                            Server = new ApplicationDescription() { ApplicationType = ApplicationType.DiscoveryServer },
                        },
 
                    },
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = Utils.Format(@"CN={0}, DC={1}", ServerName, System.Net.Dns.GetHostName()) },
                        TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                        TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                        RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },

                        AutoAcceptUntrustedCertificates = true,
                        AddAppCertToTrustedStore = true,
                        //RejectSHA1SignedCertificates = false,
                        //RejectUnknownRevocationStatus=false
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                    TraceConfiguration = new TraceConfiguration()

                };

                //安全策略
                var Collection = new List<ServerSecurityPolicy>();
                Collection.Add(new ServerSecurityPolicy() {  SecurityMode= MessageSecurityMode.None, SecurityPolicyUri= SecurityPolicies.None });
                config.ServerConfiguration.SecurityPolicies = new ServerSecurityPolicyCollection(Collection);
 

                config.Validate(ApplicationType.Server).GetAwaiter().GetResult();

                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
                }
                // 限制最大连接数
                config.ServerConfiguration.MaxSessionCount = 100; // 最大允许100个客户端

                // 会话超时设置
                config.ServerConfiguration.MinSessionTimeout = 60000; // 60 秒
                config.ServerConfiguration.MaxSessionTimeout = 3600000; // 1 小时
                config.ServerConfiguration.MaxPublishingInterval = 1000;
                config.ServerConfiguration.MaxNotificationsPerPublish = 1000;
                config.ServerConfiguration.MaxSessionCount = 1000;
                config.ServerConfiguration.MaxSubscriptionCount = 1000;
                config.ServerConfiguration.MaxPublishRequestCount = 1000;
                config.ServerConfiguration.MinRequestThreadCount = 10;


            var application = new ApplicationInstance
                {
                    ApplicationName = ServerName,
                    ApplicationType = ApplicationType.Server,
                    ApplicationConfiguration = config
                };
                //application.CheckApplicationInstanceCertificate(false, 2048).GetAwaiter().GetResult();
                bool certOk = application.CheckApplicationInstanceCertificate(false, 0).Result;
                if (!certOk)
                {
                    Console.WriteLine("证书验证失败!");
                }
                
                var dis =new DiscoveryServerBase();
                // start the server.
                application.Start(new AxiuOpcuaServer()).Wait();
                Console.WriteLine("OPC-UA服务已启动...");
                foreach (var a in config.ServerConfiguration.BaseAddresses)
                {
                    Console.WriteLine(a);
                }
            
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("启动OPC-UA服务端触发异常:" + ex.Message);
                Console.ResetColor();
            }
        }

        private void SysInit()
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load("System.config");    //加载Xml文件  
                XmlNode root = doc.SelectSingleNode("//OpcUa");//当节点Workflow带有属性是，使用
                if (root != null)
                {
                    string name = (root.SelectSingleNode("ServerName")).InnerText;
                    string url = (root.SelectSingleNode("BaseAddresses")).InnerText;
                    if (!string.IsNullOrEmpty(name))
                    {
                        ServerName = name;
                    }
                    if (!string.IsNullOrEmpty(url))
                    {
                        Addresses = url.Split(','); 
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
