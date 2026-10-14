using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneC2.DataSource.Mavlink
{
    /// <summary>MAVLink sysid 하나에 대응하는 유닛 설정. 모든 값은 가상이다.</summary>
    [Serializable]
    public struct MavlinkUnitEntry
    {
        public byte systemId;
        public string unitId;
        public UnitKind kind;

        [Tooltip("시뮬레이터·에미터의 홈 위치(WGS84). 이 지점의 지형 높이 + relative_alt를 절대 고도로 쓴다.")]
        public double homeLatitude;
        public double homeLongitude;
    }

    public interface IMavlinkUnitLookup
    {
        bool TryGet(byte systemId, out MavlinkUnitEntry entry);
    }

    /// <summary>
    /// sysid → 유닛 설정 테이블. 테이블에 없는 sysid는 표시하지 않는다.
    /// 홈 위치를 여기에 적는 이유: HOME_POSITION 요청은 송신이 필요하고,
    /// 첫 샘플을 홈으로 가정하면 비행 중에 접속했을 때 틀리기 때문이다.
    /// </summary>
    [CreateAssetMenu(fileName = "MavlinkUnits", menuName = "DroneC2/MAVLink Unit Table")]
    public class MavlinkUnitTable : ScriptableObject, IMavlinkUnitLookup
    {
        [SerializeField] List<MavlinkUnitEntry> entries = new List<MavlinkUnitEntry>();

        Dictionary<byte, MavlinkUnitEntry> bySystemId;

        public IReadOnlyList<MavlinkUnitEntry> Entries => entries;

        public void SetEntries(IEnumerable<MavlinkUnitEntry> newEntries)
        {
            entries = new List<MavlinkUnitEntry>(newEntries);
            bySystemId = null;
        }

        public bool TryGet(byte systemId, out MavlinkUnitEntry entry)
        {
            if (bySystemId == null)
            {
                bySystemId = new Dictionary<byte, MavlinkUnitEntry>();
                for (int i = 0; i < entries.Count; i++)
                    bySystemId[entries[i].systemId] = entries[i]; // 중복은 OnValidate가 경고한다. 뒤의 것이 이긴다.
            }
            return bySystemId.TryGetValue(systemId, out entry);
        }

        void OnValidate()
        {
            bySystemId = null;
            var systemIds = new HashSet<byte>();
            var unitIds = new HashSet<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                MavlinkUnitEntry e = entries[i];
                if (e.systemId == 0 || e.systemId > 250)
                    Debug.LogWarning($"MavlinkUnitTable[{i}]: sysid {e.systemId}는 1~250이어야 합니다.", this);
                if (!systemIds.Add(e.systemId))
                    Debug.LogWarning($"MavlinkUnitTable[{i}]: sysid {e.systemId}가 중복입니다.", this);
                if (string.IsNullOrEmpty(e.unitId))
                    Debug.LogWarning($"MavlinkUnitTable[{i}]: unitId가 비어 있습니다.", this);
                else if (!unitIds.Add(e.unitId))
                    Debug.LogWarning($"MavlinkUnitTable[{i}]: unitId '{e.unitId}'가 중복입니다.", this);
                if (e.homeLatitude < -90.0 || e.homeLatitude > 90.0 || e.homeLongitude < -180.0 || e.homeLongitude > 180.0)
                    Debug.LogWarning($"MavlinkUnitTable[{i}]: 홈 위경도 범위를 벗어났습니다.", this);
            }
        }
    }
}
