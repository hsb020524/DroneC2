#!/usr/bin/env python
"""가상 MAVLink 에미터. 시나리오 JSON의 유닛을 sysid별로 재생해 UDP로 보낸다. (송신 전용 도구)

Unity(수신 전용)가 MAVLink 모드에서 받는 입력을 만들기 위한 시뮬레이터 대용이다.
실제 기체·시설과 무관한 가상 위치만 보낸다. pymavlink(LGPL-3.0)는 이 도구에서만 쓰며
Unity 프로젝트에는 포함하지 않는다. (docs/requirements.md 6절)

사용 예 (저장소 루트에서):
  C:\\UnityProjects\\DroneC2-tools\\venv\\Scripts\\python.exe tools/mavlink_emitter.py
  ... mavlink_emitter.py --duration 60
  ... mavlink_emitter.py --hold-home-sysids 1 --duration 40   # 드론 1을 홈에 relative_alt=0으로 정지
  ... mavlink_emitter.py --mavlink1                           # v1 프레임으로 보내기
"""

import argparse
import json
import math
import os
import socket
import sys
import time

EARTH_RADIUS_M = 6378137.0
MAV_TYPE_QUADROTOR = 2
MAV_TYPE_GROUND_ROVER = 10
MAV_AUTOPILOT_ARDUPILOTMEGA = 3
MAV_STATE_ACTIVE = 4
MAV_STATE_CRITICAL = 5

DEFAULT_UNIT_MAP = "UAV-01=1,UAV-02=2,UAV-03=3,TNK-01=4,TNK-02=5"


class UdpFile:
    """pymavlink가 write()로 내보내는 바이트를 UDP로 보내는 파일 흉내."""

    def __init__(self, sock, address):
        self.sock = sock
        self.address = address
        self.packets = 0

    def write(self, data):
        self.sock.sendto(data, self.address)
        self.packets += 1

    def flush(self):
        pass


def parse_unit_map(text):
    mapping = {}
    for pair in text.split(","):
        pair = pair.strip()
        if not pair:
            continue
        unit_id, _, sysid = pair.partition("=")
        mapping[unit_id.strip()] = int(sysid)
    return mapping


def bearing_deg(lat1, lon1, lat2, lon2):
    """평면 근사 방위각(도, 북 0 시계방향). Unity의 FakeGpsSource와 같은 근사."""
    d_north = math.radians(lat2 - lat1) * EARTH_RADIUS_M
    d_east = math.radians(lon2 - lon1) * EARTH_RADIUS_M * math.cos(math.radians(lat1))
    return (math.degrees(math.atan2(d_east, d_north)) + 360.0) % 360.0


def distance_m(lat1, lon1, lat2, lon2):
    d_north = math.radians(lat2 - lat1) * EARTH_RADIUS_M
    d_east = math.radians(lon2 - lon1) * EARTH_RADIUS_M * math.cos(math.radians(lat1))
    return math.hypot(d_north, d_east)


class Unit:
    def __init__(self, entry, sysid, link, speed_scale, hold_home, home):
        self.id = entry["id"]
        self.sysid = sysid
        self.is_tank = entry.get("kind", "Drone") == "Tank"
        self.relative_alt_m = 0.0 if self.is_tank else float(entry.get("altitudeM", 0.0))
        self.speed = float(entry.get("speedMps", 10.0)) * speed_scale
        self.waypoints = [(p["latitude"], p["longitude"]) for p in entry["waypoints"]]
        self.warning = entry.get("warning", {"startSec": 0, "durationSec": 0})
        self.dropout = entry.get("dropout", {"startSec": 0, "durationSec": 0})
        self.link = link
        self.hold_home = hold_home
        self.lat, self.lon = (home if hold_home else self.waypoints[0])
        if hold_home:
            self.relative_alt_m = 0.0
        self.target = 1
        self.heading = bearing_deg(self.lat, self.lon, *self.waypoints[1])
        self.cur_speed = 0.0 if hold_home else self.speed

    @staticmethod
    def in_window(elapsed, window):
        duration = float(window.get("durationSec", 0))
        start = float(window.get("startSec", 0))
        return duration > 0.0 and start <= elapsed < start + duration

    def advance(self, dt):
        if self.hold_home:
            return
        remaining = self.speed * dt
        while remaining > 0.0:
            tlat, tlon = self.waypoints[self.target]
            dist = distance_m(self.lat, self.lon, tlat, tlon)
            if dist <= 5.0:  # 도착 반경
                self.target = (self.target + 1) % len(self.waypoints)
                self.heading = bearing_deg(self.lat, self.lon, *self.waypoints[self.target])
                continue
            step = min(remaining, dist)
            self.heading = bearing_deg(self.lat, self.lon, tlat, tlon)
            b = math.radians(self.heading)
            self.lat += math.degrees(step * math.cos(b) / EARTH_RADIUS_M)
            self.lon += math.degrees(step * math.sin(b) / (EARTH_RADIUS_M * math.cos(math.radians(self.lat))))
            remaining -= step

    def send_heartbeat(self, elapsed):
        status = MAV_STATE_CRITICAL if self.in_window(elapsed, self.warning) else MAV_STATE_ACTIVE
        vehicle = MAV_TYPE_GROUND_ROVER if self.is_tank else MAV_TYPE_QUADROTOR
        self.link.heartbeat_send(vehicle, MAV_AUTOPILOT_ARDUPILOTMEGA, 0, 0, status)

    def send_position(self, elapsed, home_msl_m):
        b = math.radians(self.heading)
        vx = int(round(self.cur_speed * math.cos(b) * 100.0))  # 북쪽 cm/s
        vy = int(round(self.cur_speed * math.sin(b) * 100.0))  # 동쪽 cm/s
        hdg = int(round(self.heading * 100.0)) % 36000
        rel_mm = int(round(self.relative_alt_m * 1000.0))
        msl_mm = int(round((home_msl_m + self.relative_alt_m) * 1000.0))
        self.link.global_position_int_send(
            int(elapsed * 1000.0) & 0xFFFFFFFF,
            int(round(self.lat * 1e7)), int(round(self.lon * 1e7)),
            msl_mm, rel_mm, vx, vy, 0, hdg)


