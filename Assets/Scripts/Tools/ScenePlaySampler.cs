#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CesiumForUnity;
using DroneC2.Coordinate;
using DroneC2.Core;
using DroneC2.DataSource;
using DroneC2.View;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace DroneC2.Tools
{
    /// <summary>
    /// 씬의 모든 FakeGpsSource 유닛(뷰)의 월드 위치를 일정 간격으로 Temp/scene-play-라벨.csv에 기록하는 개발용 측정기.
    /// Cesium 지오레퍼런스와 지표 높이 샘플러가 있으면 위경도·타원체고·지표 높이(샘플러)도 함께 기록한다.
    /// 웨이포인트의 월드 위치는 같은 파일에 WP 행으로 남긴다. 파일 전체가 UNITY_EDITOR 전용이다.
    /// 시작: ScenePlaySampler.Start("라벨", 초)  (RoadProbe와 별개로 Play 중에 호출)
    /// </summary>
    public class ScenePlaySampler : MonoBehaviour
    {
        class Tracked
        {
            public string unitId;
            public Transform view;
        }

        readonly StringBuilder csv = new StringBuilder("kind,unit,timeSec,worldX,worldY,worldZ,latitude,longitude,ellipsoidHeightM,groundHeightM\n");
        readonly List<Tracked> tracked = new List<Tracked>();
        string path;
        float interval;
        float duration;
        float startTime;
        float nextSample;
        CesiumGeoreference georeference;
        ITerrainHeightSampler sampler;

        public static string OutPath(string label) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", $"scene-play-{label}.csv"));

        public static void Start(string label, float durationSec, float intervalSec = 0.1f)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("ScenePlaySampler: Play 모드에서만 측정할 수 있습니다.");
                return;
            }

            var go = new GameObject("ScenePlaySampler (temp)") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<ScenePlaySampler>().Begin(label, durationSec, intervalSec);
        }

        void Begin(string label, float durationSec, float intervalSec)
        {
            path = OutPath(label);
            duration = durationSec;
            interval = intervalSec;
            if (File.Exists(path + ".done"))
                File.Delete(path + ".done");

            georeference = FindAnyObjectByType<CesiumGeoreference>();
            foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>())
                if (b is ITerrainHeightSampler s) { sampler = s; break; }
            ICoordinateConverter converter = null;
            foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>())
                if (b is ICoordinateConverter c) { converter = c; break; }

            foreach (FakeGpsSource source in FindObjectsByType<FakeGpsSource>())
            {
                var so = new SerializedObject(source);
                string id = so.FindProperty("unitId").stringValue;
                // 뷰는 같은 오브젝트(프리팹 유닛)나 같은 오브젝트의 바인더가 가리키는 오브젝트(단일 유닛 씬)에 있다.
                DroneView view = source.GetComponentInParent<DroneView>();
                if (view == null)
                {
                    UnitTelemetryBinder binder = source.GetComponent<UnitTelemetryBinder>();
                    if (binder != null)
                        view = new SerializedObject(binder).FindProperty("view").objectReferenceValue as DroneView;
                }

                if (view == null)
                {
                    Debug.LogWarning($"ScenePlaySampler: {id}의 뷰를 찾지 못했습니다.");
                    continue;
                }

                tracked.Add(new Tracked { unitId = id, view = view.transform });

                SerializedProperty waypoints = so.FindProperty("waypoints");
                for (int i = 0; converter != null && i < waypoints.arraySize; i++)
                {
                    SerializedProperty w = waypoints.GetArrayElementAtIndex(i);
                    Vector3 world = converter.ToWorld(
                        w.FindPropertyRelative("latitude").doubleValue,
                        w.FindPropertyRelative("longitude").doubleValue, 0.0);
                    csv.Append("WP,").Append(id).Append(',').Append(i).Append(',')
                        .Append(F(world.x)).Append(',').Append(F(world.y)).Append(',').Append(F(world.z))
                        .Append(",,,,\n");
                }
            }

            startTime = Time.time;
            nextSample = startTime;
        }

        static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        void Update()
        {
            if (Time.time < nextSample)
                return;
            nextSample += interval;
            float t = Time.time - startTime;

            foreach (Tracked u in tracked)
            {
                if (u.view == null)
                    continue;
                Vector3 p = u.view.position;
                string lat = "", lon = "", h = "", ground = "";
                if (georeference != null)
                {
                    double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(
                        georeference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(p.x, p.y, p.z)));
                    lat = F(llh.y);
                    lon = F(llh.x);
                    h = F(llh.z);
                    if (sampler != null && sampler.TryGetGroundHeight(llh.y, llh.x, out double g))
                        ground = F(g);
                }

                csv.Append("P,").Append(u.unitId).Append(',').Append(t.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                    .Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',')
                    .Append(lat).Append(',').Append(lon).Append(',').Append(h).Append(',').Append(ground).Append('\n');
            }

            if (t >= duration)
            {
                File.WriteAllText(path, csv.ToString());
                File.WriteAllText(path + ".done", $"완료 {duration:F0}초, 유닛 {tracked.Count}기");
                Destroy(gameObject);
            }
        }
    }
}
#endif
