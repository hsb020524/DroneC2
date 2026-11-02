# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 요약

DroneC2는 포트폴리오용 Unity 프로젝트로, 드론·전차의 GPS 위치와 작전 상황을 3D로 보여주는 관제 시스템이다. 세부 내용은 `docs/`를 참고한다.

- `docs/requirements.md` — 요구사항(FR/NFR ID), 5단계 로드맵, 범위 제외, 보안 원칙
- `docs/architecture.md` — 데이터 소스 → 좌표 변환 → 표시 3계층 구조와 Mermaid 다이어그램

계층 간 의존은 단방향이다. 표시 계층은 데이터가 가짜 GPS인지 MAVLink인지 알지 못해야 하며, 데이터 소스는 공통 인터페이스 `ITelemetrySource` 뒤에 두고, 좌표 변환은 `ICoordinateConverter` 뒤에 둔다. 소스와 변환기와 뷰를 잇는 곳은 `UnitTelemetryBinder` 하나뿐이다.

## 현재 진행 상황

> 단계가 끝날 때마다 이 섹션을 갱신한다.

- 현재 단계: 6단계(① 소스 전환 시 초기화, ② 전차 경로를 OSM 실제 도로로) 구현·검증·문서 정리 완료. `stage-6` 태그는 로컬에만 있고 푸시 전(푸시는 사용자 확인 후). 나머지 후보(③~⑦)는 정의 전(아래 "6단계 후보")
- 완료: 1단계 전체(평면 위 가짜 GPS 드론, 경로선, 추적 카메라)와 2단계 구현·검증.
  - 2단계: `Stage2Cesium` 씬(Cesium World Terrain + Bing Maps Aerial)에서 `CesiumGeoreferenceConverter`(`ICoordinateConverter`)로 위경도를 월드 좌표로 변환한다. `ITerrainHeightSampler` + `TerrainHeightGrid`(격자 캐시·쌍선형 보간) + `AltitudeResolver`로 지표 기준 고도(`AltitudeMode`: Absolute/AboveGround/ClampToGround)를 지원한다. 드론은 `AboveGround` 80m로 경로를 이동하며 지표 위 높이가 77~81m로 유지됨을 실측했다.
  - 기준점: 35.563°N, 83.499°W(클링맨스 돔 부근), 지오레퍼런스 높이 2000m(타원체고). 경로는 기준점 주변 약 1.2km 사각형(코너까지 약 850m), 속도 25m/s.
  - View·DataSource 코드는 변환기 교체 때 수정하지 않았다. 이후 `PathTrail`이 첫 배치 전 위치를 기록하는 결함만 `DroneView.HasTarget`으로 수정했다.
  - EditMode 테스트 35건 통과(좌표 변환 2종 7건씩, 격자 11건, 고도 해석 8건, `DroneView` 2건). Play 모드 확인 완료.
- 3단계: `Stage3Control` 씬에서 드론 3기(`AboveGround` 60/80/100m)·전차 2기(`ClampToGround`)를 동시에 표시한다. 구조: `UnitKind`/`UnitStatus`는 DataSource 계층, `UnitStateStore`(순수 로직)·`UnitRegistry`(MonoBehaviour)가 수신·두절 판정(2초)·선택을 담고, `UnitMarker`(마커)·`CameraSelectionBinder`(선택→카메라)·UI Toolkit 목록/상세 패널(`UnitListPresenter`, `UnitDetailPresenter`, `UnitFormat`)이 레지스트리만 읽는다. 유닛은 `Assets/Prefabs/Units/Drone.prefab`·`Tank.prefab`이며 변환기·레지스트리는 씬에 하나씩 두고 공유한다.
  - EditMode 테스트 65건 통과. 10기 에디터 평균 643 FPS(빌드 측정 아님). UI는 1920×1080 기준 높이 맞춤 스케일링이다.
  - 알려진 한계: TNK-01 경로가 도로에서 벗어나는 구간이 있다(야지 주행으로 보고 보정하지 않기로 결정 → 6단계 ②에서 OSM 도로 경로로 교체해 해소). TNK-02 경로는 짧다(동쪽 연장은 경사 15° 초과). 높이 캐시 상한 없음. 3D 오브젝트 클릭 선택 없음. (Esc 키 해제는 사용자가 직접 Play로 확인함)
