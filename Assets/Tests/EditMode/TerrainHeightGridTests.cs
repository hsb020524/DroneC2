using System.Collections.Generic;
using DroneC2.Coordinate;
using NUnit.Framework;

namespace DroneC2.Tests
{
    public class TerrainHeightGridTests
    {
        const double Cell = 0.001;

        static void Fill(TerrainHeightGrid grid, double h00, double h01, double h10, double h11)
        {
            // 셀 (0,0)의 네 모서리: (위도, 경도) = (0,0) (0,c) (c,0) (c,c)
            grid.SetNodeHeight(0.0, 0.0, h00);
            grid.SetNodeHeight(0.0, Cell, h01);
            grid.SetNodeHeight(Cell, 0.0, h10);
            grid.SetNodeHeight(Cell, Cell, h11);
        }

        [Test]
        public void MissingNodes_ReturnFalse()
        {
            var grid = new TerrainHeightGrid(Cell);
            Assert.IsFalse(grid.TryInterpolate(0.0005, 0.0005, out _));
        }

        [Test]
        public void PartialNodes_ReturnFalse()
        {
            var grid = new TerrainHeightGrid(Cell);
            grid.SetNodeHeight(0.0, 0.0, 100.0);
            Assert.IsFalse(grid.TryInterpolate(0.0005, 0.0005, out _));
        }

        [Test]
        public void AtNode_ReturnsNodeHeight()
        {
            var grid = new TerrainHeightGrid(Cell);
            Fill(grid, 100.0, 120.0, 140.0, 160.0);
            Assert.IsTrue(grid.TryInterpolate(0.0, 0.0, out double h));
            Assert.AreEqual(100.0, h, 1e-9);
        }

        [Test]
        public void ApproachingEastNode_ConvergesToNodeHeight()
        {
            // 셀 경계 바로 안쪽에서는 동쪽 노드(h01) 값에 수렴해야 한다.
            var grid = new TerrainHeightGrid(Cell);
            Fill(grid, 100.0, 120.0, 140.0, 160.0);
            Assert.IsTrue(grid.TryInterpolate(0.0, Cell * 0.999999, out double h));
            Assert.AreEqual(120.0, h, 1e-3);
        }

        [Test]
        public void CellCenter_ReturnsAverageOfFourNodes()
        {
            var grid = new TerrainHeightGrid(Cell);
            Fill(grid, 100.0, 120.0, 140.0, 160.0);
            Assert.IsTrue(grid.TryInterpolate(Cell / 2, Cell / 2, out double h));
            Assert.AreEqual(130.0, h, 1e-9);
        }

        [Test]
        public void AlongLongitude_InterpolatesLinearly()
        {
            var grid = new TerrainHeightGrid(Cell);
            Fill(grid, 100.0, 200.0, 100.0, 200.0);
            Assert.IsTrue(grid.TryInterpolate(0.0, Cell * 0.25, out double h));
            Assert.AreEqual(125.0, h, 1e-9);
        }

        [Test]
        public void NegativeCoordinates_InterpolateInCorrectCell()
        {
            // 경도가 음수여도 floor 기준으로 올바른 셀을 찾아야 한다. (예: 서경 83.499도)
            var grid = new TerrainHeightGrid(Cell);
            double lat0 = 35.563, lon0 = -83.499;
            grid.SetNodeHeight(lat0, lon0, 100.0);
            grid.SetNodeHeight(lat0, lon0 + Cell, 200.0);
            grid.SetNodeHeight(lat0 + Cell, lon0, 100.0);
            grid.SetNodeHeight(lat0 + Cell, lon0 + Cell, 200.0);

            Assert.IsTrue(grid.TryInterpolate(lat0 + Cell / 2, lon0 + Cell / 2, out double h));
            Assert.AreEqual(150.0, h, 1e-6);
        }

        [Test]
        public void CollectMissingNodes_RequestsEachNodeOnce()
        {
            var grid = new TerrainHeightGrid(Cell);
            var first = new List<(double latitude, double longitude)>();
            grid.CollectMissingNodes(0.0005, 0.0005, 0, first);
            Assert.AreEqual(4, first.Count);

            var second = new List<(double latitude, double longitude)>();
            grid.CollectMissingNodes(0.0005, 0.0005, 0, second);
            Assert.AreEqual(0, second.Count, "이미 요청한 노드는 다시 요청하지 않는다");
        }

        [Test]
        public void CollectMissingNodes_PrefetchMarginAddsOuterNodes()
        {
            var grid = new TerrainHeightGrid(Cell);
            var nodes = new List<(double latitude, double longitude)>();
            grid.CollectMissingNodes(0.0005, 0.0005, 2, nodes);
            Assert.AreEqual(36, nodes.Count); // (2 + 2*2)^2
        }

        [Test]
        public void CollectMissingNodes_SkipsCachedNodes()
        {
            var grid = new TerrainHeightGrid(Cell);
            Fill(grid, 100.0, 100.0, 100.0, 100.0);
            var nodes = new List<(double latitude, double longitude)>();
            grid.CollectMissingNodes(0.0005, 0.0005, 0, nodes);
            Assert.AreEqual(0, nodes.Count);
        }

        [Test]
        public void FailedRequest_CanBeRequestedAgain()
        {
            var grid = new TerrainHeightGrid(Cell);
            var nodes = new List<(double latitude, double longitude)>();
            grid.CollectMissingNodes(0.0005, 0.0005, 0, nodes);
            var failed = nodes[0];
            grid.MarkRequestFailed(failed.latitude, failed.longitude);

            var retry = new List<(double latitude, double longitude)>();
            grid.CollectMissingNodes(0.0005, 0.0005, 0, retry);
            Assert.AreEqual(1, retry.Count);
        }
    }
}