def main():
    parser = argparse.ArgumentParser(description="가상 MAVLink 에미터 (송신 전용)")
    parser.add_argument("--scenario", default="Assets/Scenarios/stage3-demo.json")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=14560)
    parser.add_argument("--unit-map", default=DEFAULT_UNIT_MAP, help="유닛 id=sysid 쉼표 목록")
    parser.add_argument("--rate-hz", type=float, default=10.0, help="위치 전송 주기")
    parser.add_argument("--duration", type=float, default=0.0, help="초. 0이면 Ctrl+C까지")
    parser.add_argument("--speed-scale", type=float, default=1.0)
    parser.add_argument("--home", default="35.563,-83.499", help="홈 위경도(Unity 테이블과 같게)")
    parser.add_argument("--home-msl", type=float, default=0.0, help="alt 필드용 홈 해발(m). Unity는 쓰지 않는다")
    parser.add_argument("--hold-home-sysids", default="", help="홈에 relative_alt=0으로 정지시킬 sysid 목록")
    parser.add_argument("--only-sysids", default="", help="이 sysid만 보낸다(비우면 전부)")
    parser.add_argument("--mavlink1", action="store_true", help="v1 프레임으로 보낸다")
    args = parser.parse_args()

    # 반드시 pymavlink를 불러오기 전에 정한다.
    if not args.mavlink1:
        os.environ["MAVLINK20"] = "1"
    from pymavlink import mavutil  # noqa: E402  (LGPL, 별도 프로세스 전용)

    dialect = mavutil.mavlink
    home_lat, home_lon = (float(v) for v in args.home.split(","))
    hold = {int(s) for s in args.hold_home_sysids.split(",") if s.strip()}
    only = {int(s) for s in args.only_sysids.split(",") if s.strip()}
    unit_map = parse_unit_map(args.unit_map)

    with open(args.scenario, encoding="utf-8") as f:
        scenario = json.load(f)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp = UdpFile(sock, (args.host, args.port))
    units = []
    for entry in scenario["units"]:
        sysid = unit_map.get(entry["id"])
        if sysid is None:
            print("건너뜀: 유닛 %s의 sysid 매핑이 없습니다." % entry["id"], file=sys.stderr)
            continue
        if only and sysid not in only:
            continue
        link = dialect.MAVLink(udp, srcSystem=sysid, srcComponent=1)
        units.append(Unit(entry, sysid, link, args.speed_scale, sysid in hold, (home_lat, home_lon)))

    if not units:
        print("보낼 유닛이 없습니다.", file=sys.stderr)
        return 1

    version = "v1" if args.mavlink1 else "v2"
    print("에미터 시작: %s %d기 -> %s:%d (%s, %.1f Hz)%s" % (
        os.path.basename(args.scenario), len(units), args.host, args.port, version, args.rate_hz,
        " [홈 정지: %s]" % sorted(hold) if hold else ""))
    for u in units:
        print("  sysid %d = %s (%s)" % (u.sysid, u.id, "Tank" if u.is_tank else "Drone"))

    interval = 1.0 / args.rate_hz
    start = time.monotonic()
    last = start
    next_tick = start
    next_heartbeat = start
    sent_positions = 0
    try:
        while True:
            now = time.monotonic()
            elapsed = now - start
            if args.duration > 0.0 and elapsed >= args.duration:
                break
            if now < next_tick:
                time.sleep(min(next_tick - now, 0.02))
                continue
            dt = now - last
            last = now
            next_tick += interval
            send_heartbeat = now >= next_heartbeat
            if send_heartbeat:
                next_heartbeat += 1.0
            for u in units:
                u.advance(dt)
                if Unit.in_window(elapsed, u.dropout):
                    continue  # 두절 구간: 아무것도 보내지 않는다
                if send_heartbeat:
                    u.send_heartbeat(elapsed)
                u.send_position(elapsed, args.home_msl)
                sent_positions += 1
    except KeyboardInterrupt:
        pass
    print("에미터 종료: 위치 %d건, 데이터그램 %d개" % (sent_positions, udp.packets))
    return 0


if __name__ == "__main__":
    sys.exit(main())