- 6단계 ①(소스 전환 시 초기화, 구현·검증·커밋 완료, 푸시 전): 5단계의 한계("소스 전환은 Play 시작 전 설정 기준", 전환하면 이전 유닛이 두절로 남아 가짜·실제가 섞임)를 해소했다. `TelemetrySourceSelector.SetMode`(Play 중 인스펙터 변경도 에디터 전용 `OnValidate`+`delayCall`로 받음, `#if UNITY_EDITOR`)가 전환하면 **이전 소스 구독 해제 → 각 바인더 `ResetUnit()` → `UnitRegistry.ResetUnits()` → 새 소스 켜기** 순서로 진행한다(초기화가 소스 활성화보다 먼저여야 `FakeGpsSource`가 켜질 때 바로 내보내는 첫 샘플이 지워지지 않는다). 초기화 대상: `UnitStateStore.Clear()`(유닛·선택·고정, `Cleared` 이벤트), 레지스트리의 뷰·텍스처 참조, `DroneView.ResetPlacement()`(렌더러 다시 숨김, `PathTrail`이 이벤트로 경로 삭제), 마커 선택, 카메라. `UnitListPresenter`는 `Cleared`로 목록을 다시 그린다. 시작 시 `Apply()`는 초기화하지 않는다. `FakeGpsSource`는 다시 켜질 때마다 시나리오를 처음부터 다시 시작한다(`OnEnable`→`Begin`). 선택기에 `registry` 필드를 씬에서 연결했다(없으면 경고). **카메라 `RenderTexture`는 `UnitCameraRig`가 소유**하므로 레지스트리는 참조만 버리고 `UnitCameraRig.ReleaseTexture()`가 렌더를 끄고 `Release()`만 한다(객체는 `OnDestroy`까지 유지, 다시 렌더하면 자동 재생성). EditMode 테스트 175건 통과.
  - Play 측정(에디터): 5기+선택·고정 상태에서 `SetMode(Mavlink)` → 유닛 0, 선택·고정 null, 배치 0, 활성 렌더러 0, 렌더 중 카메라 0, 목록 0행("유닛 목록 (0)"), 텍스처 5개 모두 해제. 에미터(sysid 1·2)를 보내자 UAV-01·02만 등장(미등록 sysid 0, 가짜 잔상 없음). `SetMode(FakeGps)`로 되돌리면 5기가 즉시 복구되고 수신 소켓이 닫힌다. 전환 12회 반복 후에도 전체 `RenderTexture` 수는 31개로 같았고(유닛 카메라 5개), 해제된 텍스처는 유닛을 선택하면 다시 만들어졌다. 콘솔 error·warning 0건. 씬은 모드를 바꾼 채 저장하지 않았다(기본 FakeGps; 스크린샷·GIF도 모드 값만 바꾸고 씬 저장 없이 Play). 남은 한계는 "한 번도 수신하지 못한 유닛은 목록에 나타나지 않음"(후보 ⑤).
  - 문서(README·requirements·architecture)는 이에 맞춰 수정했고, devlog의 5단계 기록은 당시 사실이라 남기되 6단계 ①에서 해소했다고 덧붙였다. 6단계 devlog 정리는 단계가 끝날 때 한다.
- 6단계 ②(전차 경로를 OSM 실제 도로로, 구현·측정·문서 완료, 푸시 전): 전차 경로를 OpenStreetMap의 `Kuwohi Access Road`(way 19607229)에서 만들었다. `tools/osm/`에 Overpass 질의문(`roads.overpassql`)·응답 원본(`roads-raw.json`, 105KB, 2026-11-02 14:16 UTC)·구간 설정(`tank-routes.json`)·ODbL 안내를 두고, `tools/osm_to_scenario.py`(표준 라이브러리만)가 도로를 구간으로 자르고 직선 단순화(1.5m)·왕복 점열로 만들어 `Assets/Scenarios/osm-roads-tnk.json`을 낸다. TNK-01은 도로 0–1000m(한 바퀴 1999m, 12m/s), TNK-02는 1100–1500m(799m, 8m/s). 드론은 `stage3-demo.json`에 남고 전차 두 기만 새 파일을 읽는다(`Stage3Control`의 TNK 인스턴스 `scenarioJson`을 새 에셋으로 연결). **ODbL:** 경로를 별도 파일로 분리하고 JSON의 `attribution`, `Assets/Scenarios/OSM-ATTRIBUTION.md`, README에 © OpenStreetMap contributors와 openstreetmap.org/copyright 링크를 넣었다.
  - **이동 방식 변경(사용자 제안):** 도착 반경(5m) 조향을 경로 길이 기반으로 바꿨다. `PathFollower`(`Assets/Scripts/DataSource/PathFollower.cs`)와 `tools/path_follower.py`가 같은 수식(선분 길이=평면 근사·선분 시작 위도의 cos, 선분 안은 위경도 선형 보간, 방위=선분 atan2)으로 `속도 × 경과 시간`을 닫힌 경로 위 위치·방위로 바꾼다. `FakeGpsSource`와 에미터가 이를 쓰고 `arrivalRadiusM`은 코드·프리팹·씬에서 제거했다. 에미터는 `--scenario`를 여러 번 받는다(기본 `stage3-demo.json` + `osm-roads-tnk.json`). 기준값 `tools/testdata/path_follower_golden.json`(`tools/make_path_golden.py`로 재생성)을 C# `PathFollowerTests`와 파이썬 `tools/test_path_follower.py`가 같이 대조한다. 드론과 1·2단계 씬도 이 방식으로 바뀌었다(꼭짓점을 정확히 지남).
  - **측정:** 도로 이탈(도로 중심선까지 최단 거리, 평균/최대) 오프라인 TNK-01 0.29/1.28m, TNK-02 0.61/1.49m; Play 실제 파이프라인(180초, 유닛 뷰 위치를 지오레퍼런스로 위경도 환산) 가짜 GPS 0.31/1.28m·0.60/1.49m, MAVLink(에미터 5기, 데이터그램 10,542·손상 0) 0.32/1.28m·0.58/1.48m. 지형 경사(Cesium 10m 간격, 140점 성공) 최대 4.4°(15° 초과 0). 1단계 씬 속도 평균 25.0m/s·꼭짓점 1.0–1.45m, 2단계 씬 지표 위 높이 79.5–80.4m(샘플러 기준). EditMode 184건·파이썬 8건 통과. 측정 도구와 사용법은 아래 "도로 이탈·경사·씬 측정". 샘플러는 에디터가 멈췄다 돌아온 뒤 밀린 샘플을 건너뛴다(처음에 1단계 최대 71.7m/s가 나온 측정 오류를 고침).
  - **설정 파일 재기록(클론 점검에서 발견):** 새 클론을 Unity로 열면 설정 파일 7개가 수정으로 표시되었다. 임시 클론을 batchmode로 열어 4개를 재현했고 내용은 원본·커밋과 바이트 단위로 같은 가짜 변경이었다(`git diff` 비어 있음). 원인은 `core.autocrlf=true`(Windows 기본)가 CRLF로 체크아웃하는데 Unity는 LF로 다시 쓰는 것으로 보이나 git 내부 메커니즘은 확인하지 못했다. `.gitattributes`에 `* text=auto eol=lf`를 추가했고, 이 규칙이 든 저장소를 새로 클론해 Unity로 열자 `git status`가 비어 있었다. 기존 기록은 모두 LF라 재정규화해도 바뀌는 파일이 없다.
