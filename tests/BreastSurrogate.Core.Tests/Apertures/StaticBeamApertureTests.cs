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

        [TestMethod]
        public void ContainsRequiresBothJawAndMlcOpenings()
        {
            BeamProjection projection = CreateProjection(0.0);
            var jaws = new JawAperture(new VRect<double>(-5.0, -20.0, 5.0, 20.0));
            var geometry = new MlcGeometryDefinition("Synthetic", new[] { -20.0, 0.0, 20.0 });
            var mlc = new MlcAperture(
                geometry,
                new[,] { { -2.0, -10.0 }, { 2.0, 10.0 } });
            var aperture = new StaticBeamAperture(projection, jaws, mlc);

            Assert.IsTrue(aperture.Contains(new VVector(0.0, -10.0, 0.0)));
            Assert.IsFalse(
                aperture.Contains(new VVector(3.0, -10.0, 0.0)),
                "Point inside jaws but blocked by the MLC should be outside.");
            Assert.IsFalse(
                aperture.Contains(new VVector(6.0, 10.0, 0.0)),
                "Point outside jaws should be outside even if the MLC is wider.");
        }

        [TestMethod]
        public void AsymmetricXJawsMatchEclipseDeliberatePointClassifications()
        {
            VVector isocentre = new VVector(
                87.748012294912272,
                -295.83164008825003,
                6.0);
            StaticBeamAperture antMed = CreateEclipseAperture(
                new VVector(-700.26274131180946, -911.49311541390853, 6.0),
                isocentre,
                30.0,
                new VRect<double>(-5.0, -20.0, 40.0, 40.0));
            StaticBeamAperture postLat = CreateEclipseAperture(
                new VVector(875.75876590163432, 319.8298352374083, 6.0),
                isocentre,
                33.0,
                new VRect<double>(-40.0, -20.0, 5.0, 40.0));

            Assert.IsTrue(antMed.Contains(isocentre + new VVector(10.0, 0.0, 0.0)));
            Assert.IsFalse(antMed.Contains(isocentre + new VVector(0.0, 10.0, 0.0)));
            Assert.IsTrue(antMed.Contains(isocentre + new VVector(0.0, 0.0, 10.0)));
            Assert.IsTrue(postLat.Contains(isocentre + new VVector(10.0, 0.0, 0.0)));
            Assert.IsFalse(postLat.Contains(isocentre + new VVector(0.0, 10.0, 0.0)));
            Assert.IsFalse(postLat.Contains(isocentre + new VVector(0.0, 0.0, 10.0)));
        }

        [TestMethod]
        public void AsymmetricYJawsMatchEclipseDeliberatePointClassifications()
        {
            VVector isocentre = new VVector(
                87.748012294912272,
                -295.83164008825003,
                6.0);
            VRect<double> jaws = new VRect<double>(-40.0, -10.0, 40.0, 0.0);
            StaticBeamAperture antMed = CreateEclipseAperture(
                new VVector(-700.26274131180946, -911.49311541390853, 6.0),
                isocentre,
                30.0,
                jaws);
            StaticBeamAperture postLat = CreateEclipseAperture(
                new VVector(875.75876590163432, 319.8298352374083, 6.0),
                isocentre,
                33.0,
                jaws);

            Assert.IsTrue(antMed.Contains(isocentre + new VVector(10.0, 0.0, 0.0)));
            Assert.IsFalse(antMed.Contains(isocentre + new VVector(0.0, 10.0, 0.0)));
            Assert.IsFalse(antMed.Contains(isocentre + new VVector(0.0, 0.0, 10.0)));
            Assert.IsFalse(postLat.Contains(isocentre + new VVector(10.0, 0.0, 0.0)));
            Assert.IsTrue(postLat.Contains(isocentre + new VVector(0.0, 10.0, 0.0)));
            Assert.IsFalse(postLat.Contains(isocentre + new VVector(0.0, 0.0, 10.0)));
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

        private static StaticBeamAperture CreateEclipseAperture(
            VVector source,
            VVector isocentre,
            double collimatorAngleDegrees,
            VRect<double> jaws)
        {
            var coordinates = new BeamCoordinateSystem(
                source,
                isocentre,
                new VVector(0.0, 0.0, 1.0),
                collimatorAngleDegrees);
            return new StaticBeamAperture(
                new BeamProjection(coordinates),
                new JawAperture(jaws));
        }
    }
}
