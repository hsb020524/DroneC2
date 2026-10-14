namespace DroneC2.DataSource.Mavlink
{
    /// <summary>MAVLink 프레임 검증용 CRC-16/MCRF4XX(X.25).</summary>
    public static class MavlinkCrc
    {
        public const ushort Init = 0xFFFF;

        public static ushort Accumulate(byte b, ushort crc)
        {
            byte tmp = (byte)(b ^ (byte)(crc & 0xFF));
            tmp ^= (byte)(tmp << 4);
            return (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
        }

        public static ushort Compute(byte[] data, int offset, int count, ushort crc = Init)
        {
            for (int i = 0; i < count; i++)
                crc = Accumulate(data[offset + i], crc);
            return crc;
        }
    }
}
