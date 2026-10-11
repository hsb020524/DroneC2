# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 요약

DroneC2는 포트폴리오용 Unity 프로젝트로, 드론·전차의 GPS 위치와 작전 상황을 3D로 보여주는 관제 시스템이다. 세부 내용은 `docs/`를 참고한다.

- `docs/requirements.md` — 요구사항(FR/NFR ID), 5단계 로드맵, 범위 제외, 보안 원칙
- `docs/architecture.md` — 데이터 소스 → 좌표 변환 → 표시 3계층 구조와 Mermaid 다이어그램

계층 간 의존은 단방향이다. 표시 계층은 데이터가 가짜 GPS인지 MAVLink인지 알지 못해야 하며, 데이터 소스는 공통 인터페이스(예정 이름 `ITelemetrySource`) 뒤에 둔다.

## 현재 진행 상황

> 단계가 끝날 때마다 이 섹션을 갱신한다.

- 현재 단계: 1단계 진행 중 — 작업 단위 1~6 완료, 7번(마무리)만 남음
- 완료: 요구사항 정의서·구조도 초안(v0.1), 스캐폴딩(인터페이스·asmdef), `LocalTangentPlaneConverter`+EditMode 테스트 7건 통과, 씬 환경(평면·드론 플레이스홀더), `FakeGpsSource`·`DroneView`·`UnitTelemetryBinder` 배선 후 Play 모드에서 웨이포인트 순환·방위 회전 확인, `PathTrail` 경로선(LineRenderer, 2m 간격·최대 2000점) 확인, `FollowCamera`(드론 뒤·위 오프셋, 방위 보간) 확인
- 다음: 작업 단위 7 마무리 — 1단계 완료 기준 점검 후 진행 상황·docs 갱신
- 계획 문서: 1단계 작업 단위는 7개로 나눠 진행하며, 각 단위마다 씬 저장 후 커밋한다.

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 평면 위 가짜 GPS 드론 이동·경로 표시 | 진행 중 (6/7) |
| 2 | Cesium 실제 지형 | 예정 |
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

## Unity 규칙

- Unity 조작(씬, 오브젝트, 컴포넌트, 에셋 등)은 MCP for Unity(UnityMCP 도구)로 한다. 스크립트 수정 후에는 `read_console`로 컴파일 오류를 확인한다.
- 스크립트는 `Assets/Scripts/<역할>/` 아래에 역할별 폴더로 둔다. (예: `DataSource`, `Coordinate`, `View`, `UI`)
- 환경: Unity `6000.6.5f1`, URP 17.6.0, Input System 1.20.0, Test Framework 1.8.0.
- `*.csproj`, `*.slnx`와 `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 Unity가 생성하므로 수정하지 않는다.
- 빌드 스크립트는 없다. 테스트는 Unity batchmode로 실행한다. (아직 테스트 없음)

```powershell
# -testFilter "Namespace.Class.Method" 로 단일 테스트 실행
& "<UnityEditorPath>\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

## 보안 원칙

- 실제 군사 데이터와 실제 시설 좌표를 사용하지 않는다. 위치·경로·식별자는 가상 값 또는 공개 데이터만 쓴다.
- 수신·표시 전용이며 유닛에 명령을 보내는 경로를 만들지 않는다.
- 자세한 내용은 `docs/requirements.md` 6절을 따른다.

## 기타

- `.gitattributes`는 `*.wlt`(Unity 레이아웃 파일)를 GitHub 언어 통계에서 제외한다. 삭제하지 않는다.
