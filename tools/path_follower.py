"""경로를 따라 이동한 거리로 위치를 구하는 순수 계산 모듈.

Unity의 PathFollower(Assets/Scripts/DataSource/PathFollower.cs)와 같은 수식이다. 두 구현이
같은 입력에서 같은 값을 내는지는 tools/testdata/path_follower_golden.json으로 대조한다.

경로는 점 목록이며 마지막 점에서 첫 점으로 닫힌다(순환). 선분 i = 점 i -> 점 (i+1) % n.
 - 선분 길이: 평면 근사. north = rad(dlat)*R, east = rad(dlon)*R*cos(rad(선분 시작 위도))
 - 선분 안의 위치: 위도·경도를 선형 보간
 - 방위: 선분의 atan2(east, north), 0~360도(북 0, 시계방향)
 - 이동 거리 s는 전체 길이 L로 나눈 나머지를 쓴다.
"""

import bisect
import math

EARTH_RADIUS_M = 6378137.0


def segment_vector_m(lat1, lon1, lat2, lon2):
    """선분의 (north, east) 길이(m). 경도 환산에는 선분 시작 위도를 쓴다."""
    north = math.radians(lat2 - lat1) * EARTH_RADIUS_M
    east = math.radians(lon2 - lon1) * EARTH_RADIUS_M * math.cos(math.radians(lat1))
    return north, east


class PathFollower:
    def __init__(self, points):
        """points: [(위도, 경도), ...] 2개 이상. 전체 길이가 0이면 ValueError."""
        if len(points) < 2:
            raise ValueError("경로에는 점이 2개 이상 필요합니다.")
        self.points = [(float(a), float(b)) for a, b in points]
        n = len(self.points)
        self._cum = [0.0]
        self._vec = []
        for i in range(n):
            a = self.points[i]
            b = self.points[(i + 1) % n]
            north, east = segment_vector_m(a[0], a[1], b[0], b[1])
            self._vec.append((north, east))
            self._cum.append(self._cum[-1] + math.sqrt(north * north + east * east))
        self.total_length_m = self._cum[-1]
        if not self.total_length_m > 0.0:
            raise ValueError("경로의 전체 길이가 0입니다.")

    def position(self, distance_m):
        """(위도, 경도, 방위각도)를 돌려준다. distance_m은 시작점에서 이동한 거리(순환)."""
        s = math.fmod(distance_m, self.total_length_m)
        if s < 0.0:
            s += self.total_length_m
        # cum[i] <= s < cum[i+1]인 마지막 i. 길이 0인 선분은 자동으로 건너뛴다.
        i = bisect.bisect_right(self._cum, s) - 1
        n = len(self.points)
        if i >= n:
            i = n - 1
        a = self.points[i]
        b = self.points[(i + 1) % n]
        length = self._cum[i + 1] - self._cum[i]
        f = (s - self._cum[i]) / length
        lat = a[0] + f * (b[0] - a[0])
        lon = a[1] + f * (b[1] - a[1])
        north, east = self._vec[i]
        heading = (math.degrees(math.atan2(east, north)) + 360.0) % 360.0
        return lat, lon, heading
