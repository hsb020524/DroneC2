using System;
using System.Collections.Generic;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>
    /// 검증된 MAVLink 프레임을 <see cref="TelemetrySample"/>로 바꾼다. 메인 스레드에서만 호출한다.
    /// - 테이블에 없는 sysid는 무시하고 <see cref="UnknownSysid"/>만 센다.
    /// - 유닛 종류는 테이블을 따른다. HEARTBEAT의 MAV_TYPE이 다르면 sysid마다 경고를 한 번만 낸다.
    /// - 드론의 고도는 (홈 지형 높이 + relative_alt)이다. 홈 지형 높이는 sysid당 한 번만 조회하고,
    ///   얻기 전의 위치 샘플은 보류(버림)한다. 스트림이 계속 오므로 대기열에 쌓지 않는다.
    /// </summary>
    public sealed class MavlinkTelemetryRouter
    {
        /// <summary>홈 위치의 지형 높이(타원체고, m) 조회. 아직 얻지 못하면 false.</summary>
        public delegate bool HomeHeightLookup(double latitude, double longitude, out double heightM);

        sealed class Link
        {
            public ReportedStatus Status = ReportedStatus.Normal;
            public double LastHeadingDeg;
            public bool HasHomeHeight;
            public double HomeHeightM;
            public bool KindWarned;
        }

        const double MinSpeedForHeadingMps = 0.5;

        readonly IMavlinkUnitLookup units;
        readonly HomeHeightLookup homeHeight;
        readonly Action<string> warn;
        readonly Dictionary<byte, Link> links = new Dictionary<byte, Link>();

        /// <param name="homeHeight">null이면 평면(높이 0)으로 간주한다. (AltitudeResolver와 같은 규칙)</param>
        public MavlinkTelemetryRouter(IMavlinkUnitLookup units, HomeHeightLookup homeHeight, Action<string> warn)
        {
            this.units = units ?? throw new ArgumentNullException(nameof(units));
            this.homeHeight = homeHeight;
            this.warn = warn;
        }

        public event Action<byte, TelemetrySample> SampleProduced;

        /// <summary>테이블에 없는 sysid의 프레임 수.</summary>
        public long UnknownSysid { get; private set; }

        /// <summary>홈 지형 높이를 얻기 전이라 보류한 위치 샘플 수.</summary>
        public long HeldForHome { get; private set; }

        /// <summary>샘플로 내보낸 수.</summary>
        public long SamplesProduced { get; private set; }

        public void Handle(in MavlinkFrame frame, double now)
        {
            if (!units.TryGet(frame.SystemId, out MavlinkUnitEntry entry))
            {
                UnknownSysid++;
                return;
            }

            if (!links.TryGetValue(frame.SystemId, out Link link))
            {
                link = new Link();
                links.Add(frame.SystemId, link);
            }

            if (frame.MessageId == MavlinkParser.MsgHeartbeat)
                HandleHeartbeat(frame, entry, link);
            else if (frame.MessageId == MavlinkParser.MsgGlobalPositionInt)
                HandlePosition(frame, entry, link, now);
        }

        void HandleHeartbeat(in MavlinkFrame frame, MavlinkUnitEntry entry, Link link)
        {
            if (!MavlinkDecoder.TryDecodeHeartbeat(frame, out MavlinkHeartbeat hb))
                return;

            link.Status = hb.IsWarning ? ReportedStatus.Warning : ReportedStatus.Normal;

            bool mapped = hb.TryGetKind(out UnitKind kind);
            if (!link.KindWarned && (!mapped || kind != entry.kind))
            {
                link.KindWarned = true;
                warn?.Invoke($"MAVLink sysid {frame.SystemId}: HEARTBEAT의 MAV_TYPE {hb.MavType}이(가) "
                    + $"테이블의 유닛 종류 {entry.kind}와 다릅니다. 테이블을 따릅니다.");
            }
        }

        void HandlePosition(in MavlinkFrame frame, MavlinkUnitEntry entry, Link link, double now)
        {
            if (!MavlinkDecoder.TryDecodeGlobalPosition(frame, out MavlinkGlobalPosition pos))
                return;

            double altitudeM = 0.0; // 지상 유닛은 ClampToGround가 높이를 정하므로 쓰지 않는다.
            if (entry.kind == UnitKind.Drone)
            {
                if (!link.HasHomeHeight)
                {
                    double h = 0.0;
                    if (homeHeight != null && !homeHeight(entry.homeLatitude, entry.homeLongitude, out h))
                    {
                        HeldForHome++;
                        return;
                    }
                    link.HomeHeightM = h;
                    link.HasHomeHeight = true;
                }
                altitudeM = link.HomeHeightM + pos.RelativeAltitudeM;
            }

            double speed = pos.GroundSpeedMps;
            double heading;
            if (pos.HeadingKnown)
                heading = pos.HeadingDeg;
            else if (speed >= MinSpeedForHeadingMps)
                heading = (Math.Atan2(pos.EastSpeedMps, pos.NorthSpeedMps) * 180.0 / Math.PI + 360.0) % 360.0;
            else
                heading = link.LastHeadingDeg;
            link.LastHeadingDeg = heading;

            var sample = new TelemetrySample(entry.unitId, pos.LatitudeDeg, pos.LongitudeDeg, altitudeM,
                AltitudeMode.Absolute, heading, speed, now, entry.kind, link.Status);
            SamplesProduced++;
            SampleProduced?.Invoke(frame.SystemId, sample);
        }
    }
}