- 6단계 후보(우선순위 순): ① ~~소스 전환 시 스토어·목록 초기화~~(완료) ② ~~TNK 경로를 OpenStreetMap 도로 데이터로 만들기~~(완료) ③ 두절 상태에서 상세 패널의 속도·위치가 마지막 값 그대로(예: 25.0 m/s) 보여 운용자가 현재 값으로 오해할 수 있음 → 두절일 때 흐리게 하거나 "마지막 값"으로 표시 ④ 지상 유닛 높이를 물리 메시 레이캐스트로 정하고 타일이 없으면 샘플러로 대체(콜라이더 비용·LOD 전환 시 높이 튐 평가 필요, 현재 약 ±4m) ⑤ 테이블 기반 유닛 사전 등록(수신 전 두절 표시) ⑥ 전차(Rover) SITL 확인 ⑦ 지표 높이 캐시 상한
- **재개 지점(2026-11-03 갱신, 푸시 전 로컬 커밋은 `origin/main`보다 앞선 상태, push 안 함)**:
  - 끝난 것: 콘솔 경고 4건은 `LogAssert.Expect`가 이미 걸린 테스트의 의도된 로그라 코드 수정 불필요(Expect는 실패만 막고 콘솔 출력은 남김). devlog에 cp949 사례와 MCP 끊김 메모를 넣었다(5단계 "남은 과제" 뒤 "메모: 새 환경 점검에서 발견한 문제"). 클론 재확인에서 pull·Safe Mode 없음·MCP 10.3.0 고정 해석은 확인됨.
  - **MCP for Unity 끊김(실측 확정):** 서버가 꺼져 있고 MCP 창이 열려 있으면 Play 중 EditorLoop가 약 276ms 멈춘다(창을 닫으면 사라짐). 원인은 창의 2초 주기 `IsLocalHttpServerReachable()`이 메인 스레드에서 `ConnectAsync().Wait(250ms)`를 하는 것. 창이 처음에 열려 있게 된 경로는 미확정(패키지에 메인 창 자동 열기 코드 없음, PC 전역 레이아웃 상속 가설은 미검증). **결정: C안.** `Packages/manifest.json`과 `packages-lock.json`에서 `com.coplaydev.unity-mcp`를 제외하고, README의 MCP 알림 문단을 "개발 도구로 쓰려면 이 줄을 추가"로 바꾼다(프로젝트 코드는 패키지에 의존하지 않음). 내 개발 PC는 **내장 패키지**로 유지한다: `Packages/com.coplaydev.unity-mcp/`에 aa5fc63(v10.3.0)을 sparse checkout으로 받아 넣고(`TestProjects`의 긴 경로 때문에 전체 클론은 Windows에서 체크아웃 실패) `.gitignore`로 제외했다. manifest 줄은 필요 없지만 Unity가 `packages-lock.json`에 `"source": "embedded"` 항목과 `newtonsoft-json`을 다시 넣으므로 lock이 항상 수정 상태로 보인다(실측). **그 lock은 커밋하지 않는다.** 로컬 `.git/hooks/pre-commit`(버전 관리 안 됨)이 스테이징된 lock·manifest에 `com.coplaydev.unity-mcp`가 있으면 커밋을 거부한다(`commit -a`도 막음, 시험함). 커밋 전 `git restore --staged Packages/`, pull 전 `git restore Packages/packages-lock.json`. 개발 도구로 쓸 때 서버를 안 켜면 창을 닫으라는 한 줄도 README에 넣는다.
  1. ~~C안 적용~~ 완료(2026-11-02): manifest·lock에서 MCP 제외·README 수정·내장 패키지 전환, 컴파일 오류 0건·EditMode 175건 통과.
  2. ~~이슈 초안 이동~~ 완료: `C:\UnityProjects\DroneC2-tools\notes\unity-mcp-issue-draft.md`. 제출 여부는 사용자가 결정한다.
  3. ~~README에 "Signature Missing" 안내~~ 완료.
  4. ~~클론 폴더 재확인~~ 완료(2026-11-02, 사용자 수행): `C:\UnityProjects\DroneC2-clonetest`(안의 `Assets/CesiumSettings`에 토큰이 있으니 건드리지 않는다)에서 pull(6개 파일, MCP 줄 삭제) 후 열었더니 MCP 탭과 `refreshed client config` 로그가 없고 Stage3Control Play 중 끊김도 없었다. 새로 발견: 클론에서 설정 파일 7개가 수정됨(아래 "설정 파일 재기록" 참고).
  5. push: 푸시 전 검사(JWT `eyJ…`, `Assets/CesiumSettings` 경로, **커밋별 패치의 계정 경로**, Packages `+unity-mcp` 0건) → 사용자 확인 → push(`main`과 `stage-6` 태그). 푸시하지 않은 로컬 백업 브랜치 `backup/before-path-scrub-2026-11-02`는 확인 후 삭제한다.
  6. 6단계는 ①②로 마무리했다. 이후 범위(후보 ③~⑦)는 정해지지 않았다. 시작 전에 사용자와 범위를 정한다.
  - 시연 지역: 그레이트 스모키 산맥 국립공원(미국). 보안상 국내 지역(특히 접경 지역)은 의도적으로 제외했고, 한국 산악 지형과 고도(최고 약 2,000m)·숲 능선이 유사해 대체 지역으로 선정했다.
  - Cesium ion 토큰: DroneC2 전용 토큰, 권한 `assets:read`만, 접근 대상은 필요한 지형·이미지 에셋으로 제한. `Assets/CesiumSettings/`는 통째로 `.gitignore` 처리(커밋 금지, `ion.cesium.com.asset`·`CesiumRuntimeSettings.asset`에 토큰 필드가 있음). 토큰은 채팅에 붙여넣지 않고 사용자가 Unity Cesium 패널에서 직접 연결한다. README에는 본인 토큰을 넣어 실행하라는 안내를 쓴다.
  - 씬이 참조하는 `ionServer` GUID는 무시된 `.asset`에 묶여 있어 다른 환경에서는 참조가 비어 있을 수 있다. 새 환경에서는 Cesium 패널에서 토큰을 연결한 뒤 씬을 열어 확인한다.
