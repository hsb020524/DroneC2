# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 요약

DroneC2는 포트폴리오용 Unity 프로젝트로, 드론·전차의 GPS 위치와 작전 상황을 3D로 보여주는 관제 시스템이다. 세부 내용은 `docs/`를 참고한다.

- `docs/requirements.md` — 요구사항(FR/NFR ID), 5단계 로드맵, 범위 제외, 보안 원칙
- `docs/architecture.md` — 데이터 소스 → 좌표 변환 → 표시 3계층 구조와 Mermaid 다이어그램

계층 간 의존은 단방향이다. 표시 계층은 데이터가 가짜 GPS인지 MAVLink인지 알지 못해야 하며, 데이터 소스는 공통 인터페이스 `ITelemetrySource` 뒤에 두고, 좌표 변환은 `ICoordinateConverter` 뒤에 둔다. 소스와 변환기와 뷰를 잇는 곳은 `UnitTelemetryBinder` 하나뿐이다.

## 작업 규칙

- 응답은 한국어로 한다.
- 작업은 작은 단위로 나눠 진행한다.
- 각 작업이 끝나면 Unity 씬을 저장한 뒤 커밋한다. `.meta` 파일은 에셋과 함께 커밋한다.
- 커밋 메시지에 공동 작성자(`Co-Authored-By`) 표시를 넣지 않는다.
- 단계가 끝나면 필요한 `docs/`를 갱신한다.

### 기록 규칙 (포트폴리오 devlog)

- `docs/devlog.md`는 **단계가 끝날 때 한 번에** 정리한다. 작업 중에는 따로 기록하지 않는다.
- 단계 정리 내용은 다음만 담는다: 구현 요약, 검증 결과, 대표 스크린샷 1장, 10~20초 결과 GIF 1개, 가장 중요했던 문제·해결 또는 설계 결정 2~3개.
- 사소한 오류나 단순 수정은 기록하지 않는다.
- 이미지는 `docs/images/stageN-이름.png`, GIF는 `docs/images/stageN-이름.gif`(10MB 이하)로 저장하고, 완료 커밋에 `stage-N` 태그를 붙인다.
- README 맨 위에는 최신 단계 GIF를 크게 두고, 단계별 섹션에도 각 단계 GIF를 둔다. 새 단계가 끝나면 맨 위 GIF를 새 단계 것으로 바꾼다.

## Unity 규칙

- Unity 조작(씬, 오브젝트, 컴포넌트, 에셋 등)은 MCP for Unity(UnityMCP 도구)로 한다. 스크립트 수정 후에는 `read_console`로 컴파일 오류를 확인한다.
- `read_console`은 `error`·`warning`만 조회한다. Cesium 네이티브 라이브러리가 `info` 로그에 토큰이 포함된 URL을 그대로 출력하므로 전체(`all`/`info`) 조회를 하지 않는다.
- 스크립트는 `Assets/Scripts/<역할>/` 아래에 역할별 폴더로 둔다. (예: `DataSource`, `Coordinate`, `View`, `UI`)
- 환경: Unity `6000.6.5f1`, URP 17.6.0, Input System 1.20.0, Test Framework 1.8.0.
- `*.csproj`, `*.slnx`와 `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 Unity가 생성하므로 수정하지 않는다.
- 빌드 스크립트는 없다. EditMode 테스트(`Assets/Tests/EditMode`, 184건)는 UnityMCP `run_tests` 또는 Unity batchmode로 실행한다.
- Player Settings의 Run In Background를 켜 두었다. 에디터가 비포커스여도 Play 모드가 진행된다.

```powershell
# -testFilter "Namespace.Class.Method" 로 단일 테스트 실행
& "<UnityEditorPath>\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

## 개발 도구

### GIF 녹화 (`stageN-이름 GIF 녹화해줘`)

Play 모드의 Game 화면을 PNG 프레임으로 저장하고 GIF로 조립한다. 녹화용 오브젝트는 숨김 처리되어 씬에 저장되지 않는다.

| 파일 | 역할 |
|---|---|
| `Assets/Editor/Tools/GifFrameRecorder.cs` | 녹화 시작 API `StartRecording(durationSec, fps, startAtTime, outDir)`. Editor 폴더라 빌드에 포함되지 않음 |
| `Assets/Scripts/Tools/GifFrameRecorderRunner.cs` | 프레임 저장 코루틴. 파일 전체가 `#if UNITY_EDITOR`라 빌드에 포함되지 않음 |
| `tools/make_gif.py` | 프레임 → GIF. 10MB 초과 시 프레임 수·해상도·색상 수를 자동으로 줄임 |

절차:

1. Pillow를 임시 폴더에 설치한다. 저장소에는 넣지 않는다.
   `python -m pip install Pillow --target <임시폴더>`
2. Play 모드에 들어가 `execute_code`로 녹화를 시작한다. 시작 시각은 원하는 장면(예: 드론이 코너를 도는 시점)이 클립에 들어오도록 정한다.
   `DroneC2.EditorTools.GifFrameRecorder.StartRecording(15f, 10f, 16f);` (15초, 10fps, 게임 시간 16초부터)
