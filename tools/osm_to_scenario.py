#!/usr/bin/env python
"""OSM 도로 선(Overpass 응답)에서 전차 경로 시나리오 JSON을 만든다. 표준 라이브러리만 쓴다.

입력: tools/osm/roads-raw.json(Overpass 응답 원본), tools/osm/tank-routes.json(구간 설정)
출력: Assets/Scenarios/osm-roads-tnk.json (OSM 파생 데이터, ODbL. tools/osm/README.md 참고)

처리: 도로 way를 시작점 기준으로 정렬 -> [fromM, toM] 구간 자르기 -> 직선 단순화(허용 오차 m)
      -> 왕복(갔다가 되돌아오는 닫힌 점열). 경로는 마지막 점에서 첫 점으로 닫힌다.

사용 예 (저장소 루트에서):
  python tools/osm_to_scenario.py
  python tools/osm_to_scenario.py --routes tools/osm/tank-routes.json --out Assets/Scenarios/osm-roads-tnk.json
"""

import argparse
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from path_follower import EARTH_RADIUS_M, segment_vector_m  # noqa: E402

ATTRIBUTION = ("Contains data from OpenStreetMap, (c) OpenStreetMap contributors, "
               "made available under the Open Database License (ODbL) 1.0. "
               "https://www.openstreetmap.org/copyright")


def load_way(raw_path, way_id):
    with open(raw_path, encoding="utf-8") as f:
        raw = json.load(f)
    for element in raw["elements"]:
        if element["type"] == "way" and element["id"] == way_id:
            return [(p["lat"], p["lon"]) for p in element["geometry"]], raw["osm3s"]["timestamp_osm_base"]
    raise SystemExit("way %d 를 찾지 못했습니다." % way_id)


def seg_len(a, b):
    north, east = segment_vector_m(a[0], a[1], b[0], b[1])
    return math.hypot(north, east)


def orient(points, near):
    """시작점이 near에 더 가깝도록 방향을 맞춘다."""
    if seg_len(near, points[-1]) < seg_len(near, points[0]):
        return points[::-1]
    return points


def cumulative(points):
    cum = [0.0]
    for a, b in zip(points, points[1:]):
        cum.append(cum[-1] + seg_len(a, b))
    return cum


def cut(points, from_m, to_m):
    """누적 거리 [from_m, to_m] 구간의 점열. 양 끝은 선분 위를 보간한 점이다."""
    cum = cumulative(points)
    if to_m > cum[-1] or from_m < 0 or from_m >= to_m:
        raise SystemExit("구간 %s~%s m 가 도로 길이(%.1f m) 범위를 벗어납니다." % (from_m, to_m, cum[-1]))

    def at(distance):
        for i in range(len(cum) - 1):
            if cum[i] <= distance <= cum[i + 1] and cum[i + 1] > cum[i]:
                f = (distance - cum[i]) / (cum[i + 1] - cum[i])
                a, b = points[i], points[i + 1]
                return (a[0] + f * (b[0] - a[0]), a[1] + f * (b[1] - a[1]))
        raise AssertionError

    out = [at(from_m)]
    out += [p for p, c in zip(points, cum) if from_m < c < to_m]
    out.append(at(to_m))
    return out


def simplify(points, tolerance_m):
    """Douglas-Peucker. 원본 점의 부분집합만 남기고 선분까지의 거리를 tolerance_m 이하로 유지한다."""
    lat0 = points[0][0]
    cos0 = math.cos(math.radians(lat0))
    xy = [(math.radians(p[1]) * EARTH_RADIUS_M * cos0, math.radians(p[0]) * EARTH_RADIUS_M) for p in points]

    def distance(p, a, b):
        dx, dy = b[0] - a[0], b[1] - a[1]
        norm = dx * dx + dy * dy
        if norm == 0.0:
            return math.hypot(p[0] - a[0], p[1] - a[1])
        t = max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / norm))
        return math.hypot(p[0] - (a[0] + t * dx), p[1] - (a[1] + t * dy))

    keep = {0, len(points) - 1}
    stack = [(0, len(points) - 1)]
    while stack:
        lo, hi = stack.pop()
        worst, index = 0.0, None
        for i in range(lo + 1, hi):
            d = distance(xy[i], xy[lo], xy[hi])
            if d > worst:
                worst, index = d, i
        if index is not None and worst > tolerance_m:
            keep.add(index)
            stack.append((lo, index))
            stack.append((index, hi))
    return [points[i] for i in sorted(keep)]


def round_trip(points):
    """p0..pn 에 되돌아오는 점(p(n-1)..p1)을 이어 붙인다. 마지막 점에서 p0으로 닫힌다."""
    return points + points[-2:0:-1]


def fmt_point(p):
    return '{"latitude": %.7f, "longitude": %.7f}' % (p[0], p[1])


def fmt_fault(fault):
    return '{"startSec": %s, "durationSec": %s}' % (float(fault["startSec"]), float(fault["durationSec"]))


def build(raw_path, routes_path, out_path):
    with open(routes_path, encoding="utf-8") as f:
        routes = json.load(f)
    way, timestamp = load_way(raw_path, routes["wayId"])
    near = (routes["startNear"]["latitude"], routes["startNear"]["longitude"])
    road = orient(way, near)
    tolerance = routes["simplifyToleranceM"]

    unit_texts = []
    for unit in routes["units"]:
        section = cut(road, unit["fromM"], unit["toM"])
        simple = simplify(section, tolerance)
        loop = round_trip(simple)
        one_way = sum(seg_len(a, b) for a, b in zip(simple, simple[1:]))
        print("%s: 도로 %d~%d m, 원본 %d점 -> 단순화 %d점 -> 왕복 %d점, 편도 %.1f m, 한 바퀴 %.1f m" % (
            unit["id"], unit["fromM"], unit["toM"], len(section), len(simple), len(loop), one_way, 2 * one_way))
        points = ",\n".join("        " + fmt_point(p) for p in loop)
        unit_texts.append(
            '    {\n'
            '      "id": "%s",\n'
            '      "kind": "Tank",\n'
            '      "altitudeM": 0.0,\n'
            '      "speedMps": %s,\n'
            '      "warning": %s,\n'
            '      "dropout": {"startSec": 0.0, "durationSec": 0.0},\n'
            '      "waypoints": [\n%s\n      ]\n'
            '    }' % (unit["id"], float(unit["speedMps"]), fmt_fault(unit["warning"]), points))

    text = (
        '{\n'
        '  "version": 1,\n'
        '  "name": "OSM roads for tanks (Kuwohi Access Road, virtual units)",\n'
        '  "attribution": %s,\n'
        '  "osmDataTimestamp": "%s",\n'
        '  "units": [\n%s\n  ]\n'
        '}\n' % (json.dumps(ATTRIBUTION), timestamp, ",\n".join(unit_texts)))
    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("출력: %s" % out_path)


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    parser = argparse.ArgumentParser(description="OSM 도로 -> 전차 경로 시나리오 JSON")
    parser.add_argument("--raw", default=os.path.join(root, "tools", "osm", "roads-raw.json"))
    parser.add_argument("--routes", default=os.path.join(root, "tools", "osm", "tank-routes.json"))
    parser.add_argument("--out", default=os.path.join(root, "Assets", "Scenarios", "osm-roads-tnk.json"))
    args = parser.parse_args()
    build(args.raw, args.routes, args.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