- 5단계 진행(계획: 5-A 상태 enum 분리 → 5-B 파서 → 5-C 수신기 → 5-D 전환 → 5-E 에미터 → 5-F SITL 1기 → 5-G 문서). 완료: 5-A~5-F(테스트 162건 통과). 5-F: Mission Planner 1.3.83 + SITL(ArduCopter 4.7.2) 1기로 실측 완료. 연결 절차(Mirror 대신 SITL이 직접 송신): SIMULATION 탭 Extra command line `--home=35.563,-83.499,2022,0 --serial1=udpclient:127.0.0.1:14560` → Multirotor → Stable, 파라미터 `MAV2_POSITION`=4(4.7의 `MAVn` 번호는 1부터, MAV2 = SERIAL1; 부팅 때 적용되므로 쓴 뒤 SITL 재시작), `SERIAL1_PROTOCOL`=2. Unity는 `TelemetrySourceSelector` 모드를 Mavlink로 바꿔 Play(씬 저장본은 FakeGps, 확인함). 이동 실측: 100m 이상 비행 시 최대 10.25m/s, 방위가 이동 방향과 일치, 상대고도 30.00m 유지, 지표 위 높이가 `홈 지형 + 상대고도 − 현재 지형`과 일치(샘플러 기준), 수신 끊김 없음(기록 간격 1.2초 초과 없음). GIF `docs/images/stage5-mavlink-sitl.gif`(720×405, 약 13초, 7.9MB) 촬영·커밋 완료. 5-G(devlog·README·requirements·architecture·CLAUDE.md 갱신, 스크린샷 `docs/images/stage5-mavlink-sitl.png`, `stage-5` 태그)까지 완료. push 완료. 계획 파일은 사용자 홈의 `.claude/plans/5-mellow-sparrow.md`.
  - 5-F 진행 기록(2026-10-25): SITL(ArduCopter 4.7.2)에 `--serial1=udpclient:127.0.0.1:14560`을 주면 Mission Planner 없이 Unity로 직접 들어온다(Mirror의 "MAVLink 직렬 출력" 표는 UDP 출력에서 NullReference 오류). 4.7.2에서는 스트림 파라미터가 `MAVn_POSITION`(MAV2 = SERIAL1)이고 **부팅 때 적용되므로 쓴 뒤 SITL을 재시작해야 한다**(재시작 전에는 HEARTBEAT만 와서 샘플 0). 지상 측정: v1 0/v2만, UAV-01이 홈 35.563/-83.499, HomeRelative, 지표 위 약 −0.01m(샘플러 기준), MAV_TYPE 불일치 경고 없음. 30m 이륙(Guided→Arm→Takeoff 30) 후 relAlt 30.00, 월드 높이가 `홈 지형 + 상대고도`와 0.00m 차이(샘플러 기준이라 메시 독립 검증은 아님), 콘솔 error/warning 0건. 이 측정에서 코드는 수정하지 않았다.
  - 5-F에서 발견해 수정한 문제 3건(테스트 162건 통과). 3번: 상세 패널 "상태"가 UXML 기본값 `-`로 남던 문제(Play 중 에셋 새로고침 직후 재현, 라벨이 외부에서 초기화되면 이전 값 캐시 때문에 복구되지 않음)는 `UnitDetailPresenter.ApplyStatusTo`가 라벨의 실제 텍스트와 비교하도록 고쳤다. 상단 데이터 소스 표시줄은 문구가 길어져 우측 패널과 겹쳐서 좌우 패널 사이 영역(left 480/right 680px, 글자 18px)으로 옮겼다. 1·2번:
    1. MAVLink 모드에서 한 번도 수신하지 못한 유닛(TNK-01/02 등)의 3D 오브젝트가 씬 초기 위치에 그대로 보였다(목록에는 없음). `DroneView`가 `Awake`에서 자식 `Renderer`(모델·마커·경로선 포함)를 모두 끄고 첫 `SetTarget`에서 켠다. 이미 꺼져 있던 렌더러는 켜지 않는다. 마커 모양과 경로선은 원래 첫 샘플 전에는 비어 있었으나 이제 같은 규칙으로 묶였다. 두절 유닛은 마지막 위치에 계속 보인다. (EditMode에서는 `AddComponent`가 `Awake`를 부르지 않아 테스트가 리플렉션으로 호출한다.)
    2. 파서의 `Rejected`가 미지원과 손상을 구분하지 못했다. 이제 `Unsupported`(해석하지 않는 메시지 ID, 정상 트래픽)와 `Corrupted`(= `Truncated`+`BadFlags`+`BadLength`+`BadCrc`)로 나누고 `Rejected`는 합계로 유지한다. 미지원 메시지는 헤더 길이만큼(v2 서명 13바이트 포함) 통째로 건너뛰어 페이로드 안 0xFD/0xFE를 새 후보로 세지 않는다. 헤더가 데이터그램을 넘는 길이를 주장하면 미지원이 아니라 잘림(손상)이다. 상태 표시줄은 "수신 N / 손상 M (미지원 K)"이다. 한계: 미지원으로 판정한 헤더는 CRC를 검증할 수 없어(CRC_EXTRA 모름) 우연히 맞는 노이즈를 미지원으로 셀 수 있다.
    - 수정 후 Play 측정(SITL 켠 상태, 에디터): 261 데이터그램 기준 v1 0 / v2 143, 미지원 118, 손상 0, 샘플 112, 미등록 sysid 0. 수신하지 못한 4기는 렌더러 0개 활성, 목록은 1기.
  - 5-A: 소스 보고용 `ReportedStatus`(Normal/Warning)와 표시용 `UnitStatus`(+Lost)를 분리했다. `TelemetrySample.Status`가 `ReportedStatus`이며 `UnitStateStore`가 변환하고 Lost는 `Tick`만 붙인다. 표시 코드는 수정하지 않았다. (devlog 설계 결정 후보 1)
  - 5-B: `Assets/Scripts/DataSource/Mavlink/`에 CRC·프레임·파서·메시지 디코더(HEARTBEAT, GLOBAL_POSITION_INT)를 두었다. 테스트 112건 통과(신규 24건).
  - 설계 결정 후보 2: **v1·v2를 모두 파싱하고 버전별 카운터(`V1Frames`/`V2Frames`/`Rejected`)를 둔다.** 우리는 송신하지 않아 상대(ArduPilot 등)의 프로토콜 버전을 협상으로 바꿀 수 없으므로 수신 쪽이 유연해야 한다. SITL 연동 때 어떤 버전이 오는지·얼마나 버려지는지 디버깅에 쓴다. v2의 끝 0바이트 생략(truncation)은 메시지 정의 길이로 0 복원한다(잘린 프레임 테스트 포함).
  - 5-C: `MavlinkUdpListener`(루프백 UDP 수신 전용 스레드 + 상한 있는 큐), `MavlinkTelemetryRouter`(순수 로직: sysid 테이블 조회·상태·고도·방위), `MavlinkUnitTable`(ScriptableObject, sysid·unitId·종류·홈 위경도), `MavlinkReceiver`(씬에 하나, 메인 스레드에서 큐를 비움), `MavlinkUnitSource`(sysid별 `ITelemetrySource`). 테이블에 없는 sysid는 무시하고 `UnknownSysid`로 센다. 테이블과 HEARTBEAT의 종류가 다르면 테이블을 따르고 sysid마다 경고 1회. 테이블 에셋과 씬 배치는 5-D·5-E에서 한다.
  - 5-D: 고도 기준을 `TelemetrySample.AltitudeReference`(필수 인자)로 옮겼다. `AltitudeMode`는 `DataSource` 네임스페이스이며 Absolute=0, AboveGround=1, ClampToGround=2, HomeRelative=3(숫자 유지). `HomeRelative`는 샘플이 싣고 온 홈 위경도의 지형 높이(`AltitudeResolver`가 홈이 바뀔 때만 재조회) + 상대고도이고, 홈이 NaN이면 바인더가 버리고 유닛당 한 번만 경고한다. 라우터는 고도의 의미만 선언하므로 `MavlinkReceiver`는 Coordinate 계층을 모른다(소스·변환기·뷰는 바인더에서만 만난다는 원칙 유지). 바인더의 직렬화된 `altitudeMode` 필드는 없어졌고 값은 프리팹의 `FakeGpsSource.altitudeReference`(Drone=AboveGround, Tank=ClampToGround)와 시나리오 JSON의 선택 필드 `altitudeMode`(생략 시 종류별 기본값)로 옮겼다.
  - 5-D 소스 전환(FR-015): `Core/TelemetrySourceSelector`(씬에 하나, `Stage3Control`에 배치, 기본 `FakeGps`)가 시작 시 바인더별로 `FakeGpsSource`/`MavlinkUnitSource` 중 하나만 켜고 `UnitTelemetryBinder.SetSource`로 연결한다. MAVLink 모드에서만 `MavlinkReceiver`(씬에서 비활성)를 켠다. 설정은 `Assets/Config/MavlinkUnits.asset`(sysid 1~5 = UAV-01~03, TNK-01~02, 홈 35.563/-83.499, 포트 14560). 유닛 프리팹에 `MavlinkUnitSource`(비활성)를 추가했고 씬 인스턴스에 수신기·sysid를 연결했다. Play에서 FakeGps 모드 정상, 모드 전환 후 합성 프레임 5개로 UAV-01이 이동(HomeRelative 80m, 카운터 v2=5)하고, 되돌리면 소켓이 닫히고 유닛이 복구됨을 확인했다.
  - 설계 결정 후보 4: **MAVLink 모드인데 수신기가 없으면 가짜 GPS로 대체하지 않는다.** 관제 화면에서 가짜 데이터를 실제처럼 보여 주는 것이 가장 위험하므로, 모드를 유지하고 오류를 출력해 데이터가 오지 않게(두절로 남게) 한다. 한계: 저장소는 첫 샘플에서 유닛을 등록하므로, 한 번도 수신하지 못한 유닛은 목록에 나타나지 않는다(두절로 표시하려면 테이블 기반 사전 등록이 필요, 미구현).
  - 지상 유닛 지형 접촉 한계(5-E 후 측정, 6단계 후보): 전차(ClampToGround) 하부와 렌더된 지형 메시 사이를 레이캐스트로 쟀더니 격자 0.0002°에서 TNK-01 −2.6~+3.4m, TNK-02 −3.7~+2.4m(34점씩, 양방향 편차, 전차 하부는 pivot과 거의 같은 높이). 격자를 0.00005°로 줄인 실험은 평균 |오차| 1.47→0.65m(TNK-01), 2.18→1.50m(TNK-02)로 일부 개선되고 캐시 노드는 70초 기준 2,195→9,171(약 4.2배)이었으나 TNK-02는 −3.5~+3.8m가 남았다. 남은 오차는 "가장 정밀한 높이"와 현재 로드된 LOD 메시의 차이이므로 보간 간격으로는 해결되지 않는다. 실험은 커밋하지 않고 되돌렸다(`gridCellDeg` 0.0002 유지). 해결안 A(지상 유닛에 한해 물리 메시 레이캐스트로 높이를 정하고 타일이 없으면 샘플러로 대체)는 콜라이더 비용·LOD 전환 시 높이 튐을 평가해야 하므로 5단계에서 하지 않고 6단계 후보로 남긴다. 현재 한계: 지상 유닛은 지형에서 약 ±4m 뜨거나 파묻혀 보일 수 있다.
  - devlog(5-G)에 넣을 것: Mission Planner 1.3.83 MSI(`firmware.ardupilot.org/Tools/MissionPlanner/MissionPlanner-latest.msi`, 119,841,133바이트, SHA-256 `B3AC2F65196629514B2059344DA37C1E9CCD3FE85A46C3FF88BA67A55B65E4D5`)는 공식 `checksums.txt`에 MSI 항목이 없고 Authenticode 서명도 없어 암호학적 대조를 하지 못했다. 대신 VirusTotal에서 해당 SHA-256을 조회해 0/54 탐지를 확인했다(사용자 확인, 2026-10-14). 설치 경로 `C:\Program Files (x86)\Mission Planner`, 설치 파일은 저장소 밖 `C:\UnityProjects\DroneC2-tools\downloads`.
  - 5-E: `tools/mavlink_emitter.py`(pymavlink 2.4.50 고정, 저장소 밖 venv `C:\UnityProjects\DroneC2-tools\venv`)로 `stage3-demo.json` 5기를 sysid 1~5로 재생. 상단 데이터 소스 표시줄(`DataSourceStatusPresenter`, "가상 GPS(시뮬레이션)" / "MAVLink · 127.0.0.1:14560 · 수신 N / 거부 M" / "수신기 없음" 등)을 추가했다. 사전 등록은 하지 않기로 했다(후보 4의 한계 유지). 실측 결과는 아래 "5-E 실측" 참고.
  - 5-E 실측(Play, MAVLink 모드, 에디터): 75초 재생, 데이터그램 4037개(v2 4037/v1 0/거부 0, 위치 샘플 3670), 상태 전이가 시나리오와 일치(TNK-02 경고 30→55초, UAV-02 경고 40→60초, UAV-03 두절 62→복구 68초(송신 중단 60~68초), 에미터 종료 후 2초 뒤 5기 모두 두절). HomeRelative 독립 확인: 드론을 홈에 relative_alt=0으로 두었을 때 샘플러 기준 지표 위 0.00m, 런타임으로 켠 물리 메시(레이캐스트) 기준 +3.41m. 12점 조사에서 샘플러와 메시의 차이는 −4.1~+0.3m(샘플러가 대체로 높음, 격자 보간 오차). 측정 때만 `createPhysicsMeshes`를 런타임으로 켰고 씬에는 저장하지 않았다(기본 False). `Body` 콜라이더(드론 자신)가 먼저 맞아 `RaycastAll`로 타일셋 하위만 골랐다.
  - (참고) 5-E에 추가할 확인: HomeRelative 독립 검증. 에미터로 드론을 홈 위치에 relative_alt=0으로 두면 홈 지점 지표 메시 위 0m 근처여야 한다(가능하면 레이캐스트 등 샘플러와 독립인 측정). 5-D의 Play 높이 실측(60/80/100·0)은 리졸버와 같은 샘플러 기준이라 메시 대비 검증이 아니다.
  - 설계 결정 후보 3: 고도는 `홈 지형 높이(sysid 설정 테이블의 홈 위경도로 조회) + relative_alt`를 절대 고도로 쓴다. `HOME_POSITION` 요청과 첫 샘플을 홈으로 가정하는 방식은 쓰지 않는다(송신 불필요, 비행 중 접속 시에도 정확).
- 4단계: `Stage3Control` 씬에 유닛 카메라 패널을 더했다(씬은 새로 만들지 않음). `UnitCameraRig`(View, 유닛 자식 카메라 + `RenderTexture`, 선택·고정된 유닛만 렌더), `CesiumUnitCameraLink`(렌더 중인 카메라만 Cesium 타일 선택에 등록), `UnitCameraPolicy`(렌더 규칙), `UnitStateStore.Pin/Unpin`(고정 1기), `CameraPanelPresenter`(선택·고정 패널). `Scenario`가 시나리오 JSON(FR-004)을 읽어 `FakeGpsSource`에 적용하며 `Assets/Scenarios/stage3-demo.json`이 3단계 5기를 옮긴 파일이다.
  - EditMode 테스트 88건 통과. 카메라 1대 약 0.4ms. Cesium 등록 시 타일 렌더러 95→149(1대)→351(2대), 2대 등록 직후 262ms 정지 1회(에디터 측정). 전차 카메라는 포신이 시야를 가려 차체 앞면 앞(높이 2m, 전방 6.3m)에 둔다.
  - 알려진 한계: 두절 오버레이 화면 미캡처, 고정/해제 버튼 실제 클릭 미검증, 고정은 1기까지.
- 3단계 문서: `docs/devlog.md` 3단계 정리(스크린샷 `docs/images/stage3-control-ui.png`, GIF `stage3-control-ui.gif`), `README.md`·`docs/requirements.md`·`docs/architecture.md` 갱신 완료. 이후 단계가 끝날 때 README의 진행 상황·맨 위 GIF를 갱신한다.
- 단계 작업은 작은 단위로 나눠 진행하며, 각 단위마다 씬 저장 후 커밋한다.

