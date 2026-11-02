#!/usr/bin/env python
"""PathFollower 기준값 파일(tools/testdata/path_follower_golden.json)을 만든다.

파이썬 구현(tools/path_follower.py)의 결과를 기록하고, C# EditMode 테스트(PathFollowerTests)가
같은 파일을 읽어 C# 구현이 같은 값을 내는지 대조한다. 경로나 수식을 바꿨을 때만 다시 만든다.

사용 예 (저장소 루트에서):
  python tools/make_path_golden.py
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from path_follower import PathFollower  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "tools", "testdata", "path_follower_golden.json")


def unit_points(scenario_path, unit_id):
    with open(os.path.join(ROOT, scenario_path), encoding="utf-8") as f:
        for unit in json.load(f)["units"]:
            if unit["id"] == unit_id:
                return [(p["latitude"], p["longitude"]) for p in unit["waypoints"]]
    raise SystemExit("유닛 %s 없음" % unit_id)


def distances(total):
    """경계(0, 선분 끝 근처, 한 바퀴, 음수, 여러 바퀴)를 포함한 거리 목록."""
    base = [0.0, 0.001, total * 0.25, total * 0.5, total * 0.999999, total, total + 0.001,
            total * 1.5, total * 3.25, -1.0, -total * 0.1, 1e6]
    base += [total * i / 40.0 for i in range(40)]
    return base


def main():
    cases = {
        "drone-rectangle(UAV-01)": unit_points("Assets/Scenarios/stage3-demo.json", "UAV-01"),
        "tank-road(TNK-01)": unit_points("Assets/Scenarios/osm-roads-tnk.json", "TNK-01"),
        "tank-road(TNK-02)": unit_points("Assets/Scenarios/osm-roads-tnk.json", "TNK-02"),
        "duplicate-points": [(35.0, -83.0), (35.0, -83.0), (35.001, -83.0), (35.001, -83.001),
                             (35.001, -83.001), (35.0, -83.001)],
    }
    out = {"note": "PathFollower golden values from tools/path_follower.py. Regenerate: python tools/make_path_golden.py",
           "cases": []}
    for name, points in cases.items():
        follower = PathFollower(points)
        samples = []
        for d in distances(follower.total_length_m):
            lat, lon, heading = follower.position(d)
            samples.append({"distanceM": repr(float(d)), "latitude": repr(lat), "longitude": repr(lon),
                            "headingDeg": repr(heading)})
        out["cases"].append({
            "name": name,
            "totalLengthM": repr(follower.total_length_m),
            "points": [{"latitude": repr(a), "longitude": repr(b)} for a, b in points],
            "samples": samples,
        })
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, indent=1)
        f.write("\n")
    print("출력: %s (%d 경로, 경로당 %d 샘플)" % (OUT, len(out["cases"]), len(out["cases"][0]["samples"])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
