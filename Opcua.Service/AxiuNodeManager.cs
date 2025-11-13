using Opcua.Model;
using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.Intrinsics.X86;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;


namespace Opcua.Service
{
    /// <summary>
    /// 以下备注中  测点即代表最叶子级节点
    /// 目前设计是 只有测点有数据  其余节点都是目录
    /// </summary>
    public class AxiuNodeManager : CustomNodeManager2
    {
        /// <summary>
        /// 配置修改次数  主要用来识别菜单树是否有变动  如果发生变动则修改菜单树对应节点  测点的实时数据变化不算在内
        /// </summary>
        private int cfgCount = -1;
        private IList<IReference> _references;
        /// <summary>
        /// 测点集合,实时数据刷新时,直接从字典中取出对应的测点,修改值即可
        /// </summary>
        private Dictionary<string, BaseDataVariableState> _nodeDic = new Dictionary<string, BaseDataVariableState>();
        List<OpcuaNode> OpcuaNodeList = new List<OpcuaNode>();//读取的节点
        
        /// <summary>
        /// 目录集合,修改菜单树时需要(我们需要知道哪些菜单需要修改,哪些需要新增,哪些需要删除)
        /// </summary>
        private Dictionary<string, FolderState> _folderDic = new Dictionary<string, FolderState>();

        public AxiuNodeManager(IServerInternal server, ApplicationConfiguration configuration) : base(server, configuration, "http://opcfoundation.org/Quickstarts/ReferenceApplications")
        {

        }

        /// <summary>
        /// 重写NodeId生成方式(目前采用'_'分隔,如需更改,请修改此方法)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        public override NodeId New(ISystemContext context, NodeState node)
        {
            BaseInstanceState instance = node as BaseInstanceState;

            if (instance != null && instance.Parent != null)
            {
                string id = instance.Parent.NodeId.Identifier as string;

                if (id != null)
                {
                    return new NodeId(id + "." + instance.SymbolicName, instance.Parent.NodeId.NamespaceIndex);
                }
            }

            return node.NodeId;
        }

        /// <summary>
        /// 重写获取节点句柄的方法
        /// </summary>
        /// <param name="context"></param>
        /// <param name="nodeId"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        protected override NodeHandle GetManagerHandle(ServerSystemContext context, NodeId nodeId, IDictionary<NodeId, NodeState> cache)
        {
            lock (Lock)
            {
                // quickly exclude nodes that are not in the namespace. 
                if (!IsNodeIdInNamespace(nodeId))
                {
                    return null;
                }

                NodeState node = null;

                if (!PredefinedNodes.TryGetValue(nodeId, out node))
                {
                    return null;
                }

                NodeHandle handle = new NodeHandle();

                handle.NodeId = nodeId;
                handle.Node = node;
                handle.Validated = true;

                return handle;
            }
        }

        /// <summary>
        /// 重写节点的验证方式
        /// </summary>
        /// <param name="context"></param>
        /// <param name="handle"></param>
        /// <param name="cache"></param>
        /// <returns></returns>
        protected override NodeState ValidateNode(ServerSystemContext context, NodeHandle handle, IDictionary<NodeId, NodeState> cache)
        {
            // not valid if no root.
            if (handle == null)
            {
                return null;
            }

            // check if previously validated.
            if (handle.Validated)
            {
                return handle.Node;
            }
            // TBD
            return null;
        }

        /// <summary>
        /// 重写创建基础目录
        /// </summary>
        /// <param name="externalReferences"></param>
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out _references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = _references = new List<IReference>();
                }

