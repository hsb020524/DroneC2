# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 요약

DroneC2는 포트폴리오용 Unity 프로젝트로, 드론·전차의 GPS 위치와 작전 상황을 3D로 보여주는 관제 시스템이다. 세부 내용은 `docs/`를 참고한다.

- `docs/requirements.md` — 요구사항(FR/NFR ID), 5단계 로드맵, 범위 제외, 보안 원칙
- `docs/architecture.md` — 데이터 소스 → 좌표 변환 → 표시 3계층 구조와 Mermaid 다이어그램

계층 간 의존은 단방향이다. 표시 계층은 데이터가 가짜 GPS인지 MAVLink인지 알지 못해야 하며, 데이터 소스는 공통 인터페이스 `ITelemetrySource` 뒤에 두고, 좌표 변환은 `ICoordinateConverter` 뒤에 둔다. 소스와 변환기와 뷰를 잇는 곳은 `UnitTelemetryBinder` 하나뿐이다.

## 현재 진행 상황

> 단계가 끝날 때마다 이 섹션을 갱신한다.

- 현재 단계: 4단계 구현·문서 정리 완료(`stage-4` 태그는 푸시 전 보고 대기) — 5단계(MAVLink) 시작 전
- 완료: 1단계 전체(평면 위 가짜 GPS 드론, 경로선, 추적 카메라)와 2단계 구현·검증.
  - 2단계: `Stage2Cesium` 씬(Cesium World Terrain + Bing Maps Aerial)에서 `CesiumGeoreferenceConverter`(`ICoordinateConverter`)로 위경도를 월드 좌표로 변환한다. `ITerrainHeightSampler` + `TerrainHeightGrid`(격자 캐시·쌍선형 보간) + `AltitudeResolver`로 지표 기준 고도(`AltitudeMode`: Absolute/AboveGround/ClampToGround)를 지원한다. 드론은 `AboveGround` 80m로 경로를 이동하며 지표 위 높이가 77~81m로 유지됨을 실측했다.
  - 기준점: 35.563°N, 83.499°W(클링맨스 돔 부근), 지오레퍼런스 높이 2000m(타원체고). 경로는 기준점 주변 약 1.2km 사각형(코너까지 약 850m), 속도 25m/s.
  - View·DataSource 코드는 변환기 교체 때 수정하지 않았다. 이후 `PathTrail`이 첫 배치 전 위치를 기록하는 결함만 `DroneView.HasTarget`으로 수정했다.
  - EditMode 테스트 35건 통과(좌표 변환 2종 7건씩, 격자 11건, 고도 해석 8건, `DroneView` 2건). Play 모드 확인 완료.
- 3단계: `Stage3Control` 씬에서 드론 3기(`AboveGround` 60/80/100m)·전차 2기(`ClampToGround`)를 동시에 표시한다. 구조: `UnitKind`/`UnitStatus`는 DataSource 계층, `UnitStateStore`(순수 로직)·`UnitRegistry`(MonoBehaviour)가 수신·두절 판정(2초)·선택을 담고, `UnitMarker`(마커)·`CameraSelectionBinder`(선택→카메라)·UI Toolkit 목록/상세 패널(`UnitListPresenter`, `UnitDetailPresenter`, `UnitFormat`)이 레지스트리만 읽는다. 유닛은 `Assets/Prefabs/Units/Drone.prefab`·`Tank.prefab`이며 변환기·레지스트리는 씬에 하나씩 두고 공유한다.
  - EditMode 테스트 65건 통과. 10기 에디터 평균 643 FPS(빌드 측정 아님). UI는 1920×1080 기준 높이 맞춤 스케일링이다.
  - 알려진 한계: TNK-01 경로가 도로에서 벗어나는 구간이 있다(야지 주행으로 보고 보정하지 않기로 결정). TNK-02 경로는 짧다(동쪽 연장은 경사 15° 초과). 높이 캐시 상한 없음. 3D 오브젝트 클릭 선택 없음. (Esc 키 해제는 사용자가 직접 Play로 확인함)
