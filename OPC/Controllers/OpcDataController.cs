using Microsoft.AspNetCore.Mvc;
using OPC.Services;
using System.Collections.Generic;

namespace OPC.Controllers
{
    /// <summary>
    /// OPC UA 数据接口控制器
    /// 提供 REST API 来访问 OPC UA 服务器中的数据
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OpcDataController : ControllerBase
    {
        private readonly OpcUaServer _opcServer;
        private readonly ILogger<OpcDataController> _logger;

        public OpcDataController(OpcUaServer opcServer, ILogger<OpcDataController> logger)
        {
            _opcServer = opcServer ?? throw new ArgumentNullException(nameof(opcServer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 获取所有 OPC 数据点
        /// </summary>
        /// <remarks>
        /// 返回 OPC UA 服务器中所有数据点的当前值
        /// 
        /// 示例响应:
        /// ```
        /// {
        ///   "temperature.T01": {
        ///     "displayName": "温度传感器01",
        ///     "value": 25.5,
        ///     "timestamp": "2025-01-15T10:30:45Z",
        ///     "dataType": "ns=0;i=11"
        ///   },
        ///   ...
        /// }
        /// ```
        /// </remarks>
        [HttpGet("all")]
        [Produces("application/json")]
        public ActionResult<Dictionary<string, object>> GetAllData()
        {
            try
            {
                _logger.LogInformation("获取所有 OPC 数据点");

                if (!_opcServer.IsRunning)
                {
                    return StatusCode(503, new { error = "OPC UA 服务器未运行", status = "unavailable" });
                }

                var data = _opcServer.GetAllNodeData();
                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    serverStatus = _opcServer.IsRunning ? "running" : "stopped",
                    dataPoints = data,
                    count = data.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取 OPC 数据时出错");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// 按分类获取 OPC 数据
        /// </summary>
        /// <param name="category">分类名称 (温度/压力/流量/状态)</param>
        /// <remarks>
        /// 示例: GET /api/opcdata/category/温度
        /// </remarks>
        [HttpGet("category/{category}")]
        [Produces("application/json")]
        public ActionResult<Dictionary<string, object>> GetDataByCategory(string category)
        {
            try
            {
                _logger.LogInformation($"获取分类 {category} 的 OPC 数据");

                if (!_opcServer.IsRunning)
                {
                    return StatusCode(503, new { error = "OPC UA 服务器未运行", status = "unavailable" });
                }

                // 验证分类
                var validCategories = new[] { "温度", "压力", "流量", "状态" };
                if (!validCategories.Contains(category))
                {
                    return BadRequest(new
                    {
                        error = "无效的分类名称",
                        validCategories = validCategories
                    });
                }

                var data = _opcServer.GetNodeDataByCategory(category);

                if (data.Count == 0)
                {
                    return NotFound(new { error = $"分类 '{category}' 中没有数据点" });
                }

                return Ok(new
                {
                    success = true,
                    category = category,
                    timestamp = DateTime.UtcNow,
                    dataPoints = data,
                    count = data.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取分类 {category} 的数据时出错");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// 获取服务器状态信息
        /// </summary>
        [HttpGet("status")]
        [Produces("application/json")]
        public ActionResult<object> GetStatus()
        {
            try
            {
                _logger.LogInformation("获取服务器状态");

                return Ok(new
                {
                    serverRunning = _opcServer.IsRunning,
                    status = _opcServer.IsRunning ? "running" : "stopped",
                    endpoint = "opc.tcp://localhost:4840",
                    applicationUri = "urn:localhost:OpcUaServer",
                    timestamp = DateTime.UtcNow,
                    message = _opcServer.IsRunning
                        ? "OPC UA 服务器正在运行"
                        : "OPC UA 服务器已停止"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取服务器状态时出错");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// 获取服务器配置信息
        /// </summary>
        [HttpGet("info")]
        [Produces("application/json")]
        public ActionResult<object> GetInfo()
        {
            try
            {
                _logger.LogInformation("获取服务器配置信息");

                return Ok(new
                {
                    applicationName = "OPC UA 数据服务器",
                    applicationUri = "urn:localhost:OpcUaServer",
                    productUri = "https://example.com/OpcUaServer",
                    endpoint = "opc.tcp://0.0.0.0:4840",
                    dataPoints = new
                    {
                        temperature = new[] { "T01", "T02", "T03" },
                        pressure = new[] { "P01", "P02" },
                        flow = new[] { "F01", "F02" },
                        status = new[] { "S01", "S02" }
                    },
                    totalDataPoints = 12,
                    restApiVersion = "1.0",
                    opcUaVersion = "1.5.377.21",
                    dotnetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取服务器配置信息时出错");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}