using System;
using System.Collections.Generic;
using System.Text;

namespace Opcua.Service
{
    public class NodeModel
    {
        /// <summary>
        /// PLC连接器
        /// </summary>
        public List<NodeDriver> drivers { get; set; }
    }
    public class NodeDriver
    { 
        /// <summary>
        /// PLC驱动名称
        /// </summary>
         public string DriverName { get; set; }
         /// <summary>
         /// IP地址
         /// </summary>
         public string IP { get; set; }

        /// <summary>
        /// 标签目录集
        /// </summary>
         public List<NodeFolder> folders { get; set; }
    }
    public class NodeFolder
    { 
        /// <summary>
        /// 目录名
        /// </summary>
        public string FolderName { get; set; }
        /// <summary>
        /// 下级目录
        /// </summary>
        public NodeFolder nodeFolder { get; set; }
        /// <summary>
        /// 标签数据
        /// </summary>
        public List<NodeItem> items { get; set; }   
    }
    public class NodeItem
    { 
        /// <summary>
        /// 标签名
        /// </summary>
        public string TagName { get; set; }

        /// <summary>
        /// 扫描周期ms
        /// </summary>
        public int TagScan { get; set; }
        /// <summary>
        /// 是否只读
        /// </summary>
        public int ReadOnly { get; set; }=1;
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }
        /// <summary>
        /// 长度
        /// </summary>
        public int Len { get; set; } = 1;
        /// <summary>
        /// 数据类型：Bool  Int16 String Int32 UInt16 UInt32 Byte
        /// </summary>
        public string DataType { get; set; } = "UInt16";

         

    }
}
