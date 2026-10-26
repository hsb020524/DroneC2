using System;
using System.Collections.Generic;
using DroneC2.DataSource;
using UnityEngine;

namespace DroneC2.Core
{
    /// <summary>유닛 한 기의 최신 상태. 값 갱신은 <see cref="UnitStateStore"/>만 한다.</summary>
    public sealed class UnitState
    {
        public string Id { get; }
        public UnitKind Kind { get; internal set; }

        /// <summary>표시용 상태. 샘플이 끊기면 Lost, 아니면 소스가 보고한 상태.</summary>
        public UnitStatus Status { get; internal set; }

        public double Latitude { get; internal set; }
        public double Longitude { get; internal set; }

        /// <summary>샘플이 보고한 고도(m). 의미는 유닛의 고도 모드를 따른다.</summary>
        public double AltitudeM { get; internal set; }

        /// <summary>샘플 고도를 해석하는 방식. UI가 고도 값의 의미를 표시하는 데 쓴다.</summary>
        public AltitudeMode AltitudeMode { get; internal set; }

        public double SpeedMps { get; internal set; }
        public double HeadingDeg { get; internal set; }

        /// <summary>변환기가 계산한 월드 좌표. 카메라 포커스 같은 표시 용도.</summary>
        public Vector3 WorldPosition { get; internal set; }

        /// <summary>마지막 샘플을 수신한 시각(수신 측 시계, 초).</summary>
        public double LastReceivedTime { get; internal set; }

        internal ReportedStatus ReportedStatus;

        internal UnitState(string id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// 유닛 상태 저장소. 샘플 수신, 통신 두절 판정, 선택 상태를 담당하는 순수 로직이며
    /// 시간은 호출자가 넘긴다. (MonoBehaviour 없이 테스트 가능)
    /// </summary>
    public sealed class UnitStateStore
    {
        readonly List<UnitState> units = new List<UnitState>();
        readonly Dictionary<string, UnitState> byId = new Dictionary<string, UnitState>();

        public UnitStateStore(double lostTimeoutSec = 2.0)
        {
            LostTimeoutSec = lostTimeoutSec;
        }

        /// <summary>마지막 샘플 이후 이 시간(초)이 지나면 Lost로 표시한다.</summary>
        public double LostTimeoutSec { get; set; }

        public IReadOnlyList<UnitState> Units => units;
        public string SelectedId { get; private set; }

        /// <summary>선택과 별개로 카메라 패널에 고정한 유닛. 고정은 최대 1기(패널 2개 = 선택 + 고정).</summary>
        public string PinnedId { get; private set; }

        public UnitState Selected =>
            SelectedId != null && byId.TryGetValue(SelectedId, out UnitState state) ? state : null;

        /// <summary>새 유닛이 처음 보고되었을 때.</summary>
        public event Action<UnitState> UnitAdded;

        /// <summary>샘플을 받아 값이 갱신될 때마다.</summary>
        public event Action<UnitState> UnitUpdated;

        /// <summary>표시용 상태(Normal/Warning/Lost)가 바뀌었을 때.</summary>
        public event Action<UnitState> StatusChanged;

        /// <summary>선택이 바뀌었을 때. 선택 해제는 null.</summary>
        public event Action<string> SelectionChanged;

        /// <summary>고정이 바뀌었을 때. 고정 해제는 null.</summary>
        public event Action<string> PinnedChanged;

        /// <summary>모든 유닛이 비워졌을 때(<see cref="Clear"/>).</summary>
        public event Action Cleared;

        public bool TryGet(string id, out UnitState state)
        {
            if (id == null)
            {
                state = null;
                return false;
            }
            return byId.TryGetValue(id, out state);
        }

        public void Report(in TelemetrySample sample, Vector3 worldPosition, double now)
        {
            if (string.IsNullOrEmpty(sample.UnitId))
                return;

            bool added = !byId.TryGetValue(sample.UnitId, out UnitState state);
            UnitStatus previousStatus = UnitStatus.Normal;
            if (added)
            {
                state = new UnitState(sample.UnitId);
                byId.Add(sample.UnitId, state);
                units.Add(state);
            }
            else
            {
                previousStatus = state.Status;
            }

            state.Kind = sample.Kind;
            state.ReportedStatus = sample.Status;
            state.Status = ToUnitStatus(sample.Status);
            state.Latitude = sample.Latitude;
            state.Longitude = sample.Longitude;
            state.AltitudeM = sample.AltitudeM;
            state.AltitudeMode = sample.AltitudeReference;
            state.SpeedMps = sample.SpeedMps;
            state.HeadingDeg = sample.HeadingDeg;
            state.WorldPosition = worldPosition;
            state.LastReceivedTime = now;

            if (added)
                UnitAdded?.Invoke(state);
            else if (previousStatus != state.Status)
                StatusChanged?.Invoke(state);

            UnitUpdated?.Invoke(state);
        }

        // 소스 보고 상태에는 Lost가 없다. Lost는 Tick만 붙인다.
        static UnitStatus ToUnitStatus(ReportedStatus reported)
        {
            return reported == ReportedStatus.Warning ? UnitStatus.Warning : UnitStatus.Normal;
        }

        /// <summary>시간 경과에 따른 두절 판정. 매 프레임 호출해도 된다.</summary>
        public void Tick(double now)
        {
            for (int i = 0; i < units.Count; i++)
            {
                UnitState state = units[i];
                if (state.Status == UnitStatus.Lost)
                    continue;

                if (now - state.LastReceivedTime > LostTimeoutSec)
                {
                    state.Status = UnitStatus.Lost;
                    StatusChanged?.Invoke(state);
                }
            }
        }

        /// <summary>등록된 유닛만 선택할 수 있다. 없는 id면 false를 돌려주고 선택은 유지된다.</summary>
        public bool Select(string id)
        {
            if (id == null || !byId.ContainsKey(id))
                return false;

            if (SelectedId != id)
            {
                SelectedId = id;
                SelectionChanged?.Invoke(id);
            }
            return true;
        }

        /// <summary>등록된 유닛만 고정할 수 있다. 이미 고정된 유닛이 있으면 교체한다.</summary>
        public bool Pin(string id)
        {
            if (id == null || !byId.ContainsKey(id))
                return false;

            if (PinnedId != id)
            {
                PinnedId = id;
                PinnedChanged?.Invoke(id);
            }
            return true;
        }

        public void Unpin()
        {
            if (PinnedId == null)
                return;

            PinnedId = null;
            PinnedChanged?.Invoke(null);
        }

        /// <summary>
        /// 모든 유닛과 선택·고정을 비운다(소스 전환). 선택·고정이 있었다면 해제 이벤트를 먼저 내고
        /// 마지막에 <see cref="Cleared"/>를 낸다. 두절 판정 시간은 유지한다.
        /// </summary>
        public void Clear()
        {
            bool hadSelection = SelectedId != null;
            bool hadPin = PinnedId != null;

            SelectedId = null;
            PinnedId = null;
            units.Clear();
            byId.Clear();

            if (hadSelection)
                SelectionChanged?.Invoke(null);
            if (hadPin)
                PinnedChanged?.Invoke(null);
            Cleared?.Invoke();
        }

        public void ClearSelection()
        {
            if (SelectedId == null)
                return;

            SelectedId = null;
            SelectionChanged?.Invoke(null);
        }
    }
}
