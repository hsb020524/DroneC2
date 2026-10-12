using System;
using System.Collections.Generic;

namespace DroneC2.Coordinate
{
    /// <summary>
    /// 지표 높이 격자 캐시. 격자 노드의 높이를 저장하고 쌍선형 보간으로 임의 지점의 높이를 돌려준다.
    /// 값이 없는 노드를 찾아 요청 대상으로 모으기만 하며 실제 조회는 하지 않는다. (엔진·Cesium 비의존)
    /// </summary>
    public class TerrainHeightGrid
    {
        readonly double cellDeg;
        readonly Dictionary<(long, long), double> heights = new Dictionary<(long, long), double>();
        readonly HashSet<(long, long)> requested = new HashSet<(long, long)>();

        public TerrainHeightGrid(double cellSizeDeg)
        {
            if (cellSizeDeg <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(cellSizeDeg));
            cellDeg = cellSizeDeg;
        }

        public int NodeCount => heights.Count;

        /// <summary>주변 4개 노드가 모두 있을 때만 보간한 높이를 돌려준다.</summary>
        public bool TryInterpolate(double latitude, double longitude, out double heightM)
        {
            Locate(latitude, longitude, out long i0, out long j0, out double fy, out double fx);

            if (heights.TryGetValue((i0, j0), out double h00) &&
                heights.TryGetValue((i0, j0 + 1), out double h01) &&
                heights.TryGetValue((i0 + 1, j0), out double h10) &&
                heights.TryGetValue((i0 + 1, j0 + 1), out double h11))
            {
                double south = h00 + (h01 - h00) * fx;
                double north = h10 + (h11 - h10) * fx;
                heightM = south + (north - south) * fy;
                return true;
            }

            heightM = 0.0;
            return false;
        }

        /// <summary>
        /// 지점 주변에서 값도 없고 요청도 하지 않은 노드를 output에 추가하고 요청됨으로 표시한다.
        /// prefetchMargin은 보간에 필요한 2x2 바깥으로 더 읽어 둘 노드 수(이동 방향 선읽기)이다.
        /// </summary>
        public void CollectMissingNodes(double latitude, double longitude, int prefetchMargin,
            List<(double latitude, double longitude)> output)
        {
            Locate(latitude, longitude, out long i0, out long j0, out _, out _);
            int margin = Math.Max(0, prefetchMargin);

            for (long i = i0 - margin; i <= i0 + 1 + margin; i++)
            {
                for (long j = j0 - margin; j <= j0 + 1 + margin; j++)
                {
                    var key = (i, j);
                    if (heights.ContainsKey(key) || requested.Contains(key))
                        continue;

                    requested.Add(key);
                    output.Add((i * cellDeg, j * cellDeg));
                }
            }
        }

        /// <summary>조회 결과를 노드에 저장한다. 노드 좌표는 CollectMissingNodes가 돌려준 값이다.</summary>
        public void SetNodeHeight(double latitude, double longitude, double heightM)
        {
            var key = NodeKey(latitude, longitude);
            heights[key] = heightM;
            requested.Remove(key);
        }

        /// <summary>조회에 실패한 노드를 다시 요청할 수 있게 한다.</summary>
        public void MarkRequestFailed(double latitude, double longitude)
        {
            requested.Remove(NodeKey(latitude, longitude));
        }

        (long, long) NodeKey(double latitude, double longitude)
        {
            return ((long)Math.Round(latitude / cellDeg), (long)Math.Round(longitude / cellDeg));
        }

        void Locate(double latitude, double longitude, out long i0, out long j0, out double fy, out double fx)
        {
            double y = latitude / cellDeg;
            double x = longitude / cellDeg;
            i0 = (long)Math.Floor(y);
            j0 = (long)Math.Floor(x);
            fy = y - i0;
            fx = x - j0;
        }
    }
}