- 다음: 5단계 — MAVLink 시뮬레이터 연동(FR-014, FR-015)
  - 시연 지역: 그레이트 스모키 산맥 국립공원(미국). 보안상 국내 지역(특히 접경 지역)은 의도적으로 제외했고, 한국 산악 지형과 고도(최고 약 2,000m)·숲 능선이 유사해 대체 지역으로 선정했다.
  - Cesium ion 토큰: DroneC2 전용 토큰, 권한 `assets:read`만, 접근 대상은 필요한 지형·이미지 에셋으로 제한. `Assets/CesiumSettings/`는 통째로 `.gitignore` 처리(커밋 금지, `ion.cesium.com.asset`·`CesiumRuntimeSettings.asset`에 토큰 필드가 있음). 토큰은 채팅에 붙여넣지 않고 사용자가 Unity Cesium 패널에서 직접 연결한다. README에는 본인 토큰을 넣어 실행하라는 안내를 쓴다.
  - 씬이 참조하는 `ionServer` GUID는 무시된 `.asset`에 묶여 있어 다른 환경에서는 참조가 비어 있을 수 있다. 새 환경에서는 Cesium 패널에서 토큰을 연결한 뒤 씬을 열어 확인한다.
- 5단계 진행(계획: 5-A 상태 enum 분리 → 5-B 파서 → 5-C 수신기 → 5-D 전환 → 5-E 에미터 → 5-F SITL 1기 → 5-G 문서). 완료: 5-A, 5-B, 5-C(테스트 128건 통과). 계획 파일은 사용자 홈의 `.claude/plans/5-mellow-sparrow.md`.
  - 5-A: 소스 보고용 `ReportedStatus`(Normal/Warning)와 표시용 `UnitStatus`(+Lost)를 분리했다. `TelemetrySample.Status`가 `ReportedStatus`이며 `UnitStateStore`가 변환하고 Lost는 `Tick`만 붙인다. 표시 코드는 수정하지 않았다. (devlog 설계 결정 후보 1)
  - 5-B: `Assets/Scripts/DataSource/Mavlink/`에 CRC·프레임·파서·메시지 디코더(HEARTBEAT, GLOBAL_POSITION_INT)를 두었다. 테스트 112건 통과(신규 24건).
  - 설계 결정 후보 2: **v1·v2를 모두 파싱하고 버전별 카운터(`V1Frames`/`V2Frames`/`Rejected`)를 둔다.** 우리는 송신하지 않아 상대(ArduPilot 등)의 프로토콜 버전을 협상으로 바꿀 수 없으므로 수신 쪽이 유연해야 한다. SITL 연동 때 어떤 버전이 오는지·얼마나 버려지는지 디버깅에 쓴다. v2의 끝 0바이트 생략(truncation)은 메시지 정의 길이로 0 복원한다(잘린 프레임 테스트 포함).
  - 5-C: `MavlinkUdpListener`(루프백 UDP 수신 전용 스레드 + 상한 있는 큐), `MavlinkTelemetryRouter`(순수 로직: sysid 테이블 조회·상태·고도·방위), `MavlinkUnitTable`(ScriptableObject, sysid·unitId·종류·홈 위경도), `MavlinkReceiver`(씬에 하나, 메인 스레드에서 큐를 비움), `MavlinkUnitSource`(sysid별 `ITelemetrySource`). 테이블에 없는 sysid는 무시하고 `UnknownSysid`로 센다. 테이블과 HEARTBEAT의 종류가 다르면 테이블을 따르고 sysid마다 경고 1회. 테이블 에셋과 씬 배치는 5-D·5-E에서 한다.
  - 5-C 계층 메모: `MavlinkReceiver`가 홈 지형 높이 조회를 위해 `ITerrainHeightSampler`(Coordinate 계층)를 직접 쓴다. "소스·변환기·뷰는 `UnitTelemetryBinder`에서만 만난다"는 원칙의 예외이므로 5단계 문서에서 architecture.md에 명시한다. 5-D에서 MAVLink 드론은 바인더 고도 모드가 `Absolute`여야 하므로 전환 시 바인더 모드 처리 방식을 정한다.
  - 설계 결정 후보 3: 고도는 `홈 지형 높이(sysid 설정 테이블의 홈 위경도로 조회) + relative_alt`를 절대 고도로 쓴다. `HOME_POSITION` 요청과 첫 샘플을 홈으로 가정하는 방식은 쓰지 않는다(송신 불필요, 비행 중 접속 시에도 정확).
