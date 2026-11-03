#!/usr/bin/env python
"""전차 경로가 OSM 도로 위에 있는지 측정한다. 표준 라이브러리만 쓴다.

시나리오의 각 유닛을 PathFollower로 재생(기본 10 Hz, 2바퀴)하고, 샘플마다 OSM 도로 선
(roads-raw.json의 way 원본 노드 전체)까지의 최단 거리(m)를 구해 평균·95%·최대를 보고한다.
지형 경사는 Unity에서 받은 지형 높이(Temp/road-probe.csv)가 있을 때 --heights로 함께 계산한다.

사용 예 (저장소 루트에서):
  python tools/measure_road_deviation.py
  python tools/measure_road_deviation.py --heights Temp/road-probe.csv
  python tools/measure_road_deviation.py --positions Temp/road-play-fakegps.csv   # Play 중 실제 파이프라인 위치
"""

import argparse
import csv
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from path_follower import EARTH_RADIUS_M, PathFollower  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def road_polyline(raw_path, way_id):
    with open(raw_path, encoding="utf-8") as f:
        for e in json.load(f)["elements"]:
            if e["type"] == "way" and e["id"] == way_id:
                return [(p["lat"], p["lon"]) for p in e["geometry"]]
    raise SystemExit("way %d 없음" % way_id)


def distance_to_polyline_m(lat, lon, poly):
    """점에서 폴리라인까지의 최단 거리(m). 점 위도 기준 평면 근사."""
    cos = math.cos(math.radians(lat))
    k_n = math.radians(1.0) * EARTH_RADIUS_M
    k_e = k_n * cos
    best = float("inf")
    prev = None
    for p in poly:
        cur = ((p[1] - lon) * k_e, (p[0] - lat) * k_n)
        if prev is not None:
            ax, ay = prev
            bx, by = cur
            dx, dy = bx - ax, by - ay
            norm = dx * dx + dy * dy
            t = 0.0 if norm == 0.0 else max(0.0, min(1.0, -(ax * dx + ay * dy) / norm))
            best = min(best, math.hypot(ax + t * dx, ay + t * dy))
        prev = cur
    return best


def percentile(values, q):
    s = sorted(values)
    return s[min(len(s) - 1, int(len(s) * q))]


def measure_deviation(scenario_path, poly, rate_hz, laps):
    with open(scenario_path, encoding="utf-8") as f:
        units = json.load(f)["units"]
    results = {}
    for u in units:
        follower = PathFollower([(p["latitude"], p["longitude"]) for p in u["waypoints"]])
        speed = float(u["speedMps"])
        duration = follower.total_length_m * laps / speed
        n = int(duration * rate_hz)
        dev = []
        for i in range(n + 1):
            lat, lon, _ = follower.position(speed * i / rate_hz)
            dev.append(distance_to_polyline_m(lat, lon, poly))
        results[u["id"]] = (dev, follower.total_length_m, speed)
    return results


def report_deviation(results, rate_hz):
    print("== 도로 이탈 거리 (OSM 도로 선까지 최단 거리) ==")
    for unit_id, (dev, length, speed) in results.items():
        print("%s: 샘플 %d개(%.0f Hz), 한 바퀴 %.0f m / %.0f m/s | 평균 %.2f m, 95%% %.2f m, 최대 %.2f m | 1m 초과 %d개, 3m 초과 %d개" % (
            unit_id, len(dev), rate_hz, length, speed, sum(dev) / len(dev), percentile(dev, 0.95), max(dev),
            sum(1 for d in dev if d > 1.0), sum(1 for d in dev if d > 3.0)))


def report_heading(items, min_move_m=1.0):
    """보고된 방위·뷰 방향을 실제 이동 방향(월드 좌표)과 비교한다. 열이 없으면 건너뛴다.

    이동 방향은 min_move_m 이상 움직인 구간만 쓴다. 월드 X=동쪽, Z=북쪽이면 방위=atan2(dx, dz).
    """
    if not items or "reportedHeadingDeg" not in items[0]:
        return
    rep, view = [], []
    ref = items[0]
    for r in items[1:]:
        dx = float(r["worldX"]) - float(ref["worldX"])
        dz = float(r["worldZ"]) - float(ref["worldZ"])
        if math.hypot(dx, dz) < min_move_m:
            continue
        move = (math.degrees(math.atan2(dx, dz)) + 360.0) % 360.0
        for name, out in (("reportedHeadingDeg", rep), ("viewYawDeg", view)):
            v = float(r[name])
            if not math.isnan(v):
                out.append(abs((v - move + 180.0) % 360.0 - 180.0))
        ref = r
    for label, d in (("보고된 방위", rep), ("뷰 방향", view)):
        if d:
            print("  방위 %s vs 이동 방향: 구간 %d개 | 평균 차이 %.1f°, 95%% %.1f°, 최대 %.1f°" % (
                label, len(d), sum(d) / len(d), percentile(d, 0.95), max(d)))


