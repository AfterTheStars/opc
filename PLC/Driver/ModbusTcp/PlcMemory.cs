using System;

namespace PLC.ModbusTcp
{
	public enum PlcMemory
	{
		Coil=1,
		InputCoil=2,
		Register=3,
		InputRegister=4,
		Err=-1
	}
}
