#!/usr/bin/env python
"""시나리오 JSON의 유닛 경로를 Mission Planner 미션 파일(QGC WPL 110)로 바꾼다.

사용 예 (저장소 루트에서):
  python -I tools/scenario_to_waypoints.py --unit TNK-01 --out tools/missions/tnk-01.waypoints
  python -I tools/scenario_to_waypoints.py --unit TNK-01 --laps 2 --out Temp/tnk-01-2laps.waypoints

시나리오 경로는 닫힌 경로(마지막 점에서 첫 점으로 돌아간다)이므로 첫 점을 끝에 한 번 더
넣어 한 바퀴를 닫는다. --laps가 2 이상이면 DO_JUMP로 반복한다.
미션 파일에는 속도가 없다. 속도는 Rover 파라미터(WP_SPEED/CRUISE_SPEED)로 정한다.
표준 라이브러리만 쓴다. 가상 위치만 다루며 기체에 명령을 보내지 않는다(파일만 만든다).
"""

import argparse
import json
import os
import sys

CMD_WAYPOINT = 16
CMD_DO_JUMP = 177
FRAME_GLOBAL = 0
FRAME_GLOBAL_REL_ALT = 3
FRAME_MISSION = 3

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_SCENARIO = os.path.join(ROOT, "Assets", "Scenarios", "osm-roads-tnk.json")


def load_points(scenario_path, unit_id):
    with open(scenario_path, encoding="utf-8") as f:
        data = json.load(f)
    for unit in data["units"]:
        if unit["id"] == unit_id:
            return [(w["latitude"], w["longitude"]) for w in unit["waypoints"]]
    raise SystemExit("유닛 %s이(가) %s에 없습니다" % (unit_id, scenario_path))


def build_mission(points, laps=1, home_alt_m=0.0):
    """QGC WPL 110 줄 목록을 만든다. 0행은 홈(첫 점)."""
    if len(points) < 2:
        raise ValueError("경로 점이 2개 이상 필요합니다")
    if laps < 1:
        raise ValueError("laps는 1 이상이어야 합니다")
    lines = ["QGC WPL 110"]
    lat0, lon0 = points[0]
    # seq current frame command p1 p2 p3 p4 lat lon alt autocontinue
    lines.append("0\t1\t%d\t%d\t0\t0\t0\t0\t%.8f\t%.8f\t%.2f\t1" % (FRAME_GLOBAL, CMD_WAYPOINT, lat0, lon0, home_alt_m))
    seq = 1
    for lat, lon in list(points) + [points[0]]:
        lines.append("%d\t0\t%d\t%d\t0\t0\t0\t0\t%.8f\t%.8f\t0.00\t1" % (seq, FRAME_GLOBAL_REL_ALT, CMD_WAYPOINT, lat, lon))
        seq += 1
    if laps > 1:
        # p1 = 점프할 seq(첫 웨이포인트 = 1), p2 = 반복 횟수
        lines.append("%d\t0\t%d\t%d\t1\t%d\t0\t0\t0\t0\t0\t1" % (seq, FRAME_MISSION, CMD_DO_JUMP, laps - 1))
    return lines


def parse_mission(lines):
    """build_mission 결과를 다시 읽는다(테스트용). [(seq, command, p1, p2, lat, lon)]."""
    if not lines or lines[0].strip() != "QGC WPL 110":
        raise ValueError("QGC WPL 110 헤더가 아닙니다")
    items = []
    for line in lines[1:]:
        c = line.rstrip("\n").split("\t")
        if len(c) != 12:
            raise ValueError("열 수가 12가 아닙니다: %r" % line)
        items.append((int(c[0]), int(c[3]), float(c[4]), float(c[5]), float(c[8]), float(c[9])))
    return items


def main():
    ap = argparse.ArgumentParser(description="시나리오 경로 → Mission Planner .waypoints")
    ap.add_argument("--scenario", default=DEFAULT_SCENARIO)
    ap.add_argument("--unit", default="TNK-01")
    ap.add_argument("--laps", type=int, default=1)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    points = load_points(args.scenario, args.unit)
    lines = build_mission(points, args.laps)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="ascii", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("%s: 경로 점 %d개 → 미션 %d행(홈 포함) 바퀴 %d → %s" % (args.unit, len(points), len(lines) - 1, args.laps, args.out))
    print("속도는 파일에 없다. Rover 파라미터 WP_SPEED/CRUISE_SPEED, 도착 반경은 WP_RADIUS로 정한다.")


if __name__ == "__main__":
    sys.exit(main())
