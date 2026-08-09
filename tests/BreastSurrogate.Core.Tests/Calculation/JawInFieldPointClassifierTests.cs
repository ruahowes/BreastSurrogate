using System;
using BreastSurrogate.Core.Apertures;
using BreastSurrogate.Core.Calculation;
using BreastSurrogate.Core.Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Core.Tests.Calculation
{
    [TestClass]
    public class JawInFieldPointClassifierTests
    {
        [TestMethod]
        public void SyntheticStructurePointsProduceExpectedApertureUnionAndIntersection()
        {
            StaticBeamAperture field1 = CreateAperture(-10.0, 1.0);
            StaticBeamAperture field2 = CreateAperture(-1.0, 10.0);
            var classifier = new JawInFieldPointClassifier(field1, field2);

            classifier.Add(new VVector(-5.0, 0.0, 0.0));
            classifier.Add(new VVector(5.0, 0.0, 0.0));
            classifier.Add(new VVector(0.0, 0.0, 0.0));
            classifier.Add(new VVector(20.0, 0.0, 0.0));

            InFieldCalculationResult result = classifier.CreateResult(1000.0, 10.0);

            Assert.AreEqual(4L, result.TotalStructurePointCount);
            Assert.AreEqual(2L, result.Field1PointCount);
            Assert.AreEqual(2L, result.Field2PointCount);
            Assert.AreEqual(3L, result.EitherFieldPointCount);
            Assert.AreEqual(1L, result.BothFieldsPointCount);
            Assert.AreEqual(30.0, result.EitherFieldPercentageOfEsapiVolume, 1e-12);
        }

        [TestMethod]
        public void ConstructorRejectsMissingApertures()
        {
            StaticBeamAperture aperture = CreateAperture(-10.0, 10.0);

            Assert.ThrowsException<ArgumentNullException>(
                () => new JawInFieldPointClassifier(null, aperture));
            Assert.ThrowsException<ArgumentNullException>(
                () => new JawInFieldPointClassifier(aperture, null));
        }

        private static StaticBeamAperture CreateAperture(double x1, double x2)
        {
            var coordinates = new BeamCoordinateSystem(
                new VVector(0.0, 0.0, -1000.0),
                new VVector(0.0, 0.0, 0.0),
                new VVector(0.0, 1.0, 0.0),
                0.0);
            var projection = new BeamProjection(coordinates);
            var jaws = new JawAperture(new VRect<double>(x1, -10.0, x2, 10.0));
            return new StaticBeamAperture(projection, jaws);
        }
    }
}
