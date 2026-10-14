using DroneC2.Coordinate;
using DroneC2.DataSource;

namespace DroneC2.Core
{
    /// <summary>
    /// 고도 모드에 따라 변환기에 넘길 최종 고도를 계산한다.
    /// 지표 높이가 아직 없으면 마지막 유효 값을 쓰고, 한 번도 얻지 못했으면 준비되지 않은 것으로 본다.
    /// 샘플러가 없으면(평면 변환기) 지표를 높이 0의 평면으로 간주한다.
    /// HomeRelative의 홈 지형 높이는 홈 위경도가 바뀔 때만 다시 조회한다.
    /// </summary>
    public sealed class AltitudeResolver
    {
        readonly ITerrainHeightSampler sampler;
        double lastGroundM;
        bool hasLastGround;

        double cachedHomeLat;
        double cachedHomeLon;
        double cachedHomeGroundM;
        bool hasCachedHome;

        public AltitudeResolver(ITerrainHeightSampler sampler)
        {
            this.sampler = sampler;
        }

        /// <summary>홈 지형 높이를 실제로 조회한 횟수(캐시 동작 확인용).</summary>
        public int HomeLookups { get; private set; }

        /// <summary>홈 위경도가 필요 없는 모드용. HomeRelative를 넘기면 false를 반환한다.</summary>
        public bool TryResolve(AltitudeMode mode, double latitude, double longitude,
            double reportedAltitudeM, out double altitudeM)
        {
            return TryResolve(mode, latitude, longitude, reportedAltitudeM,
                double.NaN, double.NaN, out altitudeM);
        }

        public bool TryResolve(AltitudeMode mode, double latitude, double longitude,
            double reportedAltitudeM, double homeLatitude, double homeLongitude, out double altitudeM)
        {
            altitudeM = reportedAltitudeM;
            if (mode == AltitudeMode.Absolute)
                return true;

            if (mode == AltitudeMode.HomeRelative)
            {
                if (double.IsNaN(homeLatitude) || double.IsNaN(homeLongitude))
                    return false;
                if (!TryGetHomeGround(homeLatitude, homeLongitude, out double homeGround))
                    return false;
                altitudeM = homeGround + reportedAltitudeM;
                return true;
            }

            double ground;
            if (sampler == null)
            {
                ground = 0.0;
            }
            else if (sampler.TryGetGroundHeight(latitude, longitude, out double sampled))
            {
                lastGroundM = sampled;
                hasLastGround = true;
                ground = sampled;
            }
            else if (hasLastGround)
            {
                ground = lastGroundM;
            }
            else
            {
                return false;
            }

            altitudeM = mode == AltitudeMode.ClampToGround ? ground : ground + reportedAltitudeM;
            return true;
        }

        bool TryGetHomeGround(double homeLatitude, double homeLongitude, out double groundM)
        {
            if (hasCachedHome && cachedHomeLat == homeLatitude && cachedHomeLon == homeLongitude)
            {
                groundM = cachedHomeGroundM;
                return true;
            }

            // 홈이 바뀌었으면 이전 값을 쓰지 않는다. 새 홈의 지형 높이를 얻을 때까지 보류한다.
            hasCachedHome = false;
            if (sampler == null)
            {
                groundM = 0.0;
            }
            else
            {
                HomeLookups++;
                if (!sampler.TryGetGroundHeight(homeLatitude, homeLongitude, out groundM))
                    return false;
            }

            cachedHomeLat = homeLatitude;
            cachedHomeLon = homeLongitude;
            cachedHomeGroundM = groundM;
            hasCachedHome = true;
            return true;
        }
    }
}
