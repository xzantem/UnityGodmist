using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Views.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class CombatGridFeedbackGeometryTests
    {
        [Test]
        public void BuildOutlineEdges_SingleCell_ReturnsFourOuterEdges()
        {
            var edges = CombatGridFeedbackGeometry.BuildOutlineEdges(
                new[] { new GridPosition(2, 3) });

            Assert.That(edges, Has.Count.EqualTo(4));
        }

        [Test]
        public void BuildOutlineEdges_AdjacentCells_OmitsSharedInteriorEdge()
        {
            var edges = CombatGridFeedbackGeometry.BuildOutlineEdges(
                new[]
                {
                    new GridPosition(0, 0),
                    new GridPosition(1, 0)
                });

            Assert.That(edges, Has.Count.EqualTo(6));
            Assert.That(edges.Any(edge => edge.Cell == new GridPosition(0, 0)
                                         && edge.Direction == CombatGridEdgeDirection.Right), Is.False);
            Assert.That(edges.Any(edge => edge.Cell == new GridPosition(1, 0)
                                         && edge.Direction == CombatGridEdgeDirection.Left), Is.False);
        }

        [Test]
        public void BuildOutlineEdges_LShape_ReturnsOnlyTheContinuousOuterPerimeter()
        {
            var cells = new List<GridPosition>
            {
                new(0, 0),
                new(1, 0),
                new(0, 1)
            };

            var edges = CombatGridFeedbackGeometry.BuildOutlineEdges(cells);

            Assert.That(edges, Has.Count.EqualTo(8));
        }
    }
}
