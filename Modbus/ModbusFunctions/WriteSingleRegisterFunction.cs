using Common;
using Modbus.FunctionParameters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace Modbus.ModbusFunctions
{
    /// <summary>
    /// Class containing logic for parsing and packing modbus write single register functions/requests.
    /// </summary>
    public class WriteSingleRegisterFunction : ModbusFunction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WriteSingleRegisterFunction"/> class.
        /// </summary>
        /// <param name="commandParameters">The modbus command parameters.</param>
        public WriteSingleRegisterFunction(ModbusCommandParameters commandParameters) : base(commandParameters)
        {
            CheckArguments(MethodBase.GetCurrentMethod(), typeof(ModbusWriteCommandParameters));
        }

        /// <inheritdoc />
        public override byte[] PackRequest()
        {
            ModbusWriteCommandParameters parametri = this.CommandParameters as ModbusWriteCommandParameters;
            byte[] paket = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.TransactionId)), 0, paket, 0, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.ProtocolId)), 0, paket, 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.Length)), 0, paket, 4, 2);
            paket[6] = parametri.UnitId;
            paket[7] = parametri.FunctionCode;
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.OutputAddress)), 0, paket, 8, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)parametri.Value)), 0, paket, 10, 2);
            return paket; ;
        }

        /// <inheritdoc />
        public override Dictionary<Tuple<PointType, ushort>, ushort> ParseResponse(byte[] response)
        {
            var dict = new Dictionary<Tuple<PointType, ushort>, ushort>();

            var adresa = BitConverter.ToUInt16(response, 8);
            var vrijednost = BitConverter.ToUInt16(response, 10);

            adresa = (ushort)IPAddress.NetworkToHostOrder((short)adresa);
            vrijednost = (ushort)IPAddress.NetworkToHostOrder((short)vrijednost);

            dict.Add(new Tuple<PointType, ushort>(PointType.DIGITAL_OUTPUT, adresa), vrijednost);

            return dict;
        }
    }
}