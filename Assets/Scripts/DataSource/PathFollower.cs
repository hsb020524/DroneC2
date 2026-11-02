using System;
using System.Collections.Generic;

namespace DroneC2.DataSource
{
    /// <summary>
    /// 경로를 따라 이동한 거리로 위치·방위를 구하는 순수 계산. 경로는 마지막 점에서 첫 점으로 닫힌다(순환).
    /// 같은 수식이 tools/path_follower.py에 있고, 둘이 같은 값을 내는지 tools/testdata/path_follower_golden.json으로 대조한다.
    /// - 선분 i = 점 i → 점 (i+1) % n, 길이는 평면 근사(경도 환산에 선분 시작 위도 사용)
    /// - 선분 안은 위도·경도를 선형 보간, 방위는 선분의 atan2(east, north)
    /// </summary>
    public sealed class PathFollower
    {
        const double EarthRadiusM = 6378137.0;
        const double DegToRad = Math.PI / 180.0;
        const double RadToDeg = 180.0 / Math.PI;

        readonly double[] latitudes;
        readonly double[] longitudes;
        readonly double[] cumulative; // cumulative[i] = 선분 i 시작까지 누적 길이, 마지막은 전체 길이
        readonly double[] northM;
        readonly double[] eastM;

        public double TotalLengthM { get; }

        /// <param name="points">(위도, 경도) 점 목록. 2개 이상이고 전체 길이가 0보다 커야 한다.</param>
        public PathFollower(IReadOnlyList<(double latitude, double longitude)> points)
        {
            if (points == null || points.Count < 2)
                throw new ArgumentException("경로에는 점이 2개 이상 필요합니다.", nameof(points));

            int n = points.Count;
            latitudes = new double[n];
            longitudes = new double[n];
            for (int i = 0; i < n; i++)
            {
                latitudes[i] = points[i].latitude;
                longitudes[i] = points[i].longitude;
            }

            cumulative = new double[n + 1];
            northM = new double[n];
            eastM = new double[n];
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                northM[i] = (latitudes[next] - latitudes[i]) * DegToRad * EarthRadiusM;
                eastM[i] = (longitudes[next] - longitudes[i]) * DegToRad * EarthRadiusM
                           * Math.Cos(latitudes[i] * DegToRad);
                cumulative[i + 1] = cumulative[i] + Math.Sqrt(northM[i] * northM[i] + eastM[i] * eastM[i]);
            }

            TotalLengthM = cumulative[n];
            if (!(TotalLengthM > 0.0))
                throw new ArgumentException("경로의 전체 길이가 0입니다.", nameof(points));
        }

        /// <summary>시작점에서 이동한 거리(순환)에 해당하는 위치와 방위(0~360도, 북 0 시계방향)를 구한다.</summary>
        public void Evaluate(double distanceM, out double latitude, out double longitude, out double headingDeg)
        {
            double s = distanceM % TotalLengthM;
            if (s < 0.0)
                s += TotalLengthM;

            int i = FindSegment(s);
            int next = (i + 1) % latitudes.Length;
            double f = (s - cumulative[i]) / (cumulative[i + 1] - cumulative[i]);
            latitude = latitudes[i] + f * (latitudes[next] - latitudes[i]);
            longitude = longitudes[i] + f * (longitudes[next] - longitudes[i]);
            headingDeg = (Math.Atan2(eastM[i], northM[i]) * RadToDeg + 360.0) % 360.0;
        }

        // cumulative[i] <= s < cumulative[i+1]인 마지막 i. 길이 0인 선분은 자동으로 건너뛴다.
        int FindSegment(double s)
        {
            int lo = 0;
            int hi = latitudes.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= s)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }
    }
}
