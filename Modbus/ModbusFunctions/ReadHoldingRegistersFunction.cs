using Common;
using Modbus.FunctionParameters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace Modbus.ModbusFunctions
{
    /// <summary>
    /// Class containing logic for parsing and packing modbus read holding registers functions/requests.
    /// </summary>
    public class ReadHoldingRegistersFunction : ModbusFunction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReadHoldingRegistersFunction"/> class.
        /// </summary>
        /// <param name="commandParameters">The modbus command parameters.</param>
        public ReadHoldingRegistersFunction(ModbusCommandParameters commandParameters) : base(commandParameters)
        {
            CheckArguments(MethodBase.GetCurrentMethod(), typeof(ModbusReadCommandParameters));
        }

        /// <inheritdoc />
        public override byte[] PackRequest()
        {
            ModbusReadCommandParameters parametri = this.CommandParameters as ModbusReadCommandParameters;
            byte[] paketi = new byte[12];
            Buffer.BlockCopy((Array)BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.TransactionId)), 0, paketi, 0, 2);
            Buffer.BlockCopy((Array)BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.ProtocolId)), 0, paketi, 2, 2);
            Buffer.BlockCopy((Array)BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.Length)), 0, paketi, 4, 2);
            paketi[6] = parametri.UnitId;
            paketi[7] = parametri.FunctionCode;
            Buffer.BlockCopy((Array)BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.StartAddress)), 0, paketi, 8, 2);
            Buffer.BlockCopy((Array)BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.Quantity)), 0, paketi, 10, 2);
            return paketi;
        }

        /// <inheritdoc />
        public override Dictionary<Tuple<PointType, ushort>, ushort> ParseResponse(byte[] response)
        {
            ModbusReadCommandParameters mpar = this.CommandParameters as ModbusReadCommandParameters;
            Dictionary<Tuple<PointType, ushort>, ushort> dict = new Dictionary<Tuple<PointType, ushort>, ushort>();

            if (response[7] == mpar.FunctionCode + 0x80)
            {
                HandeException(response[8]);
                return dict; 
            }

            ushort adresa = mpar.StartAddress;
            int byteCount = response[8]; 
             for (int i = 0; i < byteCount; i += 2)
            {
                ushort value = BitConverter.ToUInt16(response, 9 + i);
                value = (ushort)IPAddress.NetworkToHostOrder((short)value); 

                dict.Add(new Tuple<PointType, ushort>(PointType.ANALOG_OUTPUT, adresa), value);
                adresa++;
            }

            return dict;
        }
    }
}