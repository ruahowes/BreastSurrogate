using System;
using BreastSurrogate.Core.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Tests.Geometry
{
    [TestClass]
    public class BeamProjectionTests
    {
        private const double AssertionTolerance = 1e-10;

        [TestMethod]
        public void CoordinateSystemBuildsExpectedRightHandedBasis()
        {
            BeamCoordinateSystem coordinates = CreateCoordinates(0.0);

            AssertVector(1.0, 0.0, 0.0, coordinates.UAxis);
            AssertVector(0.0, 1.0, 0.0, coordinates.VAxis);
            AssertVector(0.0, 0.0, 1.0, coordinates.WAxis);
            AssertVector(coordinates.WAxis, VectorMath.Cross(coordinates.UAxis, coordinates.VAxis));
        }

        [TestMethod]
        public void IsocentreProjectsToOrigin()
        {
            BeamProjection projection = CreateProjection(0.0);

            ProjectedBeamPoint result = projection.Project(new VVector(0.0, 0.0, 0.0));

            Assert.AreEqual(0.0, result.XBld, AssertionTolerance);
            Assert.AreEqual(0.0, result.YBld, AssertionTolerance);
            Assert.AreEqual(1.0, result.ProjectionParameter, AssertionTolerance);
            AssertVector(0.0, 0.0, 0.0, result.ProjectedPoint);
        }

        [TestMethod]
        public void PointsOnCentralAxisProjectToOrigin()
        {
            BeamProjection projection = CreateProjection(0.0);

            ProjectedBeamPoint beforeIsocentre = projection.Project(new VVector(0.0, 0.0, -500.0));
            ProjectedBeamPoint afterIsocentre = projection.Project(new VVector(0.0, 0.0, 1000.0));

            AssertBeamCoordinates(0.0, 0.0, beforeIsocentre);
            AssertBeamCoordinates(0.0, 0.0, afterIsocentre);
        }

        [TestMethod]
        public void PointOnIsocentrePlaneProjectsWithExpectedSignedCoordinates()
        {
            BeamProjection projection = CreateProjection(0.0);

            ProjectedBeamPoint result = projection.Project(new VVector(15.0, -20.0, 0.0));

            AssertBeamCoordinates(15.0, -20.0, result);
            AssertVector(15.0, -20.0, 0.0, result.ProjectedPoint);
        }

        [TestMethod]
        public void ProjectionAppliesBeamDivergenceBeforeAndAfterIsocentre()
        {
            BeamProjection projection = CreateProjection(0.0);

            ProjectedBeamPoint beforeIsocentre = projection.Project(new VVector(5.0, 0.0, -500.0));
            ProjectedBeamPoint afterIsocentre = projection.Project(new VVector(20.0, 0.0, 1000.0));

            AssertBeamCoordinates(10.0, 0.0, beforeIsocentre);
            Assert.AreEqual(2.0, beforeIsocentre.ProjectionParameter, AssertionTolerance);
            AssertBeamCoordinates(10.0, 0.0, afterIsocentre);
            Assert.AreEqual(0.5, afterIsocentre.ProjectionParameter, AssertionTolerance);
        }

        [TestMethod]
        public void PositiveCollimatorRotationRotatesUAxisTowardVAxis()
        {
            BeamCoordinateSystem coordinates = CreateCoordinates(90.0);
            BeamProjection projection = new BeamProjection(coordinates);

            ProjectedBeamPoint result = projection.Project(new VVector(10.0, 0.0, 0.0));

            AssertVector(0.0, 1.0, 0.0, coordinates.UAxis);
            AssertVector(-1.0, 0.0, 0.0, coordinates.VAxis);
            AssertBeamCoordinates(0.0, -10.0, result);
        }

        [TestMethod]
        public void CoordinateSystemRejectsCoincidentSourceAndIsocentre()
        {
            Assert.ThrowsException<ArgumentException>(() => new BeamCoordinateSystem(
                new VVector(1.0, 2.0, 3.0),
                new VVector(1.0, 2.0, 3.0),
                new VVector(0.0, 1.0, 0.0),
                0.0));
        }

        [TestMethod]
        public void CoordinateSystemRejectsReferenceSuperiorParallelToCentralAxis()
        {
            Assert.ThrowsException<ArgumentException>(() => new BeamCoordinateSystem(
                new VVector(0.0, 0.0, -1000.0),
                new VVector(0.0, 0.0, 0.0),
                new VVector(0.0, 0.0, 1.0),
                0.0));
        }

        [TestMethod]
        public void ProjectRejectsRayParallelToIsocentrePlane()
        {
            BeamProjection projection = CreateProjection(0.0);

            Assert.ThrowsException<InvalidOperationException>(
                () => projection.Project(new VVector(10.0, 0.0, -1000.0)));
        }

        [TestMethod]
        public void GeometryRejectsNonFiniteInputs()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new BeamCoordinateSystem(
                new VVector(0.0, 0.0, -1000.0),
                new VVector(0.0, 0.0, 0.0),
                new VVector(0.0, 1.0, 0.0),
                double.NaN));

            BeamProjection projection = CreateProjection(0.0);
            Assert.ThrowsException<ArgumentException>(
                () => projection.Project(new VVector(double.PositiveInfinity, 0.0, 0.0)));
        }

        private static BeamCoordinateSystem CreateCoordinates(double collimatorAngleDegrees)
        {
            return new BeamCoordinateSystem(
                new VVector(0.0, 0.0, -1000.0),
                new VVector(0.0, 0.0, 0.0),
                new VVector(0.0, 1.0, 0.0),
                collimatorAngleDegrees);
        }

        private static BeamProjection CreateProjection(double collimatorAngleDegrees)
        {
            return new BeamProjection(CreateCoordinates(collimatorAngleDegrees));
        }

        private static void AssertBeamCoordinates(
            double expectedX,
            double expectedY,
            ProjectedBeamPoint actual)
        {
            Assert.AreEqual(expectedX, actual.XBld, AssertionTolerance, "Unexpected xBLD.");
            Assert.AreEqual(expectedY, actual.YBld, AssertionTolerance, "Unexpected yBLD.");
        }

        private static void AssertVector(double x, double y, double z, VVector actual)
        {
            Assert.AreEqual(x, actual.x, AssertionTolerance, "Unexpected x component.");
            Assert.AreEqual(y, actual.y, AssertionTolerance, "Unexpected y component.");
            Assert.AreEqual(z, actual.z, AssertionTolerance, "Unexpected z component.");
        }

        private static void AssertVector(VVector expected, VVector actual)
        {
            AssertVector(expected.x, expected.y, expected.z, actual);
        }
    }
}
