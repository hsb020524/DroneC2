using System;
using System.Collections.Generic;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>
    /// 검증된 MAVLink 프레임을 <see cref="TelemetrySample"/>로 바꾼다. 메인 스레드에서만 호출한다.
    /// - 테이블에 없는 sysid는 무시하고 <see cref="UnknownSysid"/>만 센다.
    /// - 유닛 종류는 테이블을 따른다. HEARTBEAT의 MAV_TYPE이 다르면 sysid마다 경고를 한 번만 낸다.
    /// - 고도의 의미만 선언한다. 드론은 relative_alt를 그대로 싣고 기준을 <see cref="AltitudeMode.HomeRelative"/>,
    ///   홈 위경도는 테이블 값으로 채운다. 지형 높이 조회는 하지 않는다(바인더가 한다).
    ///   전차는 <see cref="AltitudeMode.ClampToGround"/>로 선언한다.
    /// </summary>
    public sealed class MavlinkTelemetryRouter
    {
        sealed class Link
        {
            public ReportedStatus Status = ReportedStatus.Normal;
            public double LastHeadingDeg;
            public bool KindWarned;
        }

        const double MinSpeedForHeadingMps = 0.5;

        readonly IMavlinkUnitLookup units;
        readonly Action<string> warn;
        readonly Dictionary<byte, Link> links = new Dictionary<byte, Link>();

        public MavlinkTelemetryRouter(IMavlinkUnitLookup units, Action<string> warn)
        {
            this.units = units ?? throw new ArgumentNullException(nameof(units));
            this.warn = warn;
        }

        public event Action<byte, TelemetrySample> SampleProduced;

        /// <summary>테이블에 없는 sysid의 프레임 수.</summary>
        public long UnknownSysid { get; private set; }

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

            double speed = pos.GroundSpeedMps;
            double heading;
            if (pos.HeadingKnown)
                heading = pos.HeadingDeg;
            else if (speed >= MinSpeedForHeadingMps)
                heading = (Math.Atan2(pos.EastSpeedMps, pos.NorthSpeedMps) * 180.0 / Math.PI + 360.0) % 360.0;
            else
                heading = link.LastHeadingDeg;
            link.LastHeadingDeg = heading;

            TelemetrySample sample;
            if (entry.kind == UnitKind.Drone)
            {
                sample = new TelemetrySample(entry.unitId, pos.LatitudeDeg, pos.LongitudeDeg,
                    pos.RelativeAltitudeM, AltitudeMode.HomeRelative, heading, speed, now,
                    entry.kind, link.Status, entry.homeLatitude, entry.homeLongitude);
            }
            else
            {
                // 지상 유닛은 지표에 붙인다. 고도 값은 쓰이지 않는다.
                sample = new TelemetrySample(entry.unitId, pos.LatitudeDeg, pos.LongitudeDeg,
                    0.0, AltitudeMode.ClampToGround, heading, speed, now, entry.kind, link.Status);
            }

            SamplesProduced++;
            SampleProduced?.Invoke(frame.SystemId, sample);
        }
    }
}