                try
                {
                    //TODO: 获取节点树
                    //OpcuaNodeList = ReadXmlNodes();//从外部xml文件读
                    OpcuaNodeList = ReadXmlNodes();//从外部xml文件读
                    //开始创建节点的菜单树
                    GeneraterNodes(OpcuaNodeList, _references);

                    //实时更新测点的数据
                    UpdateVariableValue(OpcuaNodeList);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("调用接口初始化触发异常:" + ex.Message);
                    Console.ResetColor();
                }
            }
        }
        private List<OpcuaNode> ReadJsonNodes()
        {
            List<OpcuaNode> nodes = new List<OpcuaNode>();
            using (StreamReader file = File.OpenText("Nodes.json"))
            {
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    var baseimgjson = JsonConvert.DeserializeObject<NodeModel>(JToken.ReadFrom(reader).ToString());//序列化
                    foreach (var item in baseimgjson.drivers)
                    {
                        
                    }
                }
            }
            return nodes;
        }
        private List<OpcuaNode> ReadXmlNodes()
        {
            List<OpcuaNode> nodes = new List<OpcuaNode>();
            XmlDocument doc = new XmlDocument();
            doc.Load("Nodes.xml");    //加载Xml文件  
            XmlElement rootElem = doc.DocumentElement; //获取根节点  
            Readxml(rootElem, ref nodes);
            return nodes;

        }
        private void Readxml(XmlNode xml, ref List<OpcuaNode> list)
        {
            list.Add(CreateOpcuaNode(xml));
            if (xml.HasChildNodes)
            {
                foreach (XmlNode child in xml.ChildNodes)
                {
                    Readxml(child, ref list);
                }
            }
        }
        /// <summary>
        /// 推算节点名称
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        private string[] GetDocName(XmlNode xml)
        {
            string ip = "", driver = "";
            List<string> ss = new List<string>();
            ss.Add(xml.Name);
            if (xml.ParentNode.Name != "#document")
            {
                var nd = xml;
                for (int i = 0; i < 20; i++)
                {
                    var parent = nd.ParentNode;
                    ss.Add(parent.Name);
                    if (parent.ParentNode.Name == "#document")
                    {
                        break;
                    }
                    if (parent.Attributes["IP"] != null)
                    {
                        ip = parent.Attributes["IP"].Value;
                        if (parent.Attributes["Driver"] != null)
                        {
                            driver = parent.Attributes["Driver"].Value;
                        }
                    }
                    nd = parent;
                }
            }
            string name = "", pname = "";
            for (int i = ss.Count - 1; i >= 0; i--)
            {
                if (i != 0)
                {
                    if (pname != "")
                    {
                        pname += ".";
                    }
                    pname += ss[i];
                }
                if (name != "")
                {
                    name += ".";
                }
                name += ss[i];
            }
            return new string[] { name, pname, ip, driver };
        }
        private OpcuaNode CreateOpcuaNode(NodeFolder xnd)
        {
            return null;
        }
        private OpcuaNode CreateOpcuaNode(NodeItem xnd)
        {
            return null;
        }
        private OpcuaNode CreateOpcuaNode(XmlNode xnd)
        {
            var paths = GetDocName(xnd);
            OpcuaNode node = new OpcuaNode() { NodeId = paths[0], NodeName = xnd.Name, NodePath = paths[0], NodeType = NodeType.Channel, ParentPath = paths[1], IsTerminal = false };
            if (xnd.Attributes.Count == 0 || xnd.Attributes["IP"] != null)
            {//目录 
                if (xnd.ParentNode.Name == "#document")
                {
                    node.NodeType = NodeType.Scada;
                }
            }
            else
            {
                node.NodeType = NodeType.Measure;
                node.IsTerminal = true;
                if (xnd.Attributes["DataType"] != null)
                {
                    node.DataType = xnd.Attributes["DataType"].Value;
                }
                if (xnd.Attributes["Length"] != null)
                {
                    node.Length = int.Parse(xnd.Attributes["Length"].Value);
                }
                //if (xnd.Attributes["Readonly"] != null)
                //{
                //    node.Readonly = int.Parse(xnd.Attributes["Readonly"].Value);
                //}
              
            }
            return node;
        }


        /// <summary>
        /// 生成根节点(由于根节点需要特殊处理,此处单独出来一个方法)
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="references"></param>
        private void GeneraterNodes(List<OpcuaNode> nodes, IList<IReference> references)
        {
            var list = nodes.Where(d => d.NodeType == NodeType.Scada);
            foreach (var item in list)
            {
                try
                {
                    FolderState root = CreateFolder(null, item.NodePath, item.NodeName);
                    root.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                    references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, root.NodeId));
                    root.EventNotifier = EventNotifiers.SubscribeToEvents;
                    AddRootNotifier(root);
                    CreateNodes(nodes, root, item.NodePath);
                    _folderDic.Add(item.NodePath, root);
                    //添加引用关系
                    AddPredefinedNode(SystemContext, root);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("创建OPC-UA根节点触发异常:" + ex.Message);
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// 递归创建子节点(包括创建目录和测点)
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="parent"></param>
        private void CreateNodes(List<OpcuaNode> nodes, FolderState parent, string parentPath)
        {
            var list = nodes.Where(d => d.ParentPath == parentPath);
            foreach (var node in list)
            {
                try
                {
                    if (!node.IsTerminal)
                    {
                        FolderState folder = CreateFolder(parent, node.NodePath, node.NodeName);
                        _folderDic.Add(node.NodePath, folder);
                        CreateNodes(nodes, folder, node.NodePath);
                    }
                    else
                    {

                        BaseDataVariableState variable = CreateVariable(parent, node);
                        _nodeDic.Add(node.NodeId, variable);
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("创建OPC-UA子节点触发异常:" + ex.Message);
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// 创建目录
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private FolderState CreateFolder(NodeState parent, string path, string name)
        {
            FolderState folder = new FolderState(parent);

            folder.SymbolicName = name;
            folder.ReferenceTypeId = ReferenceTypes.Organizes;
            folder.TypeDefinitionId = ObjectTypeIds.FolderType;
            folder.NodeId = new NodeId(path, NamespaceIndex);
            folder.BrowseName = new QualifiedName(path, NamespaceIndex);
            folder.DisplayName = new LocalizedText("en", name);
            folder.WriteMask = AttributeWriteMask.None;
            folder.UserWriteMask = AttributeWriteMask.None;
            folder.EventNotifier = EventNotifiers.None;

            if (parent != null)
            {
                parent.AddChild(folder);
            }

            return folder;
        }


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        private BaseDataVariableState CreateVariable(NodeState parent, OpcuaNode node)
        {
            BaseDataVariableState variable = new BaseDataVariableState(parent);// GetDataType(parent, node);
            //variable.DataType = GetDataType(node);
            variable.DataType = DataTypeIds.ObjectNode;
            variable.SymbolicName = node.NodeName;
            variable.ReferenceTypeId = ReferenceTypes.Organizes;
            variable.TypeDefinitionId = VariableTypeIds.BaseDataVariableType;
            variable.NodeId = new NodeId(node.NodeId, NamespaceIndex);
            variable.BrowseName = new QualifiedName(node.NodeId, NamespaceIndex);
            variable.DisplayName = new LocalizedText("en", node.NodeName);
            variable.WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description;
            variable.UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description;
            variable.ValueRank = ValueRanks.Scalar;
            //if (variable.DataType != DataTypeIds.String)
            //{
            //    variable.ValueRank = node.Length > 1 ? ValueRanks.OneDimension : ValueRanks.Scalar;
            //}
            //variable.DataType = GetDataType(node);
            variable.AccessLevel = AccessLevels.CurrentReadOrWrite;
            variable.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
            variable.Historizing = false;
            variable.Value = 0;
            variable.StatusCode = StatusCodes.Good;
            variable.Timestamp = DateTime.Now;
            variable.OnWriteValue = OnWriteDataValue;
            variable.Value = InitValue(node);

            AutoChgValue(node);

            //variable.OnReadValue = OnReadDataValue;//有问题
            //if (valueRank == ValueRanks.OneDimension)
            //{
            //    variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { (uint)len });
            //}
            //else if (valueRank == ValueRanks.TwoDimensions)
            //{
            //    variable.ArrayDimensions = new ReadOnlyList<uint>(new List<uint> { 0, 0 });
            //}

            if (parent != null)
            {
                parent.AddChild(variable);
            }

            return variable;
        }


        private object InitValue(OpcuaNode tp)
        {

            if (tp.Length <= 0)
            {
                switch (tp.DataType.ToLower())
                {
                    case "bool":
                        return false;
                    case "byte":
                        return (byte)0;
                    case "double":
                        return (double)0;
                    case "float":
                        return (float)0;
                    case "int16":
                        return (short)0;
                    case "int32":
                        return (int)0;
                    case "int64":
                        return (long)0;
                    case "string":
                        return "string";
                    case "uint16":
                        return (ushort)0;
                    case "uint32":
                        return (uint)0;
                    case "uint64":
                        return (ulong)0;
                    case "datetime":
                        return DateTime.Now;
                    case "object":
                        return new { code="code",name="name1",id=1 };
                    default:
                        return string.Empty;

                }
            }
            else
            {
                switch (tp.DataType.ToLower())
                {
                    case "bool":
                        return new bool[tp.Length];
                    case "byte":
                        return new byte[tp.Length];
                    case "double":
                        return new double[tp.Length];
                    case "float":
                        return new float[tp.Length];
                    case "int16":
                        return new short[tp.Length];
                    case "int32":
                        return new int[tp.Length];
                    case "int64":
                        return new long[tp.Length];
                    case "string":
                        return new string[tp.Length];
                    case "uint16":
                        return new ushort[tp.Length];
                    case "uint32":
                        return new uint[tp.Length];
                    case "uint64":
                        return new ulong[tp.Length];
                    case "datetime":
                        return new DateTime[tp.Length];
                    default:
                        return string.Empty;

                }
            }
           
          
        }

        private void AutoChgValue(OpcuaNode uanode)
        {

            return;
            {
                 
                Task.Run(async () => {
                    await Task.Delay(3000);
                    if (!_nodeDic.TryGetValue(uanode.NodeId, out BaseDataVariableState nv))
                    {
                        return;
                    }
                    while (true) 
                    {
                        await Task.Delay(1000);
                        object values = nv.Value;
                        if (uanode.Length == 0)
                        {
                            var value = values;
                            
                            switch (uanode.DataType.ToLower())
                            {
                                case "bool":
                                    value = !(bool)value;
                                    break;
                                case "byte":
                                    value = (byte)value + 1;
                                    break;
                                case "double":
                                    value = (double)value + 0.1;
                                    break;
                                case "float":
                                    value = (float)value + 0.1;
                                    break;
                                case "int16":
                                    value = (short)value + 1;
                                    break;
                                case "string":
                                    value = "str:" + DateTime.Now.ToShortTimeString();
                                    break;
                                case "int32":
                                    value = (int)value + 1;
                                    break;
                                case "int64":
                                    value = (long)value + 1;
                                    break;
                                case "uint16":
                                    value = (ushort)value + 1;
                                    break;
                                case "uint32":
                                    value = (uint)value + 1;
                                    break;
                                case "uint64":
                                    value = (ulong)value + 1;
                                    break;
                                case "datetime":
                                    value = DateTime.Now;
                                    break;
                            }
                            values = value;
                        }
                        else
                        {
                            object value = DateTime.Now.Second;
                            switch (uanode.DataType.ToLower())
                            {
                                case "bool":
                                    {
                                        var listArray = new bool[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = DateTime.Now.Second % 2 == 0 ? true : false;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "byte":
                                    {
                                        var listArray = new byte[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (byte)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "double":
                                    {
                                        var listArray = new double[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (double)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "float":
                                    {
                                        var listArray = new float[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (float)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "int16":
                                    {
                                        var listArray = new short[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (short)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "string":
                                    {
                                        var listArray = new string[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = DateTime.Now.Second.ToString();
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "int32":
                                    {
                                        var listArray = new int[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (int)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "int64":
                                    {
                                        var listArray = new long[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (long)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "uint16":
                                    {
                                        var listArray = new ushort[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (ushort)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "uint32":
                                    {
                                        var listArray = new uint[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (uint)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                                case "uint64":
                                    {
                                        var listArray = new ulong[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] = (ulong)DateTime.Now.Second;
                                        }
                                        values = listArray;
                                    }
                                    break;
                             
                                case "datetime":
                                    {
                                        var listArray = new DateTime[uanode.Length];
                                        for (int i = 0; i < uanode.Length; i++)
                                        {
                                            listArray[i] =  DateTime.Now;
                                        }
                                        values = listArray;
                                    }
                                    break;
                           
                            }
                        }

                        SetNodeValue(ref nv, values);
                    }
                });
            }
        }


        /// <summary>
        /// PLC返回的变化值 更新节点
        /// </summary>
        /// <param name="d"></param>
        //private void DataChange(OpcuaNode d)
        //{
        //    try
        //    {
        //        if (_nodeDic.TryGetValue(d.NodeId, out BaseDataVariableState node))
        //        {
        //            SetNodeValue(ref node, );
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        Console.ForegroundColor = ConsoleColor.Red;
        //        Console.WriteLine("更新OPC-UA节点数据触发异常:" + ex.Message);
        //        Console.ResetColor();
        //    }
        //}

        private void SetNodeValue(ref BaseDataVariableState node, object value)
        {
            try
            {
                node.Value = value;
                //变化时间
                node.Timestamp = DateTime.Now;
                //变更标识  只有执行了这一步,订阅的客户端才会收到新的数据
                node.ClearChangeMasks(SystemContext, false);

            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.ResetColor();
            }


        }

     
        private bool WritePlcValue(NodeState nd, object value)
        {
            try
            {
                List<byte> bytes = new List<byte>();
                OpcuaNode uanode = nd.Handle as OpcuaNode;
                
                if (_nodeDic.TryGetValue(uanode.NodeId, out BaseDataVariableState nv))
                {
                    SetNodeValue(ref nv, value);
                }
                
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("写入PLC值异常：" + e.Message);
                Console.ResetColor();

            }
            return true;
        }

        //private bool ReadPlcValue(NodeState node)
        //{
        //    try
        //    {
        //        OpcuaNode nd = node.Handle as OpcuaNode;
        //        if (PLClist.TryGetValue(nd.IP, out IDriver dr))
        //        {
        //            var d = dr.Device(nd.Key);
        //            d.Read();
        //            var state = (BaseDataVariableState)node;
        //            SetNodeValue(ref state, d);
        //            return true;
        //        }
        //    }
        //    catch (Exception e)
        //    {

        //    }
        //    return false;

        //}

        /// <summary>
        /// 实时更新节点数据
        /// </summary>
        public void UpdateVariableValue(List<OpcuaNode> list)
        {
            Task.Factory.StartNew(() => {
                foreach (var tp in list)
                {
                    if (tp.IsTerminal)
                    {
                        //
                        //DataChange(tp);
                    }

                }

            });
         
        }
    
        /// <summary>
        /// 修改节点树(添加节点,删除节点,修改节点名称)
        /// </summary>
        /// <param name="nodes"></param>
        public void UpdateNodesAttribute(List<OpcuaNode> nodes)
        {
            //修改或创建根节点
            var scadas = nodes.Where(d => d.NodeType == NodeType.Scada);
            foreach (var item in scadas)
            {
                FolderState scadaNode = null;
                if (!_folderDic.TryGetValue(item.NodePath, out scadaNode))
                {
                    //如果根节点都不存在  那么整个树都需要创建
                    FolderState root = CreateFolder(null, item.NodePath, item.NodeName);
                    root.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
                    _references.Add(new NodeStateReference(ReferenceTypes.Organizes, false, root.NodeId));
                    root.EventNotifier = EventNotifiers.SubscribeToEvents;
                    AddRootNotifier(root);
                    CreateNodes(nodes, root, item.NodePath);
                    _folderDic.Add(item.NodePath, root);
                    AddPredefinedNode(SystemContext, root);
                    continue;
                }
                else
                {
                    scadaNode.DisplayName = item.NodeName;
                    scadaNode.ClearChangeMasks(SystemContext, false);
                }
            }
            //修改或创建目录(此处设计为可以有多级目录,上面是演示数据,所以我只写了三级,事实上更多级也是可以的)
            var folders = nodes.Where(d => d.NodeType != NodeType.Scada && !d.IsTerminal);
            foreach (var item in folders)
            {
                FolderState folder = null;
                if (!_folderDic.TryGetValue(item.NodePath, out folder))
                {
                    var par = GetParentFolderState(nodes, item);
                    folder = CreateFolder(par, item.NodePath, item.NodeName);
                    AddPredefinedNode(SystemContext, folder);
                    par.ClearChangeMasks(SystemContext, false);
                    _folderDic.Add(item.NodePath, folder);
                }
                else
                {
                    folder.DisplayName = item.NodeName;
                    folder.ClearChangeMasks(SystemContext, false);
                }
            }
            //修改或创建测点
            //这里我的数据结构采用IsTerminal来代表是否是测点  实际业务中可能需要根据自身需要调整
            var paras = nodes.Where(d => d.IsTerminal);
            foreach (var item in paras)
            {
                BaseDataVariableState node = null;
                if (_nodeDic.TryGetValue(item.NodeId, out node))
                {
                    node.DisplayName = item.NodeName;
                    node.Timestamp = DateTime.Now;
                    node.ClearChangeMasks(SystemContext, false);
                }
                else
                {
                    FolderState folder = null;
                    if (_folderDic.TryGetValue(item.ParentPath, out folder))
                    {
                        //node = CreateVariable(folder, item.NodeId, item.NodeName, DataTypeIds.Double, ValueRanks.Scalar);
                        node = CreateVariable(folder, item);
                        AddPredefinedNode(SystemContext, node);
                        folder.ClearChangeMasks(SystemContext, false);
                        _nodeDic.Add(item.NodeId, node);

                       
                    }
                }
            }

            /*
             * 将新获取到的菜单列表与原列表对比
             * 如果新菜单列表中不包含原有的菜单  
             * 则说明这个菜单被删除了  这里也需要删除
             */
            List<string> folderPath = _folderDic.Keys.ToList();
            List<string> nodePath = _nodeDic.Keys.ToList();
            var remNode = nodePath.Except(nodes.Where(d => d.IsTerminal).Select(d => d.NodeId.ToString()));
            foreach (var str in remNode)
            {
                BaseDataVariableState node = null;
                if (_nodeDic.TryGetValue(str, out node))
                {
                    var parent = node.Parent;
                    parent.RemoveChild(node);
                    _nodeDic.Remove(str);
                }
            }
            var remFolder = folderPath.Except(nodes.Where(d => !d.IsTerminal).Select(d => d.NodePath));
            foreach (string str in remFolder)
            {
                FolderState folder = null;
                if (_folderDic.TryGetValue(str, out folder))
                {
                    var parent = folder.Parent;
                    if (parent != null)
                    {
                        parent.RemoveChild(folder);
                        _folderDic.Remove(str);
                    }
                    else
                    {
                        RemoveRootNotifier(folder);
                        RemovePredefinedNode(SystemContext, folder, new List<LocalReference>());
                    }
                }
            }
        }

        /// <summary>
        /// 创建父级目录(请确保对应的根目录已创建)
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="currentNode"></param>
        /// <returns></returns>
        public FolderState GetParentFolderState(IEnumerable<OpcuaNode> nodes, OpcuaNode currentNode)
        {
            FolderState folder = null;
            if (!_folderDic.TryGetValue(currentNode.ParentPath, out folder))
            {
                var parent = nodes.Where(d => d.NodePath == currentNode.ParentPath).FirstOrDefault();
                if (!string.IsNullOrEmpty(parent.ParentPath))
                {
                    var pFol = GetParentFolderState(nodes, parent);
                    folder = CreateFolder(pFol, parent.NodePath, parent.NodeName);
                    pFol.ClearChangeMasks(SystemContext, false);
                    AddPredefinedNode(SystemContext, folder);
                    _folderDic.Add(currentNode.ParentPath, folder);
                }
            }
            return folder;
        }

        /// <summary>
        /// 客户端写入值时触发(绑定到节点的写入事件上)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        /// <param name="indexRange"></param>
        /// <param name="dataEncoding"></param>
        /// <param name="value"></param>
        /// <param name="statusCode"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        private ServiceResult OnWriteDataValue(ISystemContext context, NodeState node, NumericRange indexRange, QualifiedName dataEncoding,
            ref object value, ref StatusCode statusCode, ref DateTime timestamp)
        {
            BaseDataVariableState variable = node as BaseDataVariableState;
            try
            {
                if (node.Handle == null)
                {//绑定
                    node.Handle = OpcuaNodeList.Find(t => t.NodeId == node.NodeId.Identifier.ToString());
                    if (node.Handle == null)
                    {
                        return StatusCodes.BadBoundNotFound;
                    }
                }

                //验证数据类型
                //TypeInfo typeInfo = TypeInfo.IsInstanceOfDataType(
                //    value,
                //    variable.DataType,
                //    variable.ValueRank,
                //    context.NamespaceUris,
                //    context.TypeTable);

                //if (typeInfo == null || typeInfo == TypeInfo.Unknown)
                //{
                //    return StatusCodes.BadTypeMismatch;
                //}
                if (!WritePlcValue(node, value))
                {
                    return StatusCodes.BadCommunicationError;
                }

               

                //if (typeInfo.BuiltInType == BuiltInType.Double)
                //{
                //    double number = Convert.ToDouble(value);
                //    value = TypeInfo.Cast(number, typeInfo.BuiltInType);
                //}
                return ServiceResult.Good;
            }
            catch (Exception)
            {
                return StatusCodes.BadTypeMismatch;
            }
        }
    
        /// <summary>
        /// 客户端读取时发生
        /// </summary>
        /// <param name="context"></param>
        /// <param name="node"></param>
        /// <param name="indexRange"></param>
        /// <param name="dataEncoding"></param>
        /// <param name="value"></param>
        /// <param name="statusCode"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        private ServiceResult OnReadDataValue(ISystemContext context, NodeState node, NumericRange indexRange, QualifiedName dataEncoding,
          ref object value, ref StatusCode statusCode, ref DateTime timestamp)
        {
             
       
            BaseDataVariableState variable = node as BaseDataVariableState;
            var b= variable.Historizing;
            try
            {
                if (node.Handle == null)
                {//绑定
                    node.Handle = OpcuaNodeList.Find(t => t.NodeId == node.NodeId.Identifier.ToString());
                    if (node.Handle == null)
                    {
                        return StatusCodes.BadBoundNotFound;
                    }
                }
                 
                    //OpcuaNode nd = (OpcuaNode)node.Handle;
                    //if (nd.ScanTime == 0)
                    //{//不扫描的要手动从PLC读回来
                    //    DateTime t = DateTime.Now;
                    //    if (!ReadPlcValue(node))
                    //    {
                    //        return StatusCodes.BadCommunicationError;
                    //    }
                    //    Console.WriteLine(DateTime.Now.Subtract(t).TotalMilliseconds);
                    //}
                 
               
                return ServiceResult.Good;
            }
            catch (Exception)
            {
                return StatusCodes.BadTypeMismatch;
            }
        }

       
        /// <summary>
        /// 读取历史数据
        /// </summary>
        /// <param name="context"></param>
        /// <param name="details"></param>
        /// <param name="timestampsToReturn"></param>
        /// <param name="releaseContinuationPoints"></param>
        /// <param name="nodesToRead"></param>
        /// <param name="results"></param>
        /// <param name="errors"></param>
        public override void HistoryRead(OperationContext context, HistoryReadDetails details, TimestampsToReturn timestampsToReturn, bool releaseContinuationPoints,
            IList<HistoryReadValueId> nodesToRead, IList<HistoryReadResult> results, IList<ServiceResult> errors)
        {
            ReadProcessedDetails readDetail = details as ReadProcessedDetails;
            //假设查询历史数据  都是带上时间范围的
            if (readDetail == null || readDetail.StartTime == DateTime.MinValue || readDetail.EndTime == DateTime.MinValue)
            {
                errors[0] = StatusCodes.BadHistoryOperationUnsupported;
                return;
            }
            for (int ii = 0; ii < nodesToRead.Count; ii++)
            {
                int sss = readDetail.StartTime.Millisecond;
                double res = sss + DateTime.Now.Millisecond;
                //这里  返回的历史数据可以是多种数据类型  请根据实际的业务来选择
                Opc.Ua.KeyValuePair keyValue = new Opc.Ua.KeyValuePair()
                {
                    Key = new QualifiedName(nodesToRead[ii].NodeId.Identifier.ToString()),
                    Value = res
                };
                results[ii] = new HistoryReadResult()
                {
                    StatusCode = StatusCodes.Good,
                    HistoryData = new ExtensionObject(keyValue)
                };
                errors[ii] = StatusCodes.Good;
                //切记,如果你已处理完了读取历史数据的操作,请将Processed设为true,这样OPC-UA类库就知道你已经处理过了 不需要再进行检查了
                nodesToRead[ii].Processed = true;
            }
        }
    }
}
