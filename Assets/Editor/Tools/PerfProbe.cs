using System.IO;
using DroneC2.Tools;
using UnityEngine;

namespace DroneC2.EditorTools
{
    /// <summary>
    /// Play 모드에서 프레임 시간·타일 렌더러 수를 샘플링해 Temp/perf.txt에 한 줄씩 덧붙이는 개발 도구.
    /// 사용 예: PerfProbe.Start("baseline", 5f, 20f);
    /// </summary>
    public static class PerfProbe
    {
        public static string OutputPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "perf.txt"));

        public static void Start(string label, float warmupSec = 5f, float durationSec = 20f)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("PerfProbe: Play 모드에서만 측정할 수 있습니다.");
                return;
            }

            var go = new GameObject("PerfProbe (temp)") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<PerfProbeRunner>().Begin(label, warmupSec, durationSec, OutputPath);
        }
    }
}
