using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OPC.Services
{
    /// <summary>
    /// OPC UA 服务器管理器 - 完全修复版本
    /// 兼容 OPC UA 1.5.377.21
    /// </summary>
    public class OpcServerManager
    {
        private StandardServer _server;
        private ApplicationConfiguration _configuration;
        private SimpleNodeManager _nodeManager;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning = false;

        public OpcServerManager()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 启动 OPC UA 服务器
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                Console.WriteLine("正在初始化 OPC UA 服务器...");

                // 创建配置
                _configuration = CreateApplicationConfiguration();

                // 验证配置
                await _configuration.Validate(ApplicationType.Server);

                // 创建服务器
                _server = new StandardServer();

                // 将 StandardServer 转换为 IServerInternal
                IServerInternal serverInternal = _server as IServerInternal;
                if (serverInternal == null)
                {
                    throw new Exception("无法将 StandardServer 转换为 IServerInternal");
                }

                // 创建节点管理器
                _nodeManager = new SimpleNodeManager(serverInternal, _configuration);

                // 启动服务器 - Start 是同步的，不是异步的
                _server.Start(_configuration);

                _isRunning = true;

                Console.WriteLine("");
                Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  ✅ OPC UA 服务器启动成功！                                      ║");
                Console.WriteLine("║                                                                ║");
                Console.WriteLine("║  📡 OPC UA 地址:  opc.tcp://localhost:4840                    ║");
                Console.WriteLine("║                                                                ║");
                Console.WriteLine("║  🔗 可以使用以下工具连接：                                      ║");
                Console.WriteLine("║     • UaExpert (OPC Foundation 官方工具)                       ║");
                Console.WriteLine("║     • Kepware KEPServerEX                                     ║");
                Console.WriteLine("║     • 任何标准 OPC UA 客户端                                   ║");
                Console.WriteLine("║                                                                ║");
                Console.WriteLine("║  📊 点位数据：12 个                                             ║");
                Console.WriteLine("║     分类: 温度, 压力, 流量, 状态                               ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                Console.WriteLine("");

                // 启动数据模拟
                _ = SimulateDataAsync();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 启动失败: {ex.Message}");
                Console.WriteLine($"   详情: {ex.InnerException?.Message}");
                Console.WriteLine($"   堆栈: {ex.StackTrace}");
                _isRunning = false;
                throw;
            }
        }

        /// <summary>
        /// 停止 OPC UA 服务器
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                _cancellationTokenSource.Cancel();

                if (_server != null && _isRunning)
                {
                    // Stop 是同步的
                    _server.Stop();
                    _isRunning = false;
                    Console.WriteLine("✅ OPC UA 服务器已停止");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 停止失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建应用配置 - 完全兼容 1.5.377.21
        /// </summary>
        private ApplicationConfiguration CreateApplicationConfiguration()
        {
            // 只使用在 1.5 中存在的配置属性
            ApplicationConfiguration configuration = new ApplicationConfiguration
            {
                ApplicationName = "OPC UA 数据服务器",
                ApplicationType = ApplicationType.Server,
                ApplicationUri = "urn:localhost:OpcServer",
                ProductUri = "http://example.com/OpcServer",

                ServerConfiguration = new ServerConfiguration
                {
                    BaseAddresses = new StringCollection { "opc.tcp://0.0.0.0:4840" },
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
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.Directory,
                        StorePath = "OPC.Certificates"
                    },
                    // 不使用 TrustedRootCertificates 和其他 1.5 中不存在的属性
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
            };

            return configuration;
        }

        /// <summary>
        /// 模拟数据生成
        /// </summary>
        private async Task SimulateDataAsync()
        {
            try
            {
                var random = new Random();
                int cycle = 0;

                while (!_cancellationTokenSource.Token.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        await Task.Delay(5000, _cancellationTokenSource.Token);

                        cycle++;

                        // 更新节点值
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
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📊 数据已更新");
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[警告] 更新数据失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 数据模拟异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取服务器状态
        /// </summary>
        public bool IsRunning => _isRunning;

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
                    _server.Stop();
                    _server.Dispose();
                }
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[警告] 释放资源失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 简单的节点管理器 - 创建并管理 OPC UA 节点
    /// 兼容 OPC UA 1.5.377.21
    /// </summary>
    public class SimpleNodeManager : INodeIdFactory
    {
        private IServerInternal _server;
        private ApplicationConfiguration _configuration;
        private ushort _namespaceIndex;
        private Dictionary<string, BaseVariableState> _variables;

        public SimpleNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        {
            _server = server;
            _configuration = configuration;
            _variables = new Dictionary<string, BaseVariableState>();

            try
            {
                // 获取命名空间索引
                _namespaceIndex = _server.NamespaceUris.GetIndexOrAppend("http://opcserver.example.com");

                // 创建地址空间
                CreateAddressSpace();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化节点管理器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建地址空间
        /// </summary>
        private void CreateAddressSpace()
        {
            try
            {
                // 创建根文件夹
                var rootFolder = new FolderState(null)
                {
                    SymbolicName = "DataPoints",
                    NodeId = new NodeId("DataPoints", _namespaceIndex),
                    BrowseName = new QualifiedName("数据点位", _namespaceIndex),
                    DisplayName = new LocalizedText("zh-CN", "数据点位"),
                    TypeDefinitionId = ObjectTypeIds.FolderType,
                };

                // 创建分类和变量
                CreateCategory(rootFolder, "温度", new[] { "T01", "T02", "T03" });
                CreateCategory(rootFolder, "压力", new[] { "P01", "P02" });
                CreateCategory(rootFolder, "流量", new[] { "F01", "F02" });
                CreateCategory(rootFolder, "状态", new[] { "S01", "S02" });

                Console.WriteLine("✓ 地址空间创建成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 创建地址空间失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建分类和变量
        /// </summary>
        private void CreateCategory(FolderState parent, string categoryName, string[] variables)
        {
            try
            {
                var folder = new FolderState(parent)
                {
                    SymbolicName = categoryName,
                    NodeId = new NodeId($"{categoryName}", _namespaceIndex),
                    BrowseName = new QualifiedName(categoryName, _namespaceIndex),
                    DisplayName = new LocalizedText("zh-CN", categoryName),
                    TypeDefinitionId = ObjectTypeIds.FolderType,
                };

                parent.AddChild(folder);

                foreach (var varName in variables)
                {
                    var variable = new BaseDataVariableState(folder)
                    {
                        SymbolicName = varName,
                        NodeId = new NodeId($"{categoryName}.{varName}", _namespaceIndex),
                        BrowseName = new QualifiedName(varName, _namespaceIndex),
                        DisplayName = new LocalizedText("zh-CN", varName),
                        TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                        DataType = DataTypeIds.Double,
                        ValueRank = ValueRanks.Scalar,
                        AccessLevel = AccessLevels.CurrentReadOrWrite,
                        UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                        Value = 0.0,
                    };

                    folder.AddChild(variable);
                    _variables[$"{categoryName}.{varName}"] = variable;
                }

                Console.WriteLine($"  ✓ {categoryName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 创建 {categoryName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新节点值
        /// </summary>
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
                Console.WriteLine($"[警告] 更新节点值失败: {ex.Message}");
            }
        }

        public NodeId New(ISystemContext context, NodeState node)
        {
            return node.NodeId;
        }
    }
}