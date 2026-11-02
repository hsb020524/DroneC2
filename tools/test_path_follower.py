"""tools/path_follower.py 테스트. 저장소 루트에서: python -I tools/test_path_follower.py"""

import json
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from path_follower import PathFollower  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GOLDEN = os.path.join(ROOT, "tools", "testdata", "path_follower_golden.json")

# 위도 방향 0.001도 = 약 111m. 남북·동서로 이어지는 사각 경로.
SQUARE = [(35.0, -83.0), (35.001, -83.0), (35.001, -83.001), (35.0, -83.001)]


class PathFollowerTests(unittest.TestCase):
    def test_start_is_first_point_and_heading_is_first_segment(self):
        lat, lon, heading = PathFollower(SQUARE).position(0.0)
        self.assertEqual((lat, lon), SQUARE[0])
        self.assertAlmostEqual(heading, 0.0, places=6)  # 정북

    def test_corner_is_not_cut(self):
        f = PathFollower(SQUARE)
        north = math.radians(0.001) * 6378137.0
        lat, lon, _ = f.position(north)
        self.assertAlmostEqual(lat, 35.001, places=9)
        self.assertAlmostEqual(lon, -83.0, places=9)

    def test_wraps_around_after_one_lap(self):
        f = PathFollower(SQUARE)
        a = f.position(10.0)
        b = f.position(10.0 + f.total_length_m * 3)
        for x, y in zip(a, b):
            self.assertAlmostEqual(x, y, places=7)

    def test_negative_distance_wraps(self):
        f = PathFollower(SQUARE)
        a = f.position(f.total_length_m - 5.0)
        b = f.position(-5.0)
        for x, y in zip(a, b):
            self.assertAlmostEqual(x, y, places=9)

    def test_closing_segment_returns_to_start(self):
        f = PathFollower(SQUARE)
        lat, lon, heading = f.position(f.total_length_m - 0.001)
        self.assertAlmostEqual(lat, SQUARE[0][0], places=6)
        self.assertAlmostEqual(lon, SQUARE[0][1], places=6)
        self.assertAlmostEqual(heading, 90.0, places=0)  # 마지막 선분은 동쪽(-83.001 -> -83.0)

    def test_zero_length_segments_are_skipped(self):
        points = [(35.0, -83.0), (35.0, -83.0), (35.001, -83.0), (35.001, -83.001)]
        lat, lon, _ = PathFollower(points).position(1.0)
        self.assertTrue(35.0 < lat < 35.001)

    def test_degenerate_paths_are_rejected(self):
        with self.assertRaises(ValueError):
            PathFollower([(35.0, -83.0)])
        with self.assertRaises(ValueError):
            PathFollower([(35.0, -83.0), (35.0, -83.0)])


class GoldenTests(unittest.TestCase):
    def test_matches_recorded_golden_values(self):
        with open(GOLDEN, encoding="utf-8") as f:
            golden = json.load(f)
        self.assertGreaterEqual(len(golden["cases"]), 4)
        for case in golden["cases"]:
            points = [(float(p["latitude"]), float(p["longitude"])) for p in case["points"]]
            f = PathFollower(points)
            self.assertAlmostEqual(f.total_length_m, float(case["totalLengthM"]), places=9, msg=case["name"])
            for s in case["samples"]:
                lat, lon, heading = f.position(float(s["distanceM"]))
                self.assertAlmostEqual(lat, float(s["latitude"]), places=12, msg=case["name"])
                self.assertAlmostEqual(lon, float(s["longitude"]), places=12, msg=case["name"])
                self.assertAlmostEqual(heading, float(s["headingDeg"]), places=9, msg=case["name"])


if __name__ == "__main__":
    unittest.main()
