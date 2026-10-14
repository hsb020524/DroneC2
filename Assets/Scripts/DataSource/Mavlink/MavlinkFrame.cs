namespace DroneC2.DataSource.Mavlink
{
    /// <summary>CRC까지 검증을 마친 MAVLink 프레임 한 건(v1·v2 공통).</summary>
    public readonly struct MavlinkFrame
    {
        public readonly byte SystemId;
        public readonly byte ComponentId;
        public readonly uint MessageId;

        /// <summary>
        /// 페이로드. v2에서 끝 0바이트가 잘려 온 것은 이미 0으로 복원되어 메시지 정의 길이와 같다.
        /// </summary>
        public readonly byte[] Payload;

        public MavlinkFrame(byte systemId, byte componentId, uint messageId, byte[] payload)
        {
            SystemId = systemId;
            ComponentId = componentId;
            MessageId = messageId;
            Payload = payload;
        }
    }
}