def report_positions(positions_path, poly, tank_ids=("TNK-01", "TNK-02")):
    """RoadProbe.StartPlayMeasurement가 기록한 실제 파이프라인 위치(CSV)의 도로 이탈 거리."""
    rows = {}
    with open(positions_path, encoding="utf-8") as f:
        for r in csv.DictReader(f):
            rows.setdefault(r["unit"], []).append(r)
    print("== Play 중 실제 파이프라인 위치의 도로 이탈 거리 (%s) ==" % os.path.basename(positions_path))
    for unit_id in tank_ids:
        items = rows.get(unit_id, [])
        if not items:
            print("%s: 기록 없음" % unit_id)
            continue
        dev = [distance_to_polyline_m(float(r["latitude"]), float(r["longitude"]), poly) for r in items]
        span = float(items[-1]["timeSec"]) - float(items[0]["timeSec"])
        print("%s: 샘플 %d개(%.0f초) | 평균 %.2f m, 95%% %.2f m, 최대 %.2f m | 1m 초과 %d개, 3m 초과 %d개, 5m 초과 %d개" % (
            unit_id, len(dev), span, sum(dev) / len(dev), percentile(dev, 0.95), max(dev),
            sum(1 for d in dev if d > 1.0), sum(1 for d in dev if d > 3.0), sum(1 for d in dev if d > 5.0)))
        report_heading(items)
    others = sorted(set(rows) - set(tank_ids))
    if others:
        print("그 밖에 기록된 유닛(도로 비교 대상 아님): " + ", ".join("%s %d개" % (u, len(rows[u])) for u in others))


def report_slope(heights_path):
    rows = {}
    with open(heights_path, encoding="utf-8") as f:
        for r in csv.DictReader(f):
            rows.setdefault(r["unit"], []).append(r)
    print("== 지형 경사 (Cesium 지형 높이, 경로를 따라 인접 조회점 사이) ==")
    for unit_id, items in rows.items():
        ok = [r for r in items if r["success"] == "1"]
        slopes = []
        for a, b in zip(ok, ok[1:]):
            run = float(b["distanceM"]) - float(a["distanceM"])
            if run <= 0:
                continue
            rise = float(b["ellipsoidHeightM"]) - float(a["ellipsoidHeightM"])
            slopes.append((math.degrees(math.atan2(abs(rise), run)), a, b))
        worst = max(slopes, key=lambda s: s[0])
        degs = [s[0] for s in slopes]
        spacing = float(items[1]["distanceM"]) - float(items[0]["distanceM"])
        print("%s: 조회 %d점(성공 %d, 간격 %.0f m), 높이 %.1f~%.1f m | 경사 평균 %.1f°, 95%% %.1f°, 최대 %.1f° (거리 %.0f~%.0f m) | 15° 초과 %d구간" % (
            unit_id, len(items), len(ok), spacing,
            min(float(r["ellipsoidHeightM"]) for r in ok), max(float(r["ellipsoidHeightM"]) for r in ok),
            sum(degs) / len(degs), percentile(degs, 0.95), worst[0],
            float(worst[1]["distanceM"]), float(worst[2]["distanceM"]), sum(1 for d in degs if d > 15.0)))


def main():
    parser = argparse.ArgumentParser(description="전차 경로의 OSM 도로 이탈·경사 측정")
    parser.add_argument("--raw", default=os.path.join(ROOT, "tools", "osm", "roads-raw.json"))
    parser.add_argument("--routes", default=os.path.join(ROOT, "tools", "osm", "tank-routes.json"))
    parser.add_argument("--scenario", default=os.path.join(ROOT, "Assets", "Scenarios", "osm-roads-tnk.json"))
    parser.add_argument("--rate-hz", type=float, default=10.0)
    parser.add_argument("--laps", type=float, default=2.0)
    parser.add_argument("--heights", help="RoadProbe가 만든 CSV(Temp/road-probe.csv). 있으면 경사도 계산한다")
    parser.add_argument("--positions", action="append",
                        help="RoadProbe.StartPlayMeasurement가 만든 CSV(Temp/road-play-*.csv). 여러 번 줄 수 있다. "
                             "주면 시나리오 재생 대신 이 위치들의 이탈 거리만 계산한다")
    args = parser.parse_args()

    with open(args.routes, encoding="utf-8") as f:
        way_id = json.load(f)["wayId"]
    poly = road_polyline(args.raw, way_id)
    if args.positions:
        for path in args.positions:
            report_positions(path, poly)
        return 0
    report_deviation(measure_deviation(args.scenario, poly, args.rate_hz, args.laps), args.rate_hz)
    if args.heights:
        report_slope(args.heights)
    return 0


if __name__ == "__main__":
    sys.exit(main())
