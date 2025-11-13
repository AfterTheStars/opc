using System;

namespace Opcua.Model
{
    public class OpcuaNode
    {
        /// <summary>
        /// 主键
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// 节点路径(逐级拼接)
        /// </summary>
        public string NodePath { get; set; }
        /// <summary>
        /// 父节点路径(逐级拼接)
        /// </summary>
        public string ParentPath { get; set; }
        /// <summary>
        /// 节点编号 (在我的业务系统中的节点编号并不完全唯一,但是所有测点Id都是不同的)
        /// </summary>
        public string NodeId { get; set; }
        /// <summary>
        /// 节点名称(展示名称)
        /// </summary>
        public string NodeName { get; set; }
        /// <summary>
        /// 是否端点(最底端子节点)
        /// </summary>
        public bool IsTerminal { get; set; }
        /// <summary>
        /// 节点类型
        /// </summary>
        public NodeType NodeType { get; set; } 
        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType { get; set; }
    
   
        /// <summary>
        /// 数组长度<=1表示标量，>1时表示数组
        /// </summary>

        public int Length { get; set; } = 1;
  
 
        /// <summary>
        /// 1=只读
        /// </summary>
        public int Readonly { get; set; }
       

    }
    public enum NodeType
    {
        /// <summary>
        /// 根节点
        /// </summary>
        Scada = 1,
        /// <summary>
        /// 目录
        /// </summary>
        Channel = 2,
        /// <summary>
        /// 目录
        /// </summary>
        Device = 3,
        /// <summary>
        /// 测点
        /// </summary>
        Measure = 4
    }

    public enum DataType
    {
        /// <summary>
        /// 字符串
        /// </summary>
        String = 1,
        /// <summary>
        /// 字
        /// </summary>
        UInt16 = 2,
        /// <summary>
        /// 双字
        /// </summary>
        UInt32 = 3,
        /// <summary>
        /// 双字
        /// </summary>
        UInt64 = 5,
        /// <summary>
        /// 16位整数
        /// </summary>
        Int16 = 6,
        /// <summary>
        /// 32位整数
        /// </summary>
        Int32 = 7,
        /// <summary>
        /// 64位整数
        /// </summary>
        Int64 = 8,
        /// <summary>
        /// 浮点数32位
        /// </summary>
        Float = 9,
        /// <summary>
        /// 双精度64位
        /// </summary>
        Double = 10,
        /// <summary>
        /// 日期
        /// </summary>
        Date = 11,
        /// <summary>
        /// 布尔
        /// </summary>
        Bool = 12,
        /// <summary>
        /// 字节
        /// </summary>
        Byte =13,
    }
}
