using System.Collections.Generic;
using DroneC2.Coordinate;
using UnityEngine;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>
    /// MAVLink UDP 수신기(씬에 하나). 소켓·스레드는 <see cref="MavlinkUdpListener"/>가 맡고,
    /// 이 컴포넌트는 메인 스레드 <c>Update</c>에서 프레임을 꺼내 라우터로 보낸 뒤
    /// sysid별 <see cref="MavlinkUnitSource"/>로 나눠 준다. 수신 전용이며 아무것도 송신하지 않는다.
    /// 좌표 계층 의존은 홈 지형 높이 조회(ITerrainHeightSampler) 하나뿐이다.
    /// </summary>
    public class MavlinkReceiver : MonoBehaviour
    {
        [Tooltip("루프백(127.0.0.1)에서만 수신한다. 시뮬레이터/포워딩 출력 포트와 같게 맞춘다.")]
        [SerializeField] int port = 14560;
        [SerializeField] MavlinkUnitTable table;
        [Tooltip("ITerrainHeightSampler를 구현한 변환기. 비워 두면 홈 지형 높이를 0으로 본다.")]
        [SerializeField] MonoBehaviour terrainSamplerBehaviour;
        [SerializeField] int maxQueuedFrames = 2048;
        [Tooltip("한 프레임에 처리할 최대 프레임 수. 넘치면 다음 프레임으로 넘긴다.")]
        [SerializeField] int maxFramesPerUpdate = 512;

        readonly Dictionary<byte, MavlinkUnitSource> sources = new Dictionary<byte, MavlinkUnitSource>();
        MavlinkUdpListener listener;
        MavlinkTelemetryRouter router;
        ITerrainHeightSampler terrainSampler;

        public MavlinkParser Parser => listener?.Parser;
        public long Datagrams => listener?.Datagrams ?? 0;
        public long DroppedFrames => listener?.Dropped ?? 0;
        public long UnknownSysid => router?.UnknownSysid ?? 0;
        public long HeldForHome => router?.HeldForHome ?? 0;
        public long SamplesProduced => router?.SamplesProduced ?? 0;

        void OnValidate()
        {
            if (terrainSamplerBehaviour != null && !(terrainSamplerBehaviour is ITerrainHeightSampler))
            {
                Debug.LogError($"{terrainSamplerBehaviour.name}은(는) ITerrainHeightSampler를 구현하지 않습니다.", this);
                terrainSamplerBehaviour = null;
            }
        }

        public void Register(byte systemId, MavlinkUnitSource source)
        {
            sources[systemId] = source;
        }

        public void Unregister(byte systemId, MavlinkUnitSource source)
        {
            if (sources.TryGetValue(systemId, out MavlinkUnitSource current) && current == source)
                sources.Remove(systemId);
        }

        void OnEnable()
        {
            if (table == null)
            {
                Debug.LogError("MavlinkReceiver: 유닛 테이블(MavlinkUnitTable)을 지정해야 합니다.", this);
                enabled = false;
                return;
            }

            terrainSampler = terrainSamplerBehaviour as ITerrainHeightSampler;
            router = new MavlinkTelemetryRouter(table,
                terrainSampler != null ? (MavlinkTelemetryRouter.HomeHeightLookup)terrainSampler.TryGetGroundHeight : null,
                message => Debug.LogWarning(message, this));
            router.SampleProduced += OnSampleProduced;

            listener = new MavlinkUdpListener(port, maxQueuedFrames);
            try
            {
                listener.Start();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MavlinkReceiver: 127.0.0.1:{port} 바인드 실패 — {e.Message}", this);
                listener.Dispose();
                listener = null;
                enabled = false;
            }
        }

        void OnDisable()
        {
            listener?.Dispose();
            listener = null;
            if (router != null)
            {
                router.SampleProduced -= OnSampleProduced;
                router = null;
            }
        }

        void Update()
        {
            double now = Time.timeAsDouble;
            for (int i = 0; i < maxFramesPerUpdate && listener.TryDequeue(out MavlinkFrame frame); i++)
                router.Handle(frame, now);
        }

        void OnSampleProduced(byte systemId, TelemetrySample sample)
        {
            if (sources.TryGetValue(systemId, out MavlinkUnitSource source))
                source.Dispatch(sample);
        }
    }
}
