using System;
using BreastSurrogate.Core.Apertures;
using BreastSurrogate.Core.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Tests.Apertures
{
    [TestClass]
    public class StaticBeamApertureTests
    {
        [TestMethod]
        public void ContainsReturnsTrueAtFieldCentre()
        {
            StaticBeamAperture aperture = CreateAperture(0.0);

            Assert.IsTrue(aperture.Contains(new VVector(0.0, 0.0, 0.0)));
        }

        [TestMethod]
        public void ContainsReturnsFalseOutsideEachJawAtIsocentrePlane()
        {
            StaticBeamAperture aperture = CreateAperture(0.0);

            Assert.IsFalse(aperture.Contains(new VVector(-10.001, 0.0, 0.0)));
            Assert.IsFalse(aperture.Contains(new VVector(10.001, 0.0, 0.0)));
            Assert.IsFalse(aperture.Contains(new VVector(0.0, -20.001, 0.0)));
            Assert.IsFalse(aperture.Contains(new VVector(0.0, 20.001, 0.0)));
        }

        [TestMethod]
        public void ContainsIncludesEachJawEdgeAtIsocentrePlane()
        {
            StaticBeamAperture aperture = CreateAperture(0.0);

            Assert.IsTrue(aperture.Contains(new VVector(-10.0, 0.0, 0.0)));
            Assert.IsTrue(aperture.Contains(new VVector(10.0, 0.0, 0.0)));
            Assert.IsTrue(aperture.Contains(new VVector(0.0, -20.0, 0.0)));
            Assert.IsTrue(aperture.Contains(new VVector(0.0, 20.0, 0.0)));
        }

        [TestMethod]
        public void ContainsUsesDivergentCoordinatesBeforeAndAfterIsocentre()
        {
            StaticBeamAperture aperture = CreateAperture(0.0);

            Assert.IsTrue(aperture.Contains(new VVector(5.0, 0.0, -500.0)));
            Assert.IsFalse(aperture.Contains(new VVector(5.001, 0.0, -500.0)));
            Assert.IsTrue(aperture.Contains(new VVector(20.0, 0.0, 1000.0)));
            Assert.IsFalse(aperture.Contains(new VVector(20.001, 0.0, 1000.0)));
        }

        [TestMethod]
        public void ContainsAppliesCollimatorRotatedJawOpening()
        {
            StaticBeamAperture aperture = CreateAperture(90.0);

            Assert.IsTrue(aperture.Contains(new VVector(10.0, 0.0, 0.0)));
            Assert.IsFalse(aperture.Contains(new VVector(0.0, 11.0, 0.0)));
        }

        [TestMethod]
        public void ConstructorRejectsMissingComponents()
        {
            BeamProjection projection = CreateProjection(0.0);
            JawAperture jaws = new JawAperture(new VRect<double>(-10.0, -20.0, 10.0, 20.0));

            Assert.ThrowsException<ArgumentNullException>(() => new StaticBeamAperture(null, jaws));
            Assert.ThrowsException<ArgumentNullException>(() => new StaticBeamAperture(projection, null));
        }

        private static StaticBeamAperture CreateAperture(double collimatorAngleDegrees)
        {
            BeamProjection projection = CreateProjection(collimatorAngleDegrees);
            var jaws = new JawAperture(new VRect<double>(-10.0, -20.0, 10.0, 20.0));
            return new StaticBeamAperture(projection, jaws);
        }

        private static BeamProjection CreateProjection(double collimatorAngleDegrees)
        {
            var coordinates = new BeamCoordinateSystem(
                new VVector(0.0, 0.0, -1000.0),
                new VVector(0.0, 0.0, 0.0),
                new VVector(0.0, 1.0, 0.0),
                collimatorAngleDegrees);
            return new BeamProjection(coordinates);
        }
    }
}