### 남은 할 일

- [x] `stage-4` 태그와 4-C 이후 커밋의 푸시(푸시 전 토큰·`Assets/CesiumSettings` 검사)
- [x] `stage-5` 태그와 5단계 커밋의 푸시(푸시 전 토큰·`Assets/CesiumSettings` 검사 0건, 사용자가 GitHub 렌더링 확인 후 태그 푸시)
- [x] 6단계 ①(소스 전환 시 초기화) 구현·검증·문서 정리(커밋 완료, 푸시 전)
- [x] 6단계 ②(전차 경로 OSM 도로, 경로 길이 기반 이동, 측정, `.gitattributes`) 구현·측정·문서 정리·`stage-6` 로컬 태그(푸시 전)
- [x] 저장소 공개 전 **git 전체 기록 검사**(2026-10-26, 읽기 전용): 84개 커밋(모든 refs·태그 객체)의 텍스트 diff(삭제된 줄 포함)와 전체 객체 482 blob의 원본 바이트에서 검사했다. JWT `eyJ…` 0건, `Assets/CesiumSettings` 경로가 추적된 적 없음, `_ionAccessToken:` 필드는 값이 모두 비어 있음, API 키·GitHub 토큰·개인키·`access_token=` 패턴 0건, 의심 파일명 없음, 로컬 Windows 계정명·`C:\Users\<이름>` 경로·이메일·사설 IP 0건, 작성자·커미터 84건 모두 GitHub noreply. 원격 refs는 `main`과 `stage-1~5` 태그뿐. 남은 항목: ① `ProjectSettings.asset`의 `organizationId: hsb020524`(GitHub 사용자명과 같은 공개 식별자, 그대로 둠) ② Unity 템플릿 `Layout.wlt`의 제3자 로컬 경로는 기록 재작성 없이 현재 버전에서 템플릿 파일(`Assets/TutorialInfo`, `Assets/Readme.asset`)을 삭제해 처리했다(과거 커밋에는 남음, 참조 없음 확인, 컴파일 오류 0건·테스트 175건 통과) ③ devlog의 토큰 재발급 기록은 보안 사고 대응 사례로 남기기로 결정. `.gitattributes`의 `*.wlt` 규칙은 삭제하지 않는다.
- [x] 저장소 공개 전 **docs 이미지 전수 검사**(2026-10-26, 읽기 전용): `docs/images`의 PNG 5개·GIF 5개를 검사했고 문제가 없었다. 메타데이터: PNG는 IHDR·IDAT·IEND 청크뿐(텍스트·EXIF·ICC 없음), GIF는 Pillow로 파싱해 코멘트 없음. 원본 바이트에서 `hsb02`·`C:\Users`·`/Users/`·JWT `eyJ…`·이메일·`access_token`·`Bearer` 0건. 육안: PNG 5장 전체와 GIF 5개의 표본 프레임(stage1·2는 6장, stage3·4·5는 12장씩)에서 Game 뷰 화면만 보이고 에디터 창·콘솔·Inspector·Cesium 패널·계정명·토큰은 없었다(화면의 Cesium 로고·데이터 출처 문구는 정상적인 출처 표시, 좌표는 가상 시연 지역 값). 기록에는 이미지마다 blob이 하나뿐이라 이전 버전이 남아 있지 않고, 작업 폴더 파일은 커밋된 blob과 같다. docs 밖 이미지(`GroundGrid.png`, 삭제한 템플릿의 `URP.png`)도 개인정보 없음. 한계: GIF는 전체 프레임이 아니라 표본이며(녹화 도구가 Game 뷰 프레임만 저장하므로 바탕화면·다른 창이 찍힐 경로는 구조상 없음) 화면 글자는 육안으로만 읽었다(OCR 없음). 검사용 Pillow는 저장소 밖 임시 폴더에만 설치했고 PC 전역·사용자·프로젝트 venv에는 설치되지 않았음을 확인했다. 이미지를 새로 추가·교체하면 다시 검사한다.
- [x] 저장소 공개 전: 새 폴더에 클론해서(사용자가 README만 보고 직접 수행, 원본 Unity는 닫고 진행, Claude는 막힌 곳 기록·README 수정안 정리) README 실행 방법대로 동작하는지 확인 (Cesium ion 토큰 연결 후 씬 열기, `ionServer` 참조가 비어 있지 않은지 포함)
  - 1차 클론 테스트 완료(2026-10-26, `C:\UnityProjects\DroneC2-clonetest`): 1단계 씬(토큰 없이)·2단계 씬(Cesium 연결 후 지형)·3단계 씬(목록 5기, 카메라, 카메라 패널, 고정)·MAVLink 모드(에미터 5기, 수신 4037 / 손상 0, 종료 후 5기 두절·카메라 "신호 없음") 모두 동작. Unity 첫 열기 약 5분, Safe Mode 없음. `ionServer` 참조 문제는 실제로 생기지 않았다. **그 폴더의 `Assets/CesiumSettings`에 토큰이 있으므로 건드리지 않는다**(README 수정 후 재확인용으로 남김).
  - 확인하지 못한 것: venv 새로 만들기(기존 venv 사용), 드라이버 설치 거부 시 SITL 동작(실제로는 설치함).
  - [x] README 수정 적용(2026-10-26, 푸시 전, 클론 폴더에서 재확인 남음). 추가 결정: `docs/requirements.md`의 `~`도 수정, MCP for Unity는 `Packages/manifest.json`을 `#aa5fc638d623f56178d50329d9ad8c541a57fe66`(= 태그 v10.3.0의 커밋, 이전 `packages-lock.json` 해시와 동일)로 고정하고 README에 알림 문단 추가(컴파일 오류 0건, 테스트 175건 통과). 새 venv 확인에서 `tools/requirements.txt`의 한글 주석이 한국어 Windows(cp949)에서 pip 설치를 실패시키는 문제를 발견해 ASCII 주석으로 고침(수정 후 임시 venv에서 설치·에미터 실행 성공, venv 삭제함). 아래는 원래 수정안 목록이다: ① 클론(`git clone`)·Unity Hub "Add project from disk" 안내 추가 ② `~` 범위 표기가 취소선으로 깨짐(README 137행 `UAV-01~03 = 1~3, TNK-01~02 = 4~5`; 156행 `oem49.inf`~`oem72.inf`; `docs/requirements.md` 146행·`docs/devlog.md`도 `~` 사용) → `–`/"부터 … 까지"로 교체 ③ "드라이버 설치를 거부해도 SITL은 동작" 삭제(확인하지 않음, 설치한 상태로만 확인) ④ MCP for Unity가 프로젝트를 열면 자동 실행되어 사용자 AI 클라이언트 설정을 바꿈(`StartupConfigRewrite`, `EditorPrefs` `MCPForUnity.AutoRegisterEnabled` 기본 true, 로그 "refreshed N client config(s)") → README에 알리거나 제외 방법 안내 ⑤ `Assets/CesiumSettings` 자동 생성 폴더는 `.gitignore`(`/[Aa]ssets/CesiumSettings*`)로 제외됨을 `git check-ignore`로 확인함, README에 한 줄 설명 ⑥ `ionServer` 안내를 "보통은 연결·토큰 지정만으로 동작, 지형이 안 보이면 확인" 순서로 ⑦ "Cesium 패널"을 "메뉴 Cesium → Cesium → Connect to Cesium ion → Token → Use an existing token"처럼 구체적으로 ⑧ 토큰 노출 주의에 "Console에서 info를 숨겨도 하단 상태 표시줄에 마지막 로그가 보임" 추가 ⑨ venv 새로 만들기는 미확인임을 확인 범위에 반영

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 평면 위 가짜 GPS 드론 이동·경로 표시 | 완료 |
| 2 | Cesium 실제 지형 | 완료 |
| 3 | 다중 유닛 + 관제 UI | 완료 |
| 4 | 카메라 패널 | 완료 |
| 5 | MAVLink 시뮬레이터 연동 | 완료 |

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
- 푸시 전에 올라갈 커밋에 토큰 형태 문자열(JWT `eyJ…`)과 `Assets/CesiumSettings` 경로가 없는지 검사한다. 최종 diff만 보지 말고 **커밋별 패치**(`git log -p origin/main..HEAD`의 추가 줄)에서 로컬 계정명·`C:\Users` 경로·`AppData`도 검사한다(중간 커밋에 들어갔다 지운 값도 기록에 남아 공개된다. 6단계에서 실제로 한 번 발견해 백업 브랜치를 만든 뒤 푸시 전 커밋만 고쳐 썼다). 또 `git diff origin/main..HEAD -- Packages/ | grep '^+' | grep -c unity-mcp`가 0인지 확인한다(내장 패키지의 lock 변경이 섞여 올라가는 것 방지; 제거된 `-` 줄은 정상이고, `..HEAD` 없이 `origin/main`만 쓰면 커밋 안 한 로컬 lock 변경이 `+`로 섞여 나온다). 토큰이 대화나 로그에 노출되면 재발급한다.
- 자세한 내용은 `docs/requirements.md` 6절을 따른다.

## 기타

- `.gitattributes`는 `*.wlt`(Unity 레이아웃 파일)를 GitHub 언어 통계에서 제외한다. 삭제하지 않는다.
