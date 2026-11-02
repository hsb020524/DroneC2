# OpenStreetMap 도로 데이터 (전차 경로용)

전차(TNK-01/02) 경로는 OpenStreetMap의 실제 도로 선에서 만든다. 이 폴더의 데이터는 ODbL 라이선스를 따른다.

## 출처 표기 (Attribution)

- 데이터: **© OpenStreetMap contributors**, [Open Database License(ODbL) 1.0](https://opendatacommons.org/licenses/odbl/1-0/)으로 제공된다.
- 저작권·라이선스 안내: <https://www.openstreetmap.org/copyright>
- 이 폴더의 `roads-raw.json`은 Overpass API 응답 원본이고, 여기서 만든 `Assets/Scenarios/osm-roads-tnk.json`은 이 데이터에서 파생한 데이터베이스다(Derived Database). 두 파일은 ODbL 조건(출처 표기, 같은 조건으로 공유)을 따른다. 프로젝트의 나머지 코드·시나리오와 라이선스를 섞지 않도록 경로 파일을 분리해 두었다.
- 사용한 도로: `Kuwohi Access Road`(OSM way 19607229, 출처 태그 NPS). 시나리오에는 이 도로 위 두 구간의 좌표만 들어 있다.
- 시연 속성은 가상이다. 전차 유닛은 실제 차량이나 군사 자료가 아니며, 공원 도로 좌표는 공개 지도 데이터다.

## 파일

| 파일 | 설명 |
|---|---|
| `roads.overpassql` | Overpass 질의문. 기준점 주변 `35.545,-83.520,35.580,-83.470`의 `highway` 도로 |
| `roads-raw.json` | 위 질의의 응답 원본(105KB). 기준 시각 `2026-11-02T14:16:09Z` |
| `tank-routes.json` | 전차별 도로 구간(도로 시작점에서 몇 m ~ 몇 m), 속도, 단순화 오차, 결함 시각 |

## 다시 받기

OSM 데이터는 계속 바뀌므로 다시 받으면 결과가 달라질 수 있다. 재현할 때는 저장된 `roads-raw.json`을 쓴다. 새로 받으려면 이 폴더에서 실행한다(공개 서버가 혼잡하면 502가 나오니 잠시 뒤 다시 시도한다).

```sh
curl -sS -A "DroneC2-portfolio/0.1" --data-urlencode "data@roads.overpassql" \
  https://overpass-api.de/api/interpreter -o roads-raw.json
```

## 경로 만들기

```sh
python tools/osm_to_scenario.py
```

기본 입력은 이 폴더의 `roads-raw.json`과 `tank-routes.json`이고, 출력은 `Assets/Scenarios/osm-roads-tnk.json`이다. 외부 패키지가 필요 없다(표준 라이브러리만 사용).