- 4단계: `Stage3Control` 씬에 유닛 카메라 패널을 더했다(씬은 새로 만들지 않음). `UnitCameraRig`(View, 유닛 자식 카메라 + `RenderTexture`, 선택·고정된 유닛만 렌더), `CesiumUnitCameraLink`(렌더 중인 카메라만 Cesium 타일 선택에 등록), `UnitCameraPolicy`(렌더 규칙), `UnitStateStore.Pin/Unpin`(고정 1기), `CameraPanelPresenter`(선택·고정 패널). `Scenario`가 시나리오 JSON(FR-004)을 읽어 `FakeGpsSource`에 적용하며 `Assets/Scenarios/stage3-demo.json`이 3단계 5기를 옮긴 파일이다.
  - EditMode 테스트 88건 통과. 카메라 1대 약 0.4ms. Cesium 등록 시 타일 렌더러 95→149(1대)→351(2대), 2대 등록 직후 262ms 정지 1회(에디터 측정). 전차 카메라는 포신이 시야를 가려 차체 앞면 앞(높이 2m, 전방 6.3m)에 둔다.
  - 알려진 한계: 두절 오버레이 화면 미캡처, 고정/해제 버튼 실제 클릭 미검증, 고정은 1기까지.
- 3단계 문서: `docs/devlog.md` 3단계 정리(스크린샷 `docs/images/stage3-control-ui.png`, GIF `stage3-control-ui.gif`), `README.md`·`docs/requirements.md`·`docs/architecture.md` 갱신 완료. 이후 단계가 끝날 때 README의 진행 상황·맨 위 GIF를 갱신한다.
- 단계 작업은 작은 단위로 나눠 진행하며, 각 단위마다 씬 저장 후 커밋한다.

### 남은 할 일

- [x] `stage-4` 태그와 4-C 이후 커밋의 푸시(푸시 전 토큰·`Assets/CesiumSettings` 검사)
- [ ] 저장소 공개 전: 새 폴더에 클론해서 README 실행 방법대로 동작하는지 확인 (Cesium ion 토큰 연결 후 씬 열기, `ionServer` 참조가 비어 있지 않은지 포함)

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 평면 위 가짜 GPS 드론 이동·경로 표시 | 완료 |
| 2 | Cesium 실제 지형 | 완료 |
| 3 | 다중 유닛 + 관제 UI | 완료 |
| 4 | 카메라 패널 | 완료 |
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
- 빌드 스크립트는 없다. EditMode 테스트(`Assets/Tests/EditMode`, 88건)는 UnityMCP `run_tests` 또는 Unity batchmode로 실행한다.
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

UI가 들어가는 장면은 Game 뷰 해상도를 1920×1080으로 맞춘 뒤 녹화한다(작은 뷰에서는 UI 글자가 GIF에서 읽히지 않는다). 에디터 포커스가 없으면 Input System 입력이 Game 뷰에 전달되지 않으므로, 자동 검증은 UI 요소에 이벤트를 직접 보내는 방식으로 한다.

### 성능 측정 (`PerfProbe`)

Play 모드에서 프레임 시간·활성 타일 렌더러 수를 샘플링해 `Temp/perf.txt`에 한 줄씩 덧붙인다. 파일 전체가 `UNITY_EDITOR` 전용이다.

- 시작: `execute_code`로 `DroneC2.EditorTools.PerfProbe.Start("라벨", 워밍업초, 측정초);`
- 결과: `Temp/perf.txt`(`Temp/`는 Git 무시 대상). 조건을 바꿀 때마다 다른 라벨로 호출한다.
- 테스트 실행(`run_tests`)과 겹치지 않게 한다. Play를 시작하면 테스트 실행이 중단된다.

## 보안 원칙

- 실제 군사 데이터와 실제 시설 좌표를 사용하지 않는다. 위치·경로·식별자는 가상 값 또는 공개 데이터만 쓴다.
- 수신·표시 전용이며 유닛에 명령을 보내는 경로를 만들지 않는다.
- Cesium ion 토큰 등 비밀 값은 채팅에 붙여넣거나 커밋하지 않는다. 연결은 사용자가 Unity에서 직접 한다.
- 푸시 전에 올라갈 커밋에 토큰 형태 문자열(JWT `eyJ…`)과 `Assets/CesiumSettings` 경로가 없는지 검사한다. 토큰이 대화나 로그에 노출되면 재발급한다.
- 자세한 내용은 `docs/requirements.md` 6절을 따른다.

## 기타

- `.gitattributes`는 `*.wlt`(Unity 레이아웃 파일)를 GitHub 언어 통계에서 제외한다. 삭제하지 않는다.
