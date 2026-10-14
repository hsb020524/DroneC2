using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneC2.DataSource
{
    /// <summary>웨이포인트 한 점. 위경도(WGS84, 도)만 가진다.</summary>
    [Serializable]
    public class ScenarioPoint
    {
        public double latitude;
        public double longitude;
    }

    /// <summary>결함 구간. 시각은 소스 시작 후 경과 시간(초). 지속 시간이 0이면 쓰지 않는다.</summary>
    [Serializable]
    public class ScenarioFault
    {
        public float startSec;
        public float durationSec;
    }

    /// <summary>유닛 한 기의 시나리오. <c>kind</c>는 <see cref="UnitKind"/> 이름(Drone, Tank)이다.</summary>
    [Serializable]
    public class ScenarioUnit
    {
        public string id;
        public string kind = "Drone";
        public double altitudeM;

        /// <summary>
        /// 고도 기준(<see cref="AltitudeMode"/> 이름: Absolute, AboveGround, ClampToGround).
        /// 비워 두면 종류로 정한다(드론 AboveGround, 전차 ClampToGround). HomeRelative는 홈을 모르므로 쓸 수 없다.
        /// </summary>
        public string altitudeMode = "";
        public float speedMps = 10f;
        public List<ScenarioPoint> waypoints = new List<ScenarioPoint>();
        public ScenarioFault warning = new ScenarioFault();
        public ScenarioFault dropout = new ScenarioFault();

        public UnitKind Kind => Enum.TryParse(kind, out UnitKind parsed) ? parsed : UnitKind.Drone;

        public AltitudeMode AltitudeReference
        {
            get
            {
                if (!string.IsNullOrEmpty(altitudeMode) && Enum.TryParse(altitudeMode, out AltitudeMode parsed))
                    return parsed;
                return Kind == UnitKind.Tank ? AltitudeMode.ClampToGround : AltitudeMode.AboveGround;
            }
        }
    }

    /// <summary>
    /// 웨이포인트 기반 시나리오 파일(JSON). 같은 파일이면 같은 이동을 재현한다. (NFR-004)
    /// 위경도·고도·속도와 고도 기준 이름만 담으며 Unity 좌표는 알지 못한다. 고도 기준은 소스가 샘플에 실어 보낸다.
    /// </summary>
    [Serializable]
    public class Scenario
    {
        public int version = 1;
        public string name;
        public List<ScenarioUnit> units = new List<ScenarioUnit>();

        /// <summary>JSON을 읽고 검증한다. 실패하면 false와 사유를 돌려준다.</summary>
        public static bool TryParse(string json, out Scenario scenario, out string error)
        {
            scenario = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "시나리오 JSON이 비어 있습니다.";
                return false;
            }

            Scenario parsed;
            try
            {
                parsed = JsonUtility.FromJson<Scenario>(json);
            }
            catch (ArgumentException e)
            {
                error = $"시나리오 JSON을 읽을 수 없습니다: {e.Message}";
                return false;
            }

            if (parsed == null || parsed.units == null || parsed.units.Count == 0)
            {
                error = "시나리오에 유닛이 없습니다.";
                return false;
            }

            var ids = new HashSet<string>();
            foreach (ScenarioUnit unit in parsed.units)
            {
                if (!Validate(unit, ids, out error))
                    return false;
            }

            scenario = parsed;
            error = null;
            return true;
        }

        public bool TryGetUnit(string id, out ScenarioUnit unit)
        {
            foreach (ScenarioUnit candidate in units)
            {
                if (candidate.id == id)
                {
                    unit = candidate;
                    return true;
                }
            }

            unit = null;
            return false;
        }

        static bool Validate(ScenarioUnit unit, HashSet<string> ids, out string error)
        {
            if (unit == null || string.IsNullOrEmpty(unit.id))
            {
                error = "유닛 id가 비어 있습니다.";
                return false;
            }

            if (!ids.Add(unit.id))
            {
                error = $"유닛 id가 중복되었습니다: {unit.id}";
                return false;
            }

            if (!Enum.TryParse(unit.kind, out UnitKind _))
            {
                error = $"{unit.id}: 알 수 없는 종류 '{unit.kind}' (Drone, Tank)";
                return false;
            }

            if (!string.IsNullOrEmpty(unit.altitudeMode)
                && (!Enum.TryParse(unit.altitudeMode, out AltitudeMode mode)
                    || !Enum.IsDefined(typeof(AltitudeMode), mode)
                    || mode == AltitudeMode.HomeRelative))
            {
                error = $"{unit.id}: 알 수 없는 고도 모드 '{unit.altitudeMode}' (Absolute, AboveGround, ClampToGround)";
                return false;
            }

            if (!(unit.speedMps > 0f))
            {
                error = $"{unit.id}: 속도는 0보다 커야 합니다.";
                return false;
            }

            if (unit.waypoints == null || unit.waypoints.Count < 2)
            {
                error = $"{unit.id}: 웨이포인트가 2개 이상 필요합니다.";
                return false;
            }

            foreach (ScenarioPoint point in unit.waypoints)
            {
                if (point == null || point.latitude < -90.0 || point.latitude > 90.0
                    || point.longitude < -180.0 || point.longitude > 180.0)
                {
                    error = $"{unit.id}: 위경도가 범위를 벗어났습니다.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
