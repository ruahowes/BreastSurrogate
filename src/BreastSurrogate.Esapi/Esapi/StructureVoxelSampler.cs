using System;
using System.Collections;
using System.Diagnostics;
using System.Windows.Media.Media3D;
using BreastSurrogate.Core.Apertures;
using BreastSurrogate.Core.Calculation;
using Uclh.XRT.Esapi.Utilities;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace BreastSurrogate.Esapi.Esapi
{
    /// <summary>
    /// Samples full-resolution ESAPI structure voxel centres and classifies two jaw apertures.
    /// </summary>
    public sealed class StructureVoxelSampler
    {
        private const int TruncatedIndexMarginVoxels = 1;

        public StructureVoxelSamplingResult Sample(
            Structure structure,
            Image image,
            StaticBeamAperture field1,
            StaticBeamAperture field2)
        {
            ValidateInputs(structure, image, field1, field2);

            Rect3D bounds = structure.MeshGeometry.Bounds;
            VoxelIndexRange xRange = CreateRange(
                VoxelUtilities.DicomToVoxel_x(image, bounds.X),
                VoxelUtilities.DicomToVoxel_x(image, bounds.X + bounds.SizeX),
                image.XSize,
                "X");
            VoxelIndexRange yRange = CreateRange(
                VoxelUtilities.DicomToVoxel_y(image, bounds.Y),
                VoxelUtilities.DicomToVoxel_y(image, bounds.Y + bounds.SizeY),
                image.YSize,
                "Y");
            VoxelIndexRange zRange = CreateRange(
                VoxelUtilities.DicomToVoxel_z(image, bounds.Z),
                VoxelUtilities.DicomToVoxel_z(image, bounds.Z + bounds.SizeZ),
                image.ZSize,
                "Z");

            long candidateVoxelCount = checked(
                (long)xRange.Count * yRange.Count * zRange.Count);
            long insideStructureVoxelCount = 0;
            long structureMembershipQueryCount = 0;
            var inFieldClassifier = new JawInFieldPointClassifier(field1, field2);
            var profileBuffer = new BitArray(xRange.Count);
            var stopwatch = Stopwatch.StartNew();

            for (int z = zRange.Minimum; z <= zRange.Maximum; z++)
            {
                for (int y = yRange.Minimum; y <= yRange.Maximum; y++)
                {
                    VVector start = VoxelUtilities.VoxelToVVector(
                        image,
                        xRange.Minimum,
                        y,
                        z);

                    if (xRange.Count == 1)
                    {
                        structureMembershipQueryCount++;
                        if (structure.IsPointInsideSegment(start))
                        {
                            insideStructureVoxelCount++;
                            inFieldClassifier.Add(start);
                        }

                        continue;
                    }

                    VVector stop = VoxelUtilities.VoxelToVVector(
                        image,
                        xRange.Maximum,
                        y,
                        z);
                    profileBuffer.SetAll(false);
                    SegmentProfile profile = structure.GetSegmentProfile(
                        start,
                        stop,
                        profileBuffer);
                    structureMembershipQueryCount++;

                    if (profile == null || profile.Count != xRange.Count)
                    {
                        throw new StructureVoxelSamplingException(
                            "ESAPI returned an unexpected segment-profile length while sampling structure '"
                            + structure.Id
                            + "'.");
                    }

                    for (int xOffset = 0; xOffset < profileBuffer.Count; xOffset++)
                    {
                        if (profileBuffer[xOffset])
                        {
                            insideStructureVoxelCount++;
                            inFieldClassifier.Add(profile[xOffset].Position);
                        }
                    }
                }
            }

            stopwatch.Stop();
            if (insideStructureVoxelCount == 0)
            {
                throw new StructureVoxelSamplingException(
                    "Full-resolution voxel-centre sampling found no points inside structure '"
                    + structure.Id
                    + "'.");
            }

            double voxelVolumeCubicMillimetres = image.XRes * image.YRes * image.ZRes;
            InFieldCalculationResult inFieldResult = inFieldClassifier.CreateResult(
                voxelVolumeCubicMillimetres,
                structure.Volume);

            return new StructureVoxelSamplingResult(
                structure.Id,
                xRange.Minimum,
                xRange.Maximum,
                yRange.Minimum,
                yRange.Maximum,
                zRange.Minimum,
                zRange.Maximum,
                candidateVoxelCount,
                insideStructureVoxelCount,
                structureMembershipQueryCount,
                voxelVolumeCubicMillimetres,
                structure.Volume,
                inFieldResult,
                stopwatch.ElapsedMilliseconds);
        }

        private static void ValidateInputs(
            Structure structure,
            Image image,
            StaticBeamAperture field1,
            StaticBeamAperture field2)
        {
            if (structure == null)
            {
                throw new ArgumentNullException("structure");
            }

            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            if (field1 == null)
            {
                throw new ArgumentNullException("field1");
            }

            if (field2 == null)
            {
                throw new ArgumentNullException("field2");
            }

            if (!structure.HasSegment || structure.IsEmpty)
            {
                throw new StructureVoxelSamplingException(
                    "Structure '" + structure.Id + "' has no non-empty segment to sample.");
            }

            if (!IsFinitePositive(structure.Volume))
            {
                throw new StructureVoxelSamplingException(
                    "Structure '" + structure.Id + "' has an invalid ESAPI volume.");
            }

            if (structure.MeshGeometry == null || structure.MeshGeometry.Bounds.IsEmpty)
            {
                throw new StructureVoxelSamplingException(
                    "Structure '" + structure.Id + "' has no non-empty mesh bounds to sample.");
            }

            if (image.XSize <= 0 || image.YSize <= 0 || image.ZSize <= 0)
            {
                throw new StructureVoxelSamplingException(
                    "The associated image has invalid voxel dimensions.");
            }

            if (!IsFinitePositive(image.XRes)
                || !IsFinitePositive(image.YRes)
                || !IsFinitePositive(image.ZRes))
            {
                throw new StructureVoxelSamplingException(
                    "The associated image has invalid voxel resolution.");
            }

            Rect3D bounds = structure.MeshGeometry.Bounds;
            if (!IsFinite(bounds.X)
                || !IsFinite(bounds.Y)
                || !IsFinite(bounds.Z)
                || !IsFinite(bounds.SizeX)
                || !IsFinite(bounds.SizeY)
                || !IsFinite(bounds.SizeZ))
            {
                throw new StructureVoxelSamplingException(
                    "Structure '" + structure.Id + "' has non-finite mesh bounds.");
            }
        }

        private static VoxelIndexRange CreateRange(
            int firstEndpoint,
            int secondEndpoint,
            int imageSize,
            string axisName)
        {
            long minimum = Math.Min(firstEndpoint, secondEndpoint) - (long)TruncatedIndexMarginVoxels;
            long maximum = Math.Max(firstEndpoint, secondEndpoint) + (long)TruncatedIndexMarginVoxels;

            if (maximum < 0 || minimum >= imageSize)
            {
                throw new StructureVoxelSamplingException(
                    "The structure mesh bounds do not overlap the image on the "
                    + axisName
                    + " axis.");
            }

            int clampedMinimum = (int)Math.Max(0L, minimum);
            int clampedMaximum = (int)Math.Min(imageSize - 1L, maximum);
            return new VoxelIndexRange(clampedMinimum, clampedMaximum);
        }

        private static bool IsFinitePositive(double value)
        {
            return IsFinite(value) && value > 0.0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private sealed class VoxelIndexRange
        {
            public VoxelIndexRange(int minimum, int maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }

            public int Minimum { get; private set; }

            public int Maximum { get; private set; }

            public int Count
            {
                get { return Maximum - Minimum + 1; }
            }
        }
    }
}
