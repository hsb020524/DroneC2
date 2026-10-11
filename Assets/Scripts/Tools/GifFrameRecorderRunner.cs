#if UNITY_EDITOR
// 개발 도구 전용. 에디터에서만 컴파일되므로 플레이어 빌드에는 포함되지 않는다.
// MonoBehaviour는 Editor 어셈블리에 두면 AddComponent가 동작하지 않아 런타임 어셈블리에 둔다.
using System.Collections;
using System.IO;
using UnityEngine;

namespace DroneC2.EditorTools
{
    /// <summary>
    /// Play 모드에서 Game 화면을 일정 간격으로 PNG로 저장하는 임시 컴포넌트.
    /// Assets/Editor/Tools/GifFrameRecorder가 숨김 GameObject에 붙여 사용한다.
    /// </summary>
    public class GifFrameRecorderRunner : MonoBehaviour
    {
        public const string DoneMarkerFileName = "_done.txt";

        public bool IsRunning { get; private set; }

        public void Begin(float durationSec, float fps, float startAtTime, string outDir)
        {
            IsRunning = true;
            StartCoroutine(Record(durationSec, fps, startAtTime, outDir));
        }

        IEnumerator Record(float durationSec, float fps, float startAtTime, string outDir)
        {
            Directory.CreateDirectory(outDir);
            foreach (string old in Directory.GetFiles(outDir, "frame_*.png"))
                File.Delete(old);
            string marker = Path.Combine(outDir, DoneMarkerFileName);
            if (File.Exists(marker))
                File.Delete(marker);

            while (Time.time < startAtTime)
                yield return null;

            float interval = 1f / fps;
            int total = Mathf.Max(1, Mathf.RoundToInt(durationSec * fps));
            float startReal = Time.realtimeSinceStartup;
            float gameStart = Time.time;

            for (int i = 0; i < total; i++)
            {
                float due = startReal + i * interval;
                while (Time.realtimeSinceStartup < due)
                    yield return null;

                yield return new WaitForEndOfFrame();
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(outDir, $"frame_{i:D4}.png"), tex.EncodeToPNG());
                Destroy(tex);
            }

            float realElapsed = Time.realtimeSinceStartup - startReal;
            File.WriteAllText(marker,
                $"frames={total}\nfps={fps}\ngameTimeStart={gameStart:F2}\nrealElapsedSec={realElapsed:F2}\n");
            Debug.Log($"GifFrameRecorder: {total}프레임 저장 완료 ({outDir}, 실제 {realElapsed:F1}초)");

            IsRunning = false;
            Destroy(gameObject);
        }
    }
}
#endif
