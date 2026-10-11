using System.IO;
using UnityEditor;
using UnityEngine;

namespace DroneC2.EditorTools
{
    /// <summary>
    /// Play 모드의 Game 화면을 PNG 프레임으로 저장하는 개발 도구. GIF 조립은 tools/make_gif.py가 맡는다.
    /// Editor 폴더에 있어 플레이어 빌드에 포함되지 않으며, 녹화용 오브젝트는 씬에 저장되지 않는다.
    /// 사용법은 CLAUDE.md의 "개발 도구" 절을 참고한다.
    /// </summary>
    public static class GifFrameRecorder
    {
        public static string DefaultOutputDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "GifFrames"));

        [MenuItem("Tools/DroneC2/Record Game View (15s, 10fps)")]
        static void RecordDefault()
        {
            StartRecording(15f, 10f);
        }

        /// <param name="durationSec">녹화 길이(초)</param>
        /// <param name="fps">초당 저장 프레임 수</param>
        /// <param name="startAtTime">이 게임 시간(Time.time)이 되면 녹화를 시작한다</param>
        /// <param name="outDir">프레임 저장 폴더. 비우면 프로젝트 Temp/GifFrames</param>
        public static void StartRecording(float durationSec, float fps, float startAtTime = 0f, string outDir = null)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("GifFrameRecorder: Play 모드에서만 녹화할 수 있습니다.");
                return;
            }

            if (string.IsNullOrEmpty(outDir))
                outDir = DefaultOutputDir;

            var go = new GameObject("GifFrameRecorder (temp)") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<GifFrameRecorderRunner>().Begin(durationSec, fps, startAtTime, outDir);
            Debug.Log($"GifFrameRecorder: 녹화 예약 (시작 {startAtTime:F1}s, {durationSec:F1}초, {fps:F0}fps) → {outDir}");
        }
    }
}
