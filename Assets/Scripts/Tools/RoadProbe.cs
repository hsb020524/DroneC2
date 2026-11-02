#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CesiumForUnity;
using DroneC2.DataSource;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneC2.Tools
{
    /// <summary>
    /// 전차 경로의 지형 높이를 Cesium 지형에서 조회해 Temp/road-probe.csv로 저장하는 개발용 측정기(경사 계산용).
    /// 파일 전체가 UNITY_EDITOR 전용이라 플레이어 빌드에 포함되지 않는다.
    /// 실행(화면 없이): Unity.exe -batchmode -nographics -projectPath . -executeMethod DroneC2.Tools.RoadProbe.RunFromCommandLine
    /// 환경 변수 ROAD_PROBE_STEP(미터, 기본 10)이 경로를 따라 조회하는 간격이다.
    /// </summary>
    public static class RoadProbe
    {
        const string ScenePath = "Assets/Scenes/Stage3Control.unity";
        const string ScenarioPath = "Assets/Scenarios/osm-roads-tnk.json";
        const double TimeoutSec = 300.0;

        static readonly StringBuilder Report = new StringBuilder();

        static string TempPath(string file) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", file));

        public static void RunFromCommandLine()
        {
            try
            {
                Run();
            }
            catch (Exception e)
            {
                Report.AppendLine("예외: " + e);
                Finish(2);
            }
        }

        static void Run()
        {
            string stepText = Environment.GetEnvironmentVariable("ROAD_PROBE_STEP");
            double step = string.IsNullOrEmpty(stepText) ? 10.0 : double.Parse(stepText, CultureInfo.InvariantCulture);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ReportScenarioWiring();

            Scenario.TryParse(File.ReadAllText(ScenarioPath), out Scenario scenario, out string error);
            if (scenario == null)
            {
                Report.AppendLine("시나리오 파싱 실패: " + error);
                Finish(2);
                return;
            }

            var meta = new List<(string unit, double distance, double lat, double lon)>();
            foreach (ScenarioUnit unit in scenario.units)
            {
                var points = new List<(double, double)>();
                foreach (ScenarioPoint p in unit.waypoints)
                    points.Add((p.latitude, p.longitude));

                var follower = new PathFollower(points);
                double half = follower.TotalLengthM / 2.0; // 왕복 경로의 편도
                for (double d = 0.0; d <= half + 1e-9; d += step)
                {
                    follower.Evaluate(d, out double lat, out double lon, out _);
                    meta.Add((unit.id, d, lat, lon));
                }
            }

            Cesium3DTileset tileset = UnityEngine.Object.FindAnyObjectByType<Cesium3DTileset>();
            if (tileset == null)
            {
                Report.AppendLine("Cesium3DTileset을 씬에서 찾지 못했습니다.");
                Finish(2);
                return;
            }

            Report.AppendLine($"지형 조회 {meta.Count}점(간격 {step} m), 타일셋 '{tileset.name}'");
            var positions = new double3[meta.Count];
            for (int i = 0; i < meta.Count; i++)
                positions[i] = new double3(meta[i].lon, meta[i].lat, 0.0);

            var task = tileset.SampleHeightMostDetailed(positions);
            double start = EditorApplication.timeSinceStartup;
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!task.IsCompleted && EditorApplication.timeSinceStartup - start < TimeoutSec)
                    return;

                EditorApplication.update -= poll;
                double seconds = EditorApplication.timeSinceStartup - start;
                if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
                {
                    Report.AppendLine($"조회 실패: 완료={task.IsCompleted}, 실패={task.IsFaulted}, 취소={task.IsCanceled}, {seconds:F1}초");
                    Finish(3);
                    return;
                }

                CesiumSampleHeightResult result = task.Result;
                var csv = new StringBuilder("unit,distanceM,latitude,longitude,ellipsoidHeightM,success\n");
                int ok = 0;
                for (int i = 0; i < meta.Count; i++)
                {
                    bool success = result.sampleSuccess[i];
                    if (success)
                        ok++;
                    csv.Append(meta[i].unit).Append(',')
                        .Append(meta[i].distance.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(meta[i].lat.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(meta[i].lon.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(result.longitudeLatitudeHeightPositions[i].z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(success ? 1 : 0).Append('\n');
                }

                File.WriteAllText(TempPath("road-probe.csv"), csv.ToString());
                Report.AppendLine($"조회 완료 {seconds:F1}초, 성공 {ok}/{meta.Count}, 경고 {result.warnings?.Length ?? 0}건");
                if (result.warnings != null)
                    foreach (string w in result.warnings)
                        Report.AppendLine("  경고: " + w);
                Finish(ok == meta.Count ? 0 : 4);
            };
            EditorApplication.update += poll;
        }

        // 씬의 FakeGpsSource가 어떤 시나리오 파일을 가리키는지(유닛별) 기록한다.
        static void ReportScenarioWiring()
        {
            foreach (FakeGpsSource source in UnityEngine.Object.FindObjectsByType<FakeGpsSource>())
            {
                var so = new SerializedObject(source);
                string id = so.FindProperty("unitId").stringValue;
                UnityEngine.Object asset = so.FindProperty("scenarioJson").objectReferenceValue;
                Report.AppendLine($"연결: {id} -> {(asset != null ? asset.name : "(없음)")}");
            }
        }

        static void Finish(int exitCode)
        {
            File.WriteAllText(TempPath("road-probe.txt"), Report.ToString());
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
