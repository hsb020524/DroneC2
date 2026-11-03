# 실행 방법

처음으로 돌아가려면 [README](../README.md)를 보세요.

## 1. 열기

1. 저장소를 내려받습니다: `git clone <저장소 URL>`
2. Unity Hub에서 Unity `6000.6.5f1`을 설치합니다.
3. Unity Hub의 **Add → Add project from disk**로 클론한 폴더를 추가하고 엽니다. 첫 열기는 패키지 해석과 임포트 때문에 5분 안팎 걸릴 수 있습니다(테스트 PC 기준).
4. 패키지는 `Packages/manifest.json`의 Cesium 레지스트리와 Git 의존성에서 자동으로 받아옵니다.

> **개발 도구 — MCP for Unity(선택):** 이 저장소는 [MCP for Unity](https://github.com/CoplayDev/unity-mcp)에 의존하지 않으며, 프로젝트 코드도 이 패키지를 쓰지 않습니다. 개발 중 AI 클라이언트로 에디터를 제어하려면 `Packages/manifest.json`의 `dependencies`에 아래 줄을 직접 추가하세요(개발에는 10.3.0, 커밋 `aa5fc63`을 썼습니다).
>
> ```json
> "com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#aa5fc638d623f56178d50329d9ad8c541a57fe66",
> ```
>
> 이 패키지는 열면 에디터에서 자동으로 실행되고 PC에 설치된 AI 클라이언트의 설정 파일을 갱신할 수 있습니다(콘솔의 `refreshed N client config(s)` 로그). 또한 MCP 서버를 켜지 않은 채 MCP 창을 열어 두면 Play 중 에디터가 주기적으로 약 0.3초씩 멈출 수 있으니, 서버를 쓰지 않을 때는 창을 닫으세요.
>
> **Cesium 경고:** 콘솔에 Cesium for Unity의 `Signature Missing` 경고가 보이면 정상입니다. 동작에는 영향이 없습니다.

## 2. 1단계 씬 (토큰 불필요)

`Assets/Scenes/SampleScene.unity`를 열고 Play하면 평면 위에서 가짜 GPS 드론이 움직입니다.

## 3. 2단계 씬 (Cesium ion 토큰 필요)

실제 지형은 Cesium ion에서 받으므로 **본인 Cesium ion 계정과 토큰으로 실행**해야 합니다. 토큰은 저장소에 포함되어 있지 않습니다.

1. [Cesium ion](https://ion.cesium.com)에 가입하고 Assets에서 **Cesium World Terrain**과 **Bing Maps Aerial**을 My Assets에 추가합니다.
2. Access Tokens에서 새 토큰을 만듭니다. 권한은 `assets:read`만, 접근 대상은 위 두 에셋으로 제한하는 것을 권장합니다.
3. Unity 메뉴 **Cesium → Cesium**으로 패널을 열고 **Connect to Cesium ion**으로 로그인한 뒤, **Token → Use an existing token**에서 위에서 만든 토큰을 지정합니다. (Cesium ion Assets 창과는 다른 창입니다.) 토큰은 Unity 안에서만 입력하세요.
4. `Assets/Scenes/Stage2Cesium.unity`를 열고 Play합니다. 지형 타일이 로드되는 데 잠시 걸립니다.

참고:
- 토큰이 저장되는 `Assets/CesiumSettings/` 폴더는 Cesium 패키지가 처음 열 때 자동으로 만들며, `.gitignore`에 포함되어 커밋되지 않습니다.
- 보통은 위 3번의 연결·토큰 지정만으로 지형이 나옵니다. 지형이 보이지 않으면 Cesium 패널의 연결 상태와 씬의 지형 오브젝트(`Cesium World Terrain`)의 Ion Server 지정을 확인하세요.
- 화면의 Cesium·데이터 출처 크레딧은 이용 약관상 유지해야 합니다.
- 토큰이 로그나 채팅 등에 노출되면 Cesium ion에서 폐기하고 재발급하세요. 에디터 콘솔의 `info` 로그에는 토큰이 포함된 URL이 출력될 수 있습니다. Console 창에서 Info를 숨겨도 에디터 하단 상태 표시줄에는 마지막 로그가 그대로 보이므로, 화면 공유·캡처 전에 확인하세요.

## 4. 3단계 씬 (Cesium ion 토큰 필요)

위 3절과 같은 방법으로 토큰을 연결한 뒤 `Assets/Scenes/Stage3Control.unity`를 열고 Play합니다. 왼쪽 목록에서 유닛을 클릭하면 카메라가 따라가고, `전체 보기` 버튼이나 Esc로 선택을 해제합니다. 유닛 중 일부는 시연용으로 일정 시각에 경고 또는 통신 두절 상태가 됩니다. 4단계부터 같은 씬에서 선택한 유닛의 카메라 패널이 오른쪽 아래에 나타나고, `고정` 버튼으로 왼쪽 아래 패널에 유닛 하나를 남길 수 있습니다.

## 5. MAVLink 모드 (5단계)

기본은 가상 GPS(시뮬레이션)입니다. 화면 위 가운데의 표시줄이 현재 데이터 소스와 수신 상태를 항상 보여 줍니다. MAVLink로 바꾸려면 `Stage3Control` 씬의 `TelemetrySourceSelector`에서 Mode를 `Mavlink`로 바꾼 뒤 Play합니다. Play 중에 바꿔도(Inspector에서 값을 바꾸거나 `SetMode` 호출) 이전 소스의 유닛·3D 표시·경로선·선택이 모두 비워진 뒤 새 소스의 유닛만 나타나므로 가짜와 실제 데이터가 섞이지 않습니다. 가짜 GPS로 돌아오면 시나리오를 처음부터 다시 시작합니다. MAVLink 모드에서 한 번도 수신하지 못한 유닛은 목록에 나타나지 않습니다. 이때 수신기(`MavlinkReceiver`)가 `127.0.0.1:14560`(UDP)에서 수신만 합니다. 유닛에 명령을 보내지 않습니다.

데이터를 보내는 가상 에미터는 Python 도구이며 `pymavlink`(LGPL-3.0)를 별도 프로세스로만 씁니다. 가상환경은 저장소 밖에 만듭니다.

새 가상환경을 만들어 `pip install -r tools/requirements.txt`가 되고 에미터가 실행되는 것까지 확인했습니다(Windows, Python 3.12.6). 한국어 Windows에서는 `requirements.txt`에 UTF-8 한글이 있으면 pip이 cp949로 읽다 실패하므로 이 파일은 ASCII로만 씁니다.

```powershell
# 한 번만: 저장소 밖에 가상환경을 만들고 버전을 고정한 의존성을 설치한다.
python -m venv C:\UnityProjects\DroneC2-tools\venv
C:\UnityProjects\DroneC2-tools\venv\Scripts\python.exe -m pip install -r tools/requirements.txt

# Unity가 MAVLink 모드로 Play 중일 때 저장소 루트에서 실행한다. (5기, 시나리오 stage3-demo.json(드론)·osm-roads-tnk.json(전차) 재생)
C:\UnityProjects\DroneC2-tools\venv\Scripts\python.exe tools/mavlink_emitter.py --duration 75
```

- 에미터 옵션: `--mavlink1`(v1 프레임), `--only-sysids`, `--hold-home-sysids`(홈에 정지), `--port` 등. 전체는 `--help`로 확인합니다.
- sysid와 유닛 id, 홈 위치는 `Assets/Config/MavlinkUnits.asset`의 테이블이 정하고, 에미터는 같은 값(UAV-01부터 03까지 = sysid 1–3, TNK-01·02 = sysid 4–5, 홈 35.563°N 83.499°W)을 씁니다.
- 가상환경을 지우려면 `C:\UnityProjects\DroneC2-tools\venv` 폴더를 삭제하면 됩니다(약 110MB).

### SITL로 연결하기 (Mission Planner, 선택)

에미터 대신 실제 ArduPilot SITL을 연결하려면 Mission Planner의 SIMULATION 탭을 씁니다. 5단계에서는 ArduCopter 4.7.2로 확인했습니다. Mission Planner의 MAVLink 포워딩(UDP 출력)은 이 환경에서 오류가 나서 쓰지 않고, SITL이 Unity로 직접 보내게 했습니다.

1. SIMULATION 탭의 **Extra command line**에 `--home=35.563,-83.499,2022,0 --serial1=udpclient:127.0.0.1:14560`을 넣고 Multirotor → Stable로 시작합니다.
2. CONFIG → Full Parameter List에서 `SERIAL1_PROTOCOL`=2, `MAV2_POSITION`=4를 씁니다. 4.7에서는 스트림 설정 이름이 `SRn_*`가 아니라 `MAVn_*`이고 번호가 1부터라 `MAV2`가 SERIAL1입니다. **이 설정은 부팅할 때 적용되므로 쓴 뒤 SITL을 재시작해야 합니다.** 재시작 전에는 HEARTBEAT만 와서 유닛이 나타나지 않습니다.
3. Unity에서 모드를 `Mavlink`로 바꾸고 Play하면 UAV-01(sysid 1)이 나타납니다. Guided → Arm → Takeoff, "이곳으로 비행"으로 움직일 수 있습니다.

### Mission Planner 설치와 제거

실제 시뮬레이터(ArduPilot SITL) 연동을 확인하려면 [Mission Planner](https://ardupilot.org/planner/docs/mission-planner-installation.html)를 씁니다. 5단계 개발에서는 1.3.83을 사용했습니다. 에미터만으로도 MAVLink 모드는 확인할 수 있어 필수가 아닙니다.

- 설치 파일: `https://firmware.ardupilot.org/Tools/MissionPlanner/MissionPlanner-latest.msi` (공식 디렉터리). 설치 파일에 서명이 없고 공식 체크섬 목록에도 없으므로, 내려받은 뒤 SHA-256을 직접 확인하세요. 개발에 쓴 파일은 `B3AC2F65196629514B2059344DA37C1E9CCD3FE85A46C3FF88BA67A55B65E4D5`이며 VirusTotal에서 0/54 탐지였습니다.
- 설치는 관리자 권한이 필요하고, 설치 중 **USB 드라이버 설치 창**이 뜹니다. 드라이버는 비행 컨트롤러 USB 연결용이라 이 프로젝트의 시뮬레이션과는 직접 관련이 없어 보입니다. 개발 중에는 드라이버를 설치한 상태로 SITL 연동을 확인했고, 설치를 거부했을 때의 동작은 확인하지 않았습니다.
- 설치 파일은 저장소 밖(`C:\UnityProjects\DroneC2-tools\downloads`)에 두었습니다. 지우려면 이 폴더(또는 `C:\UnityProjects\DroneC2-tools` 전체)를 삭제합니다.
- **제거(프로그램):** Windows `설정 > 앱 > 설치된 앱`에서 `Mission Planner`를 제거합니다. 설치 경로는 `C:\Program Files (x86)\Mission Planner`입니다.
- **제거(드라이버):** 프로그램을 지워도 USB 드라이버 패키지는 남을 수 있습니다. 설치 때 게시자 서명이 `Michael Oborne`인 패키지가 21개(`oem49.inf`–`oem72.inf` 사이, 번호는 PC마다 다름) 추가되었습니다. 관리자 PowerShell에서 먼저 목록을 확인한 뒤 지웁니다.

  ```powershell
  # 1) 서명자가 Michael Oborne인 드라이버 패키지 확인 (읽기 전용)
  pnputil /enum-drivers | Out-String | ForEach-Object { $_ -split "(\r?\n){2,}" } |
    Where-Object { $_ -match 'Michael Oborne' }

  # 2) 확인한 항목의 Published Name(oemNN.inf)마다 제거 (관리자 권한)
  pnputil /delete-driver oem49.inf /uninstall
  ```

## 6. 테스트

Unity의 Test Runner(`Window > General > Test Runner`)에서 EditMode를 실행합니다. 184건이 모두 통과해야 합니다.
