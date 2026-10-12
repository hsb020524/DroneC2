# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 요약

DroneC2는 포트폴리오용 Unity 프로젝트로, 드론·전차의 GPS 위치와 작전 상황을 3D로 보여주는 관제 시스템이다. 세부 내용은 `docs/`를 참고한다.

- `docs/requirements.md` — 요구사항(FR/NFR ID), 5단계 로드맵, 범위 제외, 보안 원칙
- `docs/architecture.md` — 데이터 소스 → 좌표 변환 → 표시 3계층 구조와 Mermaid 다이어그램

계층 간 의존은 단방향이다. 표시 계층은 데이터가 가짜 GPS인지 MAVLink인지 알지 못해야 하며, 데이터 소스는 공통 인터페이스 `ITelemetrySource` 뒤에 두고, 좌표 변환은 `ICoordinateConverter` 뒤에 둔다. 소스와 변환기와 뷰를 잇는 곳은 `UnitTelemetryBinder` 하나뿐이다.

## 현재 진행 상황

> 단계가 끝날 때마다 이 섹션을 갱신한다.

- 현재 단계: 2단계 완료(`stage-2` 태그) — 3단계(다중 유닛 + 관제 UI) 시작 전
- 완료: 1단계 전체(평면 위 가짜 GPS 드론, 경로선, 추적 카메라)와 2단계 구현·검증.
  - 2단계: `Stage2Cesium` 씬(Cesium World Terrain + Bing Maps Aerial)에서 `CesiumGeoreferenceConverter`(`ICoordinateConverter`)로 위경도를 월드 좌표로 변환한다. `ITerrainHeightSampler` + `TerrainHeightGrid`(격자 캐시·쌍선형 보간) + `AltitudeResolver`로 지표 기준 고도(`AltitudeMode`: Absolute/AboveGround/ClampToGround)를 지원한다. 드론은 `AboveGround` 80m로 경로를 이동하며 지표 위 높이가 77~81m로 유지됨을 실측했다.
  - 기준점: 35.563°N, 83.499°W(클링맨스 돔 부근), 지오레퍼런스 높이 2000m(타원체고). 경로는 기준점 주변 약 1.2km 사각형(코너까지 약 850m), 속도 25m/s.
  - View·DataSource 코드는 변환기 교체 때 수정하지 않았다. 이후 `PathTrail`이 첫 배치 전 위치를 기록하는 결함만 `DroneView.HasTarget`으로 수정했다.
  - EditMode 테스트 35건 통과(좌표 변환 2종 7건씩, 격자 11건, 고도 해석 8건, `DroneView` 2건). Play 모드 확인 완료.
- 다음: 3단계 — 다중 유닛 + 관제 UI(드론·전차, 전차는 `ClampToGround`). 미구현: FR-004 시나리오 JSON 로딩(웨이포인트는 인스펙터 입력)
  - 시연 지역: 그레이트 스모키 산맥 국립공원(미국). 보안상 국내 지역(특히 접경 지역)은 의도적으로 제외했고, 한국 산악 지형과 고도(최고 약 2,000m)·숲 능선이 유사해 대체 지역으로 선정했다.
  - Cesium ion 토큰: DroneC2 전용 토큰, 권한 `assets:read`만, 접근 대상은 필요한 지형·이미지 에셋으로 제한. `Assets/CesiumSettings/`는 통째로 `.gitignore` 처리(커밋 금지, `ion.cesium.com.asset`·`CesiumRuntimeSettings.asset`에 토큰 필드가 있음). 토큰은 채팅에 붙여넣지 않고 사용자가 Unity Cesium 패널에서 직접 연결한다. README에는 본인 토큰을 넣어 실행하라는 안내를 쓴다.
  - 씬이 참조하는 `ionServer` GUID는 무시된 `.asset`에 묶여 있어 다른 환경에서는 참조가 비어 있을 수 있다. 새 환경에서는 Cesium 패널에서 토큰을 연결한 뒤 씬을 열어 확인한다.
- 5단계 메모: `UnitStatus` enum 분리 재검토. 소스용(Normal/Warning)과 표시용(Normal/Warning/Lost)을 나눠 두절을 타입으로 강제할지, MAVLink 외부 데이터 수신 시 결정한다. (3단계에서는 `Lost`를 한 enum에 두고 소스는 보고하지 않는다는 규칙을 주석으로만 둠)
- 2단계 문서: `docs/devlog.md` 2단계 정리(스크린샷 `docs/images/stage2-cesium-terrain.png`, GIF `stage2-cesium-terrain.gif`), `README.md` 작성 완료. 이후 단계가 끝날 때 README의 진행 상황·스크린샷을 갱신한다.
- 단계 작업은 작은 단위로 나눠 진행하며, 각 단위마다 씬 저장 후 커밋한다.

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 평면 위 가짜 GPS 드론 이동·경로 표시 | 완료 |
| 2 | Cesium 실제 지형 | 완료 |
| 3 | 다중 유닛 + 관제 UI | 예정 |
| 4 | 카메라 패널 | 예정 |
| 5 | MAVLink 시뮬레이터 연동 | 예정 |

## 작업 규칙

- 응답은 한국어로 한다.
- 세션을 시작하면 먼저 `git status`와 위 "현재 진행 상황"을 확인한다.
- 작업은 작은 단위로 나눠 진행한다.
- 각 작업이 끝나면 Unity 씬을 저장한 뒤 커밋한다. `.meta` 파일은 에셋과 함께 커밋한다.
- 커밋 메시지에 공동 작성자(`Co-Authored-By`) 표시를 넣지 않는다.
- 단계가 끝나면 "현재 진행 상황"을 갱신하고 필요하면 `docs/`도 함께 갱신한다.

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
- 빌드 스크립트는 없다. EditMode 테스트(`Assets/Tests/EditMode`, 35건)는 UnityMCP `run_tests` 또는 Unity batchmode로 실행한다.
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
5. 중간 프레임 몇 장을 직접 확인한 뒤(원하는 장면이 들어갔는지) `Temp/GifFrames/`와 Pillow 임시 폴더를 삭제한다.
6. GIF 크기가 10MB 이하인지 확인하고 `docs/devlog.md`에 추가한다.

주의: Play 모드에서 스크립트를 수정하면 도메인 리로드로 녹화가 끊긴다. 에디터 Run In Background는 이미 켜져 있다.

## 보안 원칙

- 실제 군사 데이터와 실제 시설 좌표를 사용하지 않는다. 위치·경로·식별자는 가상 값 또는 공개 데이터만 쓴다.
- 수신·표시 전용이며 유닛에 명령을 보내는 경로를 만들지 않는다.
- Cesium ion 토큰 등 비밀 값은 채팅에 붙여넣거나 커밋하지 않는다. 연결은 사용자가 Unity에서 직접 한다.
- 푸시 전에 올라갈 커밋에 토큰 형태 문자열(JWT `eyJ…`)과 `Assets/CesiumSettings` 경로가 없는지 검사한다. 토큰이 대화나 로그에 노출되면 재발급한다.
- 자세한 내용은 `docs/requirements.md` 6절을 따른다.

## 기타

- `.gitattributes`는 `*.wlt`(Unity 레이아웃 파일)를 GitHub 언어 통계에서 제외한다. 삭제하지 않는다.
