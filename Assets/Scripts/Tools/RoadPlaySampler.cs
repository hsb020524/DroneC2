#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CesiumForUnity;
using DroneC2.Core;
using Unity.Mathematics;
using UnityEngine;

namespace DroneC2.Tools
{
    /// <summary>
    /// Play 중 유닛 뷰(가짜 GPS 또는 MAVLink → 바인더 → 변환기를 거친 실제 파이프라인의 결과)의 월드 위치를
    /// 일정 간격으로 기록하고 Cesium 지오레퍼런스로 위경도로 되돌려 Temp/road-play-라벨.csv로 저장하는 개발용 측정기.
    /// 파일 전체가 UNITY_EDITOR 전용이다. 시작은 RoadProbe.StartPlayMeasurement.
    /// </summary>
    public class RoadPlaySampler : MonoBehaviour
    {
        static readonly string[] UnitIds = { "TNK-01", "TNK-02", "UAV-01", "UAV-02", "UAV-03" };

        readonly StringBuilder csv = new StringBuilder("unit,timeSec,latitude,longitude,worldX,worldY,worldZ,viewYawDeg,reportedHeadingDeg,reportedSpeedMps\n");
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        string outPath;
        float interval;
        float duration;
        float startTime = -1f;
        float nextSample;
        UnitRegistry registry;
        CesiumGeoreference georeference;

        public static string DonePath(string label) => OutPath(label) + ".done";

        public static string OutPath(string label) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", $"road-play-{label}.csv"));

        public void Begin(string label, float durationSec, float intervalSec)
        {
            outPath = OutPath(label);
            duration = durationSec;
            interval = intervalSec;
            if (File.Exists(DonePath(label)))
                File.Delete(DonePath(label));
        }

        void Update()
        {
            if (registry == null)
                registry = FindAnyObjectByType<UnitRegistry>();
            if (georeference == null)
                georeference = FindAnyObjectByType<CesiumGeoreference>();
            if (registry == null || georeference == null)
                return;

            // 첫 샘플이 도착해 TNK-01 뷰가 생긴 뒤부터 시간을 센다.
            if (startTime < 0f)
            {
                if (!registry.TryGetView("TNK-01", out _))
                    return;
                startTime = Time.time;
                nextSample = startTime;
            }

            if (Time.time < nextSample)
                return;
            nextSample += interval;
            // 에디터가 멈췄다 돌아온 뒤 밀린 샘플을 한 프레임에 몰아서 기록하지 않는다(시간 간격이 0에 가까운 샘플 방지).
            if (nextSample <= Time.time)
                nextSample = Time.time + interval;

            float t = Time.time - startTime;
            foreach (string id in UnitIds)
            {
                if (!registry.TryGetView(id, out Transform view) || view == null)
                    continue;

                Vector3 p = view.position;
                double reportedHeading = double.NaN, reportedSpeed = double.NaN;
                foreach (UnitState us in registry.Store.Units)
                {
                    if (us.Id != id)
                        continue;
                    reportedHeading = us.HeadingDeg;
                    reportedSpeed = us.SpeedMps;
                    break;
                }
                double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(p.x, p.y, p.z));
                double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);
                csv.Append(id).Append(',')
                    .Append(t.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                    .Append(llh.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(llh.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(p.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                    .Append(p.y.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                    .Append(p.z.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                    .Append(view.eulerAngles.y.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                    .Append(reportedHeading.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                    .Append(reportedSpeed.ToString("F2", CultureInfo.InvariantCulture)).Append('\n');
                counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            }

            if (t >= duration)
                Finish();
        }

        void Finish()
        {
            File.WriteAllText(outPath, csv.ToString());
            var summary = new StringBuilder($"완료 {duration:F0}초, 간격 {interval:F2}초");
            foreach (var kv in counts)
                summary.Append($" | {kv.Key} {kv.Value}개");
            File.WriteAllText(outPath + ".done", summary.ToString());
            Destroy(gameObject);
        }
    }
}
#endif
