using System.Collections.Generic;
using System.Threading.Tasks;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

namespace DroneC2.Coordinate
{
    /// <summary>
    /// Cesium 지오레퍼런스 기준으로 WGS84 위경도·타원체고를 Unity 월드 좌표로 변환한다.
    /// 월드 축은 지오레퍼런스 원점에서 x=동, y=위, z=북이다. 고도는 타원체고(ellipsoid height)로 해석한다.
    /// 지형 타일셋에서 지표 높이를 조회해 <see cref="ITerrainHeightSampler"/>도 제공한다.
    /// </summary>
    public class CesiumGeoreferenceConverter : MonoBehaviour, ICoordinateConverter, ITerrainHeightSampler
    {
        [SerializeField] CesiumGeoreference georeference;
        [SerializeField] Cesium3DTileset terrainTileset;

        // 격자 간격(도). 0.0002도는 위도 방향 약 22m, 경도 방향 약 18m(북위 35.5도).
        [SerializeField, Min(0.00001f)] float gridCellDeg = 0.0002f;
        // 보간에 필요한 2x2 노드 바깥으로 미리 읽어 둘 노드 수. 이동 방향 선읽기용.
        [SerializeField, Min(0)] int prefetchMargin = 2;

        TerrainHeightGrid grid;
        readonly List<(double latitude, double longitude)> pending = new List<(double, double)>();
        List<(double latitude, double longitude)> inFlightNodes;
        Task<CesiumSampleHeightResult> inFlight;
        float inFlightStartTime;
        bool loggedFirstLatency;

        /// <summary>마지막으로 완료된 지표 높이 조회의 소요 시간(초). 진단용.</summary>
        public float LastRequestLatencySeconds { get; private set; }

        public void SetGeoreference(CesiumGeoreference value)
        {
            georeference = value;
        }

        public Vector3 ToWorld(double latitude, double longitude, double altitudeM)
        {
            CesiumGeoreference geo = ResolveGeoreference();
            double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
                new double3(longitude, latitude, altitudeM));
            double3 unity = geo.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
            return new Vector3((float)unity.x, (float)unity.y, (float)unity.z);
        }

        /// <summary>
        /// 위치를 받지 않는 인터페이스라 원점 기준 ENU 회전을 쓴다.
        /// 원점에서 수 km 이내라면 지구 곡률에 의한 기울기(5km에서 약 0.05°)는 무시할 수 있다.
        /// </summary>
        public Quaternion HeadingToRotation(double headingDeg)
        {
            return Quaternion.Euler(0f, (float)headingDeg, 0f);
        }

        public bool TryGetGroundHeight(double latitude, double longitude, out double ellipsoidHeightM)
        {
            if (grid == null)
                grid = new TerrainHeightGrid(gridCellDeg);

            // 필요한 노드와 이동 방향 선읽기 노드를 요청 대기열에 모은다. (조회는 Update에서 배치로 보낸다)
            grid.CollectMissingNodes(latitude, longitude, prefetchMargin, pending);
            return grid.TryInterpolate(latitude, longitude, out ellipsoidHeightM);
        }

        void Update()
        {
            if (inFlight != null)
            {
                if (!inFlight.IsCompleted)
                    return;
                CompleteRequest();
            }

            if (pending.Count == 0)
                return;

            Cesium3DTileset tileset = ResolveTileset();
            if (tileset == null)
                return;

            var positions = new double3[pending.Count];
            for (int i = 0; i < pending.Count; i++)
                positions[i] = new double3(pending[i].longitude, pending[i].latitude, 0.0);

            inFlightNodes = new List<(double, double)>(pending);
            pending.Clear();
            inFlightStartTime = Time.realtimeSinceStartup;
            inFlight = tileset.SampleHeightMostDetailed(positions);
        }

        void OnDisable()
        {
            // 진행 중이던 요청의 노드는 다음 활성화 때 다시 요청할 수 있도록 격자를 새로 만든다.
            inFlight = null;
            inFlightNodes = null;
            pending.Clear();
            grid = null;
        }

        void CompleteRequest()
        {
            Task<CesiumSampleHeightResult> task = inFlight;
            List<(double latitude, double longitude)> nodes = inFlightNodes;
            inFlight = null;
            inFlightNodes = null;

            LastRequestLatencySeconds = Time.realtimeSinceStartup - inFlightStartTime;
            if (!loggedFirstLatency)
            {
                loggedFirstLatency = true;
                Debug.Log($"CesiumGeoreferenceConverter: 첫 지표 높이 조회 {nodes.Count}점, {LastRequestLatencySeconds:F2}초");
            }

            if (grid == null)
                return;

            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning("CesiumGeoreferenceConverter: 지표 높이 조회에 실패했습니다.", this);
                foreach (var node in nodes)
                    grid.MarkRequestFailed(node.latitude, node.longitude);
                return;
            }

            CesiumSampleHeightResult result = task.Result;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (result.sampleSuccess[i])
                    grid.SetNodeHeight(nodes[i].latitude, nodes[i].longitude,
                        result.longitudeLatitudeHeightPositions[i].z);
                else
                    grid.MarkRequestFailed(nodes[i].latitude, nodes[i].longitude);
            }
        }

        Cesium3DTileset ResolveTileset()
        {
            if (terrainTileset == null)
                terrainTileset = FindAnyObjectByType<Cesium3DTileset>();
            return terrainTileset;
        }

        CesiumGeoreference ResolveGeoreference()
        {
            if (georeference == null)
                georeference = GetComponentInParent<CesiumGeoreference>();
            if (georeference == null)
                georeference = FindAnyObjectByType<CesiumGeoreference>();
            if (georeference == null)
                throw new System.InvalidOperationException(
                    "CesiumGeoreferenceConverter: 씬에서 CesiumGeoreference를 찾을 수 없습니다.");
            return georeference;
        }
    }
}
