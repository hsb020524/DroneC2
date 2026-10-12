using DroneC2.Coordinate;

namespace DroneC2.Core
{
    /// <summary>
    /// 고도 모드에 따라 변환기에 넘길 최종 고도를 계산한다.
    /// 지표 높이가 아직 없으면 마지막 유효 값을 쓰고, 한 번도 얻지 못했으면 준비되지 않은 것으로 본다.
    /// 샘플러가 없으면(평면 변환기) 지표를 높이 0의 평면으로 간주한다.
    /// </summary>
    public sealed class AltitudeResolver
    {
        readonly ITerrainHeightSampler sampler;
        double lastGroundM;
        bool hasLastGround;

        public AltitudeResolver(ITerrainHeightSampler sampler)
        {
            this.sampler = sampler;
        }

        public bool TryResolve(AltitudeMode mode, double latitude, double longitude,
            double reportedAltitudeM, out double altitudeM)
        {
            altitudeM = reportedAltitudeM;
            if (mode == AltitudeMode.Absolute)
                return true;

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
    }
}
