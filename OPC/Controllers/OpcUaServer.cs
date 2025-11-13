using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace OPC.Services
{
    /// <summary>
    /// OPC UA 服务器实现 - 完全兼容 OPC UA 1.5.377.21
    /// ✅ 已修复：使用ApplicationInstance和CheckApplicationInstanceCertificate正确加载证书
    /// </summary>
    public class OpcUaServer : IDisposable
    {
        private StandardServer _server;
        private ApplicationInstance _applicationInstance;
        private ApplicationConfiguration _configuration;
        private OpcNodeManager _nodeManager;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning = false;
        private readonly object _lockObject = new object();

        public OpcUaServer()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 启动 OPC UA 服务器
        /// </summary>
        public async Task StartAsync()
        {
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    LogInfo("OPC UA 服务器已在运行中");
                    return;
                }
            }

            try
            {
                LogInfo("════════════════════════════════════════");
                LogInfo("正在初始化 OPC UA 服务器...");
                LogInfo("════════════════════════════════════════");

                // 1. 确保证书目录存在
                LogInfo("[1/7] 创建证书目录...");
                EnsureDirectoriesExist("OPC.Certificates");
                LogSuccess("证书目录已准备");

                // 2. 创建应用配置
                LogInfo("[2/7] 创建应用配置...");
                _configuration = CreateApplicationConfiguration();
                LogSuccess("应用配置已创建");

                // 3. 创建 ApplicationInstance
                LogInfo("[3/7] 创建应用实例...");
                _applicationInstance = new ApplicationInstance
                {
                    ApplicationName = "OPC UA 数据服务器",
                    ApplicationType = ApplicationType.Server,
                    ApplicationConfiguration = _configuration
                };
                LogSuccess("应用实例已创建");

                // 4. 检查和创建应用实例证书
                LogInfo("[4/7] 检查并创建应用实例证书...");
                bool certOk = await _applicationInstance.CheckApplicationInstanceCertificates(false, 0);
                if (!certOk)
                {
                    throw new InvalidOperationException("应用实例证书验证失败");
                }
                LogSuccess("应用实例证书已验证");

                // 5. 验证配置
                LogInfo("[5/7] 验证应用配置...");
                await _configuration.Validate(ApplicationType.Server);
                LogSuccess("配置验证完成");

                // 6. 创建服务器实例
                LogInfo("[6/7] 创建 OPC UA 服务器...");
                _server = new StandardServer();
                LogSuccess("服务器实例已创建");

                // 7. 启动服务器
                LogInfo("[7/7] 启动 OPC UA 服务器...");
                await _applicationInstance.Start(_server);
                LogSuccess("OPC UA 服务器已启动");

                // 8. 初始化节点管理器
                LogInfo("初始化节点管理器...");
                var serverInternal = _server as IServerInternal;
                if (serverInternal == null)
                    throw new InvalidOperationException("无法获取 IServerInternal 接口");

                _nodeManager = new OpcNodeManager(serverInternal, _configuration);
                LogSuccess("节点管理器已初始化");

                lock (_lockObject)
                {
                    _isRunning = true;
                }

                // 启动数据模拟
                _ = SimulateDataAsync();

                PrintStartupBanner();
            }
            catch (Exception ex)
            {
                LogError($"启动失败: {ex.Message}", ex);
                await StopAsync();
                throw;
            }
        }

        /// <summary>
        /// 停止 OPC UA 服务器
        /// </summary>
        public async Task StopAsync()
        {
            lock (_lockObject)
            {
                if (!_isRunning)
                {
                    return;
                }
            }

            try
            {
                LogInfo("正在停止 OPC UA 服务器...");

                _cancellationTokenSource?.Cancel();

                if (_server != null)
                {
                    _server.Stop();
                    LogSuccess("OPC UA 服务器已停止");
                }

                lock (_lockObject)
                {
                    _isRunning = false;
                }
            }
            catch (Exception ex)
            {
                LogError($"停止时出错: {ex.Message}", ex);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 获取所有节点数据
        /// </summary>
        public Dictionary<string, object> GetAllNodeData()
        {
            return _nodeManager?.GetAllNodeData() ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// 按分类获取节点数据
        /// </summary>
        public Dictionary<string, object> GetNodeDataByCategory(string category)
        {
            return _nodeManager?.GetNodeDataByCategory(category) ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// 获取服务器运行状态
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_lockObject)
                {
                    return _isRunning;
                }
            }
        }

        /// <summary>
        /// 确保所有必需的目录存在
        /// </summary>
        private void EnsureDirectoriesExist(string basePath)
        {
            try
            {
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                    LogInfo($"  ✓ 创建基础目录");
                }

                string[] subdirs = { "trusted", "issuers", "rejected" };
                foreach (var subdir in subdirs)
                {
                    string fullPath = Path.Combine(basePath, subdir);
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                        LogInfo($"  ✓ 创建子目录: {subdir}");
                    }
                }

                LogSuccess($"所有证书目录已准备");
            }
            catch (Exception ex)
            {
                LogError($"创建目录失败: {basePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// 创建应用配置
        /// ✅ 关键：与示例中相同的配置方式
        /// </summary>
        private ApplicationConfiguration CreateApplicationConfiguration()
        {
            const string certificateBasePath = "OPC.Certificates";

            var config = new ApplicationConfiguration
            {
                ApplicationName = "OPC UA 数据服务器",
                ApplicationType = ApplicationType.Server,
                ApplicationUri = $"urn:{System.Net.Dns.GetHostName()}:OpcUaServer",
                ProductUri = "http://opcfoundation.org/Quickstart/ReferenceServer/v1.04",

                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection { "opc.tcp://0.0.0.0:4840" },
                    MinRequestThreadCount = 5,
                    MaxRequestThreadCount = 100,
                    MaxQueuedRequestCount = 200,
                    DiagnosticsEnabled = true,
                    MaxSessionCount = 100,
                    MinSessionTimeout = 10000,
                    MaxSessionTimeout = 3600000,
                    MaxBrowseContinuationPoints = 100,
                    MaxQueryContinuationPoints = 100,
                    MaxHistoryContinuationPoints = 100,
                },

                SecurityConfiguration = new SecurityConfiguration
                {
                    // ✅ 关键：使用与示例相同的方式配置证书标识符
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.GetFullPath(certificateBasePath),
                        // SubjectName格式必须正确
                        SubjectName = Utils.Format("CN={0}, DC={1}", "OpcUaServer", System.Net.Dns.GetHostName())
                    },

                    TrustedIssuerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.GetFullPath(Path.Combine(certificateBasePath, "issuers"))
                    },

                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.GetFullPath(Path.Combine(certificateBasePath, "trusted"))
                    },

                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = Path.GetFullPath(Path.Combine(certificateBasePath, "rejected"))
                    },

                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true,
                    SendCertificateChain = true,
                },

                TransportQuotas = new TransportQuotas
                {
                    OperationTimeout = 600000,
                    MaxStringLength = int.MaxValue,
                    MaxByteStringLength = int.MaxValue,
                    MaxArrayLength = int.MaxValue,
                    MaxMessageSize = 4 * 1024 * 1024,
                    ChannelLifetime = 3600000,
                    SecurityTokenLifetime = 3600000,
                },

                ClientConfiguration = new ClientConfiguration
                {
                    DefaultSessionTimeout = 60000,
                },

                TraceConfiguration = new TraceConfiguration()
            };

            return config;
        }

        /// <summary>
        /// 数据模拟循环
        /// </summary>
        private async Task SimulateDataAsync()
        {
            try
            {
                var random = new Random();
                int cycle = 0;

                while (!_cancellationTokenSource.Token.IsCancellationRequested && IsRunning)
                {
                    try
                    {
                        await Task.Delay(5000, _cancellationTokenSource.Token);
                        cycle++;

                        if (_nodeManager != null)
                        {
                            _nodeManager.UpdateNodeValue("T01", Math.Round(25.5 + random.NextDouble() * 2, 2));
                            _nodeManager.UpdateNodeValue("T02", Math.Round(26.3 + random.NextDouble() * 2, 2));
                            _nodeManager.UpdateNodeValue("T03", Math.Round(24.8 + random.NextDouble() * 2, 2));

                            _nodeManager.UpdateNodeValue("P01", Math.Round(101.3 + random.NextDouble() * 1, 2));
                            _nodeManager.UpdateNodeValue("P02", Math.Round(102.5 + random.NextDouble() * 1, 2));

                            _nodeManager.UpdateNodeValue("F01", Math.Round(150.0 + random.NextDouble() * 50, 2));
                            _nodeManager.UpdateNodeValue("F02", Math.Round(200.0 + random.NextDouble() * 50, 2));

                            _nodeManager.UpdateNodeValue("S01", cycle % 2 == 0);
                            _nodeManager.UpdateNodeValue("S02", cycle % 3 == 0);
                        }

                        if (cycle % 6 == 0)
                        {
                            LogInfo($"✅ 数据更新完成 (周期 #{cycle})");
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogError($"数据更新失败", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("数据模拟异常", ex);
            }
        }

        /// <summary>
        /// 打印启动信息横幅
        /// </summary>
        private void PrintStartupBanner()
        {
            Console.WriteLine("");
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ✅ OPC UA 服务器启动成功！                                      ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║  📡 OPC UA 地址:  opc.tcp://localhost:4840                    ║");
            Console.WriteLine("║  🌐 REST API:     http://localhost:5001/api/opcdata           ║");
            Console.WriteLine("║  📊 Swagger:      http://localhost:5001/swagger               ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║  📂 证书目录:     ./OPC.Certificates/                         ║");
            Console.WriteLine("║     ├── OpcUaServer.cer   (应用证书)                           ║");
            Console.WriteLine("║     ├── OpcUaServer.pfx   (私钥)                              ║");
            Console.WriteLine("║     ├── trusted/          (受信任的根证书)                     ║");
            Console.WriteLine("║     ├── issuers/          (受信任的签发者)                     ║");
            Console.WriteLine("║     └── rejected/         (拒绝的证书)                        ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║  📊 数据点位：12 个                                             ║");
            Console.WriteLine("║     - 温度: T01, T02, T03                                      ║");
            Console.WriteLine("║     - 压力: P01, P02                                           ║");
            Console.WriteLine("║     - 流量: F01, F02                                           ║");
            Console.WriteLine("║     - 状态: S01, S02                                           ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine("");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                if (_server != null)
                {
                    try
                    {
                        _server.Stop();
                    }
                    catch { }

                    _server?.Dispose();
                }

                _cancellationTokenSource?.Dispose();
                LogInfo("资源已释放");
            }
            catch (Exception ex)
            {
                LogError("释放资源时出错", ex);
            }
        }

        #region 日志辅助方法

        private void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[信息] {DateTime.Now:HH:mm:ss} {message}");
            Console.ResetColor();
        }

        private void LogSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[成功] {DateTime.Now:HH:mm:ss} {message}");
            Console.ResetColor();
        }

        private void LogError(string message, Exception ex = null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[错误] {DateTime.Now:HH:mm:ss} {message}");
            if (ex != null)
            {
                Console.WriteLine($"       异常: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"       内部异常: {ex.InnerException.Message}");
            }
            Console.ResetColor();
        }

        #endregion
    }

    /// <summary>
    /// OPC 节点管理器
    /// </summary>
    public class OpcNodeManager : INodeIdFactory
    {
        private readonly IServerInternal _server;
        private readonly ApplicationConfiguration _configuration;
        private readonly ushort _namespaceIndex;
        private readonly Dictionary<string, BaseDataVariableState> _variables;

        public OpcNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _variables = new Dictionary<string, BaseDataVariableState>();

            try
            {
                _namespaceIndex = _server.NamespaceUris.GetIndexOrAppend("http://opcserver.example.com");
                CreateAddressSpace();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 节点管理器初始化失败: {ex.Message}");
                throw;
            }
        }

        private void CreateAddressSpace()
        {
            try
            {
                var rootFolder = new FolderState(null)
                {
                    SymbolicName = "RootDataPoints",
                    NodeId = new NodeId("DataPoints", _namespaceIndex),
                    BrowseName = new QualifiedName("数据点位", _namespaceIndex),
                    DisplayName = new LocalizedText("zh-CN", "数据点位"),
                    TypeDefinitionId = ObjectTypeIds.FolderType,
                };

                CreateCategory(rootFolder, "温度", new[] { ("T01", "温度传感器01"), ("T02", "温度传感器02"), ("T03", "温度传感器03") });
                CreateCategory(rootFolder, "压力", new[] { ("P01", "压力传感器01"), ("P02", "压力传感器02") });
                CreateCategory(rootFolder, "流量", new[] { ("F01", "流量计01"), ("F02", "流量计02") });
                CreateCategory(rootFolder, "状态", new[] { ("S01", "泵01运行状态"), ("S02", "泵02运行状态") });

                Console.WriteLine("[成功] 地址空间创建完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 创建地址空间失败: {ex.Message}");
                throw;
            }
        }

        private void CreateCategory(FolderState parent, string categoryName, (string id, string name)[] variables)
        {
            try
            {
                var folder = new FolderState(parent)
                {
                    SymbolicName = categoryName,
                    NodeId = new NodeId(categoryName, _namespaceIndex),
                    BrowseName = new QualifiedName(categoryName, _namespaceIndex),
                    DisplayName = new LocalizedText("zh-CN", categoryName),
                    TypeDefinitionId = ObjectTypeIds.FolderType,
                };

                parent.AddChild(folder);

                foreach (var (varId, varName) in variables)
                {
                    var variable = new BaseDataVariableState(folder)
                    {
                        SymbolicName = varId,
                        NodeId = new NodeId($"{categoryName}.{varId}", _namespaceIndex),
                        BrowseName = new QualifiedName(varId, _namespaceIndex),
                        DisplayName = new LocalizedText("zh-CN", varName),
                        TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                        DataType = DataTypeIds.Double,
                        ValueRank = ValueRanks.Scalar,
                        AccessLevel = AccessLevels.CurrentReadOrWrite,
                        UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                        Value = 0.0,
                        Timestamp = DateTime.UtcNow,
                    };

                    folder.AddChild(variable);
                    _variables[$"{categoryName}.{varId}"] = variable;
                }

                Console.WriteLine($"  ✓ {categoryName} ({variables.Length} 个节点)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 创建 {categoryName} 失败: {ex.Message}");
                throw;
            }
        }

        public void UpdateNodeValue(string nodeId, object value)
        {
            try
            {
                foreach (var kvp in _variables)
                {
                    if (kvp.Key.EndsWith(nodeId))
                    {
                        kvp.Value.Value = value;
                        kvp.Value.Timestamp = DateTime.UtcNow;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[警告] 更新节点值失败 {nodeId}: {ex.Message}");
            }
        }

        public Dictionary<string, object> GetAllNodeData()
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in _variables)
            {
                result[kvp.Key] = new
                {
                    displayName = kvp.Value.DisplayName?.Text,
                    value = kvp.Value.Value,
                    timestamp = kvp.Value.Timestamp,
                    dataType = kvp.Value.DataType?.ToString(),
                };
            }
            return result;
        }

        public Dictionary<string, object> GetNodeDataByCategory(string category)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in _variables)
            {
                if (kvp.Key.StartsWith(category))
                {
                    result[kvp.Key] = new
                    {
                        displayName = kvp.Value.DisplayName?.Text,
                        value = kvp.Value.Value,
                        timestamp = kvp.Value.Timestamp,
                    };
                }
            }
            return result;
        }

        public NodeId New(ISystemContext context, NodeState node)
        {
            return node.NodeId;
        }
    }
}