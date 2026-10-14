#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CesiumForUnity;
using UnityEngine;

namespace DroneC2.Tools
{
    /// <summary>
    /// 프레임 시간과 Cesium 타일 부담을 일정 시간 샘플링하는 개발용 측정기.
    /// 파일 전체가 UNITY_EDITOR 전용이라 플레이어 빌드에 포함되지 않는다. 시작은 PerfProbe(Editor)에서 한다.
    /// </summary>
    public class PerfProbeRunner : MonoBehaviour
    {
        readonly List<float> frameMs = new List<float>();
        string label;
        string outPath;
        float warmupEnd;
        float end;

        public void Begin(string label, float warmupSec, float durationSec, string outPath)
        {
            this.label = label;
            this.outPath = outPath;
            warmupEnd = Time.unscaledTime + warmupSec;
            end = warmupEnd + durationSec;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now >= warmupEnd && now < end)
            {
                frameMs.Add(Time.unscaledDeltaTime * 1000f);
                return;
            }

            if (now < end)
                return;

            Finish();
        }

        void Finish()
        {
            frameMs.Sort();
            int n = frameMs.Count;
            double sum = 0;
            foreach (float v in frameMs)
                sum += v;

            double avg = n > 0 ? sum / n : 0;
            float p95 = n > 0 ? frameMs[Mathf.Min(n - 1, (int)(n * 0.95f))] : 0f;
            float max = n > 0 ? frameMs[n - 1] : 0f;

            int tileRenderers = 0;
            int tilesets = 0;
            double progress = 0;
            foreach (Cesium3DTileset tileset in FindObjectsByType<Cesium3DTileset>(FindObjectsSortMode.None))
            {
                tilesets++;
                progress += tileset.ComputeLoadProgress();
                foreach (MeshRenderer r in tileset.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (r.enabled)
                        tileRenderers++;
                }
            }

            var inv = CultureInfo.InvariantCulture;
            string line = string.Format(inv,
                "{0}\tframes={1}\tavgMs={2:F2}\tfps={3:F0}\tp95Ms={4:F2}\tmaxMs={5:F2}\tactiveTileRenderers={6}\ttilesets={7}\tloadProgress={8:F0}\tmonoMB={9:F0}",
                label, n, avg, avg > 0 ? 1000.0 / avg : 0, p95, max, tileRenderers, tilesets,
                tilesets > 0 ? progress / tilesets : 0, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576.0);
            File.AppendAllText(outPath, line + "\n");
            Destroy(gameObject);
        }
    }
}
#endif
