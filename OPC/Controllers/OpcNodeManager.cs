using System;
using System.Collections.Generic;

namespace OPC.Services
{
    /// <summary>
    /// 简化的 OPC 节点管理器 - 不依赖 CustomNodeManager2
    /// 直接管理点位数据
    /// </summary>
    public class OpcNodeManager
    {
        private Dictionary<string, DataPoint> _dataPoints;

        public OpcNodeManager()
        {
            _dataPoints = new Dictionary<string, DataPoint>();
            InitializeDataPoints();
        }

        /// <summary>
        /// 初始化所有点位数据
        /// </summary>
        private void InitializeDataPoints()
        {
            try
            {
                // 温度点位
                _dataPoints["温度.T01"] = new DataPoint { Name = "温度传感器01", CurrentValue = 25.5f, DataType = "float" };
                _dataPoints["温度.T02"] = new DataPoint { Name = "温度传感器02", CurrentValue = 26.3f, DataType = "float" };
                _dataPoints["温度.T03"] = new DataPoint { Name = "温度传感器03", CurrentValue = 24.8f, DataType = "float" };

                // 压力点位
                _dataPoints["压力.P01"] = new DataPoint { Name = "压力传感器01", CurrentValue = 101.3f, DataType = "float" };
                _dataPoints["压力.P02"] = new DataPoint { Name = "压力传感器02", CurrentValue = 102.5f, DataType = "float" };

                // 流量点位
                _dataPoints["流量.F01"] = new DataPoint { Name = "流量计01", CurrentValue = 150.0f, DataType = "float" };
                _dataPoints["流量.F02"] = new DataPoint { Name = "流量计02", CurrentValue = 200.0f, DataType = "float" };

                // 状态点位
                _dataPoints["状态.S01"] = new DataPoint { Name = "泵01运行状态", CurrentValue = true, DataType = "bool" };
                _dataPoints["状态.S02"] = new DataPoint { Name = "泵02运行状态", CurrentValue = false, DataType = "bool" };

                Console.WriteLine($"✓ 已初始化 {_dataPoints.Count} 个点位");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 初始化点位失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新点位数据
        /// </summary>
        public void UpdateDataPoint(string pointId, object newValue)
        {
            try
            {
                if (_dataPoints.TryGetValue(pointId, out var dataPoint))
                {
                    dataPoint.CurrentValue = newValue;
                    dataPoint.Timestamp = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 更新点位失败 {pointId}: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有点位数据
        /// </summary>
        public Dictionary<string, object> GetAllDataPoints()
        {
            try
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                foreach (var kvp in _dataPoints)
                {
                    result[kvp.Key] = new
                    {
                        name = kvp.Value.Name,
                        value = kvp.Value.CurrentValue,
                        type = kvp.Value.DataType,
                        timestamp = kvp.Value.Timestamp
                    };
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 获取点位数据失败: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// 按分类获取点位数据
        /// </summary>
        public Dictionary<string, object> GetDataPointsByCategory(string category)
        {
            try
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                foreach (var kvp in _dataPoints)
                {
                    if (kvp.Key.StartsWith(category))
                    {
                        result[kvp.Key] = new
                        {
                            name = kvp.Value.Name,
                            value = kvp.Value.CurrentValue,
                            type = kvp.Value.DataType,
                            timestamp = kvp.Value.Timestamp
                        };
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 获取分类数据失败: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// 获取指定点位的值
        /// </summary>
        public object GetDataPointValue(string pointId)
        {
            try
            {
                if (_dataPoints.TryGetValue(pointId, out var dataPoint))
                {
                    return dataPoint.CurrentValue;
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 获取点位值失败 {pointId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有分类
        /// </summary>
        public List<string> GetCategories()
        {
            List<string> categories = new List<string>();
            foreach (var key in _dataPoints.Keys)
            {
                string category = key.Split('.')[0];
                if (!categories.Contains(category))
                {
                    categories.Add(category);
                }
            }
            return categories;
        }

        /// <summary>
        /// 获取点位总数
        /// </summary>
        public int GetDataPointCount()
        {
            return _dataPoints.Count;
        }
    }

    /// <summary>
    /// 数据点位类
    /// </summary>
    public class DataPoint
    {
        public string Name { get; set; }
        public object CurrentValue { get; set; }
        public string DataType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}