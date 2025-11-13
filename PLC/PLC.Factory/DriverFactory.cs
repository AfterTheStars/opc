
using PLC.Interface;
using PLC.KvHost;
using PLC.FinsUDP;
using PLC.MC;
using PLC.ModbusTcp;

namespace PLC.Factory
{
    public class DriverFactory
    {
        public static IDriver Driver(DeviceType tp,string IP,bool IsAuto=true)
        {
            switch (tp)
            {
             
                case DeviceType.HostLinkTcp:
                    return new HostLinkManage(IP, IsAuto);
                case DeviceType.HostLinkUdp:
                    return new HostLinkManage(IP, IsAuto,true);
                case DeviceType.McAsciiTcp:
                    return  new McAsciiManage(IP, IsAuto,false);
                case DeviceType.McAsciiUdp:
                    return new McAsciiManage(IP, IsAuto, true);
                case DeviceType.McByteTcp:
                    return new McAsciiManage(IP, IsAuto,false,true);
                case DeviceType.McByteUdp:
                    return new McAsciiManage(IP, IsAuto, true, true);
                case DeviceType.ModbusTcp:
                    return new ModbusManage(IP, IsAuto);
                case DeviceType.FinsUdp:
                    return new FinsUdpManage(IP, IsAuto);
                default:
                    throw new System.Exception("暂不支持"+ tp.ToString());
            }
        }
    }

    public enum DeviceType
    {
         HostLinkTcp,
         HostLinkUdp,
         KvHost,
         McAsciiTcp,
         McAsciiUdp,
         McByteTcp,
         McByteUdp,
         ModbusTcp,
         FinsTcp,
         FinsUdp,
         Ads ,
         OpcUa,
         Http
    }
}