3. `Temp/GifFrames/_done.txt`가 생길 때까지 기다린다. (`Temp/`는 Git 무시 대상) 프레임은 `Temp/GifFrames/frame_NNNN.png`로 저장된다.
4. Play를 종료하고 GIF를 만든다.
   `python -I tools/make_gif.py Temp/GifFrames docs/images/stageN-이름.gif --fps 10 --width 640 --max-mb 10 --pillow-path <임시폴더>`
5. `make_gif.py`는 UI 상태 색(초록·노랑·빨강)을 팔레트에 고정 배정한다(작은 글자 색이 256색 양자화에서 회색으로 사라지는 것을 막음, `--keep-color`로 변경). 만든 GIF에서 상태 글자 색이 살아 있는지 확인한다. 중간 프레임 몇 장을 직접 확인한 뒤(원하는 장면이 들어갔는지) `Temp/GifFrames/`와 Pillow 임시 폴더를 삭제한다.
6. GIF 크기가 10MB 이하인지 확인하고 `docs/devlog.md`에 추가한다.

주의: Play 모드에서 스크립트를 수정하면 도메인 리로드로 녹화가 끊긴다. 에디터 Run In Background는 이미 켜져 있다.

UI가 들어가는 장면은 Game 뷰 해상도를 1920×1080으로 맞춘 뒤 녹화한다(작은 뷰에서는 UI 글자가 GIF에서 읽히지 않는다). 에디터 포커스가 없으면 Input System 입력이 Game 뷰에 전달되지 않으므로, 자동 검증은 UI 요소에 이벤트를 직접 보내는 방식으로 한다.

### 도로 이탈·경사·씬 측정 (`RoadProbe`, `ScenePlaySampler`)

전차 경로의 도로 위 정렬과 지형 경사를 재고, Play 중 실제 파이프라인(소스 → 바인더 → 변환기 → 뷰)의 위치를 기록한다. 파일 전체가 `UNITY_EDITOR` 전용이다(`Assets/Scripts/Tools/`).

- 지형 높이(화면 없이, 에디터를 닫고): `Unity.exe -batchmode -nographics -projectPath . -executeMethod DroneC2.Tools.RoadProbe.RunFromCommandLine` → `Temp/road-probe.csv`·`road-probe.txt`. 환경 변수 `ROAD_PROBE_STEP`(m, 기본 10)이 조회 간격이다.
- Play 중 전차 뷰 위치: `execute_code`로 `DroneC2.Tools.RoadProbe.StartPlayMeasurement("라벨", 초);` → `Temp/road-play-라벨.csv`(끝나면 `.done`).
- Play 중 씬의 모든 `FakeGpsSource` 유닛(1·2단계 씬 포함): `DroneC2.Tools.ScenePlaySampler.Start("라벨", 초);` → `Temp/scene-play-라벨.csv`.
- 분석: `python tools/measure_road_deviation.py [--heights Temp/road-probe.csv] [--positions Temp/road-play-라벨.csv]`(`--positions`는 여러 번 가능, 인자 없이 실행하면 오프라인 재생).
- 샘플러는 밀린 샘플을 건너뛰므로 시간 간격이 0에 가까운 값이 생기지 않는다. Play 중 소스 전환(`SetMode`)은 `execute_code`로 하고, 측정 뒤 FakeGps로 되돌리며 씬은 저장하지 않는다.
- `execute_code`의 안전 검사가 `Directory.Delete` 같은 구문을 막는다. 임시 폴더 정리는 셸에서 한다.

### 성능 측정 (`PerfProbe`)

Play 모드에서 프레임 시간·활성 타일 렌더러 수를 샘플링해 `Temp/perf.txt`에 한 줄씩 덧붙인다. 파일 전체가 `UNITY_EDITOR` 전용이다.

- 시작: `execute_code`로 `DroneC2.EditorTools.PerfProbe.Start("라벨", 워밍업초, 측정초);`
- 결과: `Temp/perf.txt`(`Temp/`는 Git 무시 대상). 조건을 바꿀 때마다 다른 라벨로 호출한다.
- 테스트 실행(`run_tests`)과 겹치지 않게 한다. Play를 시작하면 테스트 실행이 중단된다.

## 보안 원칙

- 실제 군사 데이터와 실제 시설 좌표를 사용하지 않는다. 위치·경로·식별자는 가상 값 또는 공개 데이터만 쓴다.
- 수신·표시 전용이며 유닛에 명령을 보내는 경로를 만들지 않는다.
- Cesium ion 토큰 등 비밀 값은 채팅에 붙여넣거나 커밋하지 않는다. 연결은 사용자가 Unity에서 직접 한다.
- 푸시 전에 커밋에 토큰 형태 문자열(JWT `eyJ…`)과 `Assets/CesiumSettings` 경로가 없는지 검사한다. 토큰이 대화나 로그에 노출되면 재발급한다.
- 자세한 내용은 `docs/requirements.md` 6절을 따른다.

## 기타

- `.gitattributes`는 `*.wlt`(Unity 레이아웃 파일)를 GitHub 언어 통계에서 제외한다. 삭제하지 않는다.
