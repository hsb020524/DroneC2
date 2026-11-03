"""tools/scenario_to_waypoints.py 테스트. 저장소 루트에서: python -I tools/test_waypoints_export.py"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import scenario_to_waypoints as s  # noqa: E402

SQUARE = [(35.0, -83.0), (35.001, -83.0), (35.001, -83.001), (35.0, -83.001)]


class WaypointExportTests(unittest.TestCase):
    def test_header_and_home(self):
        lines = s.build_mission(SQUARE)
        self.assertEqual(lines[0], "QGC WPL 110")
        items = s.parse_mission(lines)
        self.assertEqual(items[0][0], 0)
        self.assertAlmostEqual(items[0][4], 35.0, places=7)
        self.assertAlmostEqual(items[0][5], -83.0, places=7)

    def test_points_roundtrip_and_loop_closed(self):
        items = s.parse_mission(s.build_mission(SQUARE))
        wps = [(i[4], i[5]) for i in items[1:]]
        self.assertEqual(len(wps), len(SQUARE) + 1)
        for got, want in zip(wps, SQUARE + [SQUARE[0]]):
            self.assertAlmostEqual(got[0], want[0], places=7)
            self.assertAlmostEqual(got[1], want[1], places=7)
        self.assertEqual([i[0] for i in items], list(range(len(items))))

    def test_laps_adds_do_jump(self):
        items = s.parse_mission(s.build_mission(SQUARE, laps=3))
        last = items[-1]
        self.assertEqual(last[1], s.CMD_DO_JUMP)
        self.assertEqual((last[2], last[3]), (1.0, 2.0))
        self.assertEqual(s.parse_mission(s.build_mission(SQUARE))[-1][1], s.CMD_WAYPOINT)

    def test_invalid_input(self):
        with self.assertRaises(ValueError):
            s.build_mission(SQUARE[:1])
        with self.assertRaises(ValueError):
            s.build_mission(SQUARE, laps=0)

    def test_real_scenario(self):
        pts = s.load_points(s.DEFAULT_SCENARIO, "TNK-01")
        items = s.parse_mission(s.build_mission(pts))
        self.assertEqual(len(items), len(pts) + 2)


if __name__ == "__main__":
    unittest.main()
