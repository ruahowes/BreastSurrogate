# BreastSurrogate — Design

**Status:** Draft design for initial implementation  
**Primary objective:** Calculate geometric ipsilateral lung-in-field (gILF%) and heart-in-field (gHIF%) surrogates directly from treatment-field geometry, without dose calculation and without modifying ARIA/Eclipse data.

## 1. Clinical context

The current workflow estimates lung-in-field (ILF%) and heart-in-field (HIF%) by:

1. placing the breast tangent fields and MLCs;
2. calculating an initial unoptimised dose distribution;
3. generating a structure from the 50% dose level;
4. intersecting that structure with the ipsilateral lung and heart; and
5. calculating the overlap as a percentage of the relevant organ volume.

These values are then used as surrogates for final optimised lung and heart dose endpoints.

The purpose of BreastSurrogate is to calculate an equivalent **geometric surrogate** directly from the beam aperture. This should allow rapid recalculation whenever the tangents or MLCs are adjusted, without recalculating dose or creating temporary structures.

The geometric result is a new metric and must not initially be assumed to be numerically interchangeable with the current 50%-isodose-derived ILF/HIF.

## 2. Definitions

For two selected tangent fields with geometric apertures \(A_1\) and \(A_2\):

\[
gILF = 100 \times
\frac{V(Lung_{ipsi} \cap (A_1 \cup A_2))}
{V(Lung_{ipsi})}
\]

\[
gHIF = 100 \times
\frac{V(Heart \cap (A_1 \cup A_2))}
{V(Heart)}
\]

A sampled point is therefore counted as "in field" when it is inside the open geometric aperture of **either** selected tangent field.

The calculation should also retain the following diagnostic values:

- percentage inside field 1;
- percentage inside field 2;
- percentage inside either field (the primary surrogate);
- percentage inside both fields.

These extra values are primarily for verification and debugging.

## 3. Design principles

1. **Read-only ESAPI operation.** The script must not call `Patient.BeginModifications()` and must not create or alter structures, beams, plans or dose.
2. **Simple first implementation.** Support a deliberately restricted set of conventional static breast tangent geometries before generalising.
3. **Explicit coordinate systems.** All spatial calculations must state the coordinate system and units used.
4. **Test the mathematical geometry outside Eclipse where possible.**
5. **Use real ESAPI value types where this reduces unnecessary conversion.**
6. **Reject unsupported geometry explicitly rather than silently approximating it.**
7. **Use runtime logging extensively for ESAPI integration/debugging, but do not log every sampled voxel in normal operation.**
8. **Treat geometric and clinical validation as separate problems.**

## 4. Solution architecture

The solution will contain two production projects and one test project.

```text
BreastSurrogate/
├── BreastSurrogate.sln
├── README.md
├── AGENTS.md                       # optional but recommended for Codex
│
├── docs/
│   ├── design.md
│   ├── development-plan.md
│   └── validation-plan.md
│
├── src/
│   ├── BreastSurrogate.Core/
│   │   ├── Geometry/
│   │   ├── Apertures/
│   │   ├── Calculation/
│   │   └── Models/
│   │
│   └── BreastSurrogate.Esapi/
│       ├── Script.cs
│       ├── Esapi/
│       ├── Selection/
│       ├── Diagnostics/
│       └── Presentation/
│
└── tests/
    └── BreastSurrogate.Core.Tests/
```

### 4.1 Revised Core/ESAPI boundary

`BreastSurrogate.Core` does **not** need to be completely independent of all Varian assemblies.

The practical boundary is:

- `BreastSurrogate.Core` **may reference `VMS.TPS.Common.Model.Types`** and use lightweight geometry/value types such as `VVector` and `VRect<double>`.
- `BreastSurrogate.Core` **must not reference `VMS.TPS.Common.Model.API`**.
- `BreastSurrogate.Core` must not access `Patient`, `PlanSetup`, `Beam`, `ControlPoint`, `Structure`, `Image`, dose, or any persistent ESAPI object.
- `BreastSurrogate.Esapi` references:
  - `BreastSurrogate.Core`;
  - `VMS.TPS.Common.Model.API`;
  - `VMS.TPS.Common.Model.Types`;
  - `Uclh.XRT.Library`.

This avoids writing unnecessary `VVector` converters while still isolating the mathematical engine from the ESAPI object model and ARIA access.

The test project references `BreastSurrogate.Core` and `VMS.TPS.Common.Model.Types`, so the core beam geometry can be tested using real `VVector` objects without Eclipse.

## 5. Existing library reuse

The following existing `Uclh.XRT.Library` components are expected to be useful in the ESAPI project:

### `EsapiContext`

Use `Uclh.XRT.Esapi.Core.EsapiContext` as the standard wrapper around the supplied `ScriptContext`.

### `Logger`

Use `Uclh.XRT.Esapi.Core.Logger` for runtime diagnostics and timing.

Expected uses include:

- `LogMethodStart()` at important integration boundaries;
- `Log(name, value)` for beam, structure and coordinate state;
- `LogTiming(name, elapsedMs)` around expensive operations;
- `WriteToFile(patientId)` at the end of execution and, where practical, after caught failures.

Logging is an important part of the development process because many ESAPI values can only be meaningfully inspected while the script is running inside Eclipse.

### `BeamAnalyzer`

Potential uses:

- `GetTreatmentBeams(plan)` to exclude setup fields consistently;
- `GetArcs(plan)` when validating that a selected tangent is static;
- `AreVectorsEqual(a, b, tolerance)` where a shared tolerance comparison is useful.

### `VoxelUtilities`

`VoxelUtilities.VoxelToVVector(image, x, y, z)` is particularly useful for structure sampling because it converts image voxel indices directly to DICOM-coordinate `VVector` positions in millimetres.

The corresponding DICOM-to-voxel helpers can be used to restrict sampling to the structure bounding box.

### `MlcUtilities`

The existing upper/lower leaf-number utilities may be useful as cross-checks when MLC support is added. They do not replace the need to define the physical leaf-boundary positions required to map a projected BLD coordinate to a leaf pair.

### Components not required initially

`ResultsOutput` is not required for the first development version. A simple WPF or message output plus the debug log is preferable until the calculation itself is stable.

`ContourGeometry` is also not required for the primary algorithm because ESAPI already provides `Structure.IsPointInsideSegment(VVector)` for 3D structure-membership testing.

## 6. Supported scope for version 1

Initial support is deliberately restricted to:

- external photon treatment plans;
- conventional static tangent fields;
- two selected treatment beams;
- head-first supine patient orientation;
- patient support/couch angle of 0 degrees;
- one static aperture per beam;
- a specifically validated TrueBeam MLC model;
- lung and heart structures with valid segment volumes;
- a volume image associated with the structure set.

Initially unsupported:

- VMAT/arcs;
- sliding-window IMRT;
- multiple static segments;
- couch rotations;
- non-HFS orientations;
- Halcyon;
- electron fields;
- dynamic wedges where the control-point sequence changes relevant aperture geometry;
- multiple incompatible apertures within one beam.

Unsupported inputs must produce a clear user-facing message and a corresponding log entry.

## 7. Coordinate systems and units

The calculation should use **DICOM patient coordinates in millimetres** for all 3D patient-space positions.

Relevant ESAPI values include:

- `Beam.IsocenterPosition`;
- `Beam.GetSourceLocation(gantryAngle)`;
- image voxel positions returned by `VoxelUtilities.VoxelToVVector(...)`.

Jaw and MLC coordinates are expressed in the **IEC BEAM LIMITING DEVICE (BLD)** coordinate system in millimetres.

The central task of the beam-projection code is therefore:

```text
DICOM patient-space VVector
        ↓
ray from source through point
        ↓
intersection with beam isocentre plane
        ↓
BLD x/y coordinates
        ↓
jaw + MLC aperture test
```

No TPS user-coordinate conversion is required for the calculation itself. User coordinates may be used only for display/debugging where helpful.

## 8. Beam geometry model

### 8.1 Required inputs

For each selected static beam, the ESAPI layer extracts:

- beam identifier;
- isocentre: `Beam.IsocenterPosition`;
- source location: `Beam.GetSourceLocation(controlPoint.GantryAngle)`;
- gantry angle;
- collimator angle;
- patient support angle;
- jaw positions;
- leaf positions;
- MLC model identifier;
- control-point count and relevant control-point state.

These values are converted into a Core model such as `StaticBeamGeometry`.

### 8.2 Central axis

Let:

- \(S\) = source position;
- \(I\) = isocentre position.

The unit source-to-isocentre direction is:

\[
\hat{w} = \frac{I-S}{|I-S|}
\]

### 8.3 Isocentre-plane basis

For the initial HFS, couch-zero implementation, construct an orthonormal basis on the isocentre plane from:

- the source-to-isocentre direction;
- the DICOM patient superior direction;
- the collimator angle.

The implementation must document the chosen cross-product order and collimator rotation sign.

For the Phase 2 internal mathematical convention:

\[
\hat{u}_0 = \operatorname{normalize}(\hat{s}_{superior} \times \hat{w})
\]

\[
\hat{v}_0 = \hat{w} \times \hat{u}_0
\]

For a positive collimator angle \(\theta\), the beam-plane axes are rotated as:

\[
\hat{u} = \cos(\theta)\hat{u}_0 + \sin(\theta)\hat{v}_0
\]

\[
\hat{v} = -\sin(\theta)\hat{u}_0 + \cos(\theta)\hat{v}_0
\]

This fixes the sign convention used by the analytical Core tests. It remains
provisional until compared with Eclipse BEV coordinates during integration.

**Important:** the final sign convention is not considered established merely because the mathematics is internally consistent. It must be validated in Eclipse against known field orientations and ESAPI's BEV projection before MLC logic is trusted.

The Core project should provide any missing vector operations (for example a cross product) as small deterministic helpers using `VVector`.

### 8.4 Projection onto the isocentre plane

For patient point \(P\), define the ray:

\[
R(t)=S+t(P-S)
\]

The isocentre plane passes through \(I\) and has normal \(\hat{w}\).

The ray/plane intersection parameter is:

\[
t =
\frac{(I-S)\cdot\hat{w}}
{(P-S)\cdot\hat{w}}
\]

The projected point is:

\[
Q=S+t(P-S)
\]

If \(\hat{u}\) and \(\hat{v}\) are the BLD plane axes:

\[
x_{BLD}=(Q-I)\cdot\hat{u}
\]

\[
y_{BLD}=(Q-I)\cdot\hat{v}
\]

The projection implementation must detect invalid/degenerate cases such as a near-zero denominator.

## 9. Jaw aperture

For a projected point `(xBLD, yBLD)`, the point is inside the jaw opening when it lies within the `VRect<double>` jaw bounds.

The Phase 3 classifier treats the four jaw boundaries as inclusive. It requires
finite, ordered bounds (`X1 <= X2` and `Y1 <= Y2`) and rejects invalid rectangles
rather than silently reordering them.

Jaw-only geometry is the first complete field model to be implemented and validated.

This allows source position, divergence, gantry geometry, collimator rotation and BLD coordinate signs to be tested before MLC complexity is added.

## 10. MLC aperture

MLC support is added only after jaw-only projection has been validated.

For the initial expected `MLCX` configuration:

1. projected `yBLD` determines the leaf pair;
2. projected `xBLD` is compared with the two leaf-bank positions for that pair;
3. the point must also pass the jaw test.

ESAPI leaf positions are indexed `[bank, leaf]`.

The Core project should use an explicit `MlcGeometryDefinition` containing the leaf-boundary positions for each supported MLC model. The ESAPI project selects the correct definition from `Beam.MLC.Model`.

No model should be accepted unless its leaf geometry has been explicitly configured and validated.

The first MLC implementation should support one known clinical TrueBeam model only. Additional models can be added later.

## 11. Static control-point validation

A nominally static beam can still contain more than one control point.

Before constructing the aperture, verify that all relevant control points have unchanged:

- gantry angle;
- collimator angle;
- patient support angle;
- jaw positions;
- leaf positions.

If these differ materially, the beam is outside the version-1 scope and the calculation should stop for that beam.

Phase 5 uses provisional integration tolerances of 0.01 degrees for gantry,
collimator and patient-support angles, and 0.01 mm for jaw and leaf positions.
Angles are compared circularly. After all control points pass this constancy
check, control point 0 supplies the source-angle, collimator and jaw values used
to construct the jaw-only Core aperture. These tolerances must be reviewed
against a wider set of clinical plans before version 1 validation is complete.

## 12. Structure sampling

### 12.1 Primary approach

Sample the structure using the associated image voxel grid.

For each candidate voxel centre:

1. convert `(x, y, z)` image indices to a DICOM `VVector` using `VoxelUtilities.VoxelToVVector(...)`;
2. use `Structure.IsPointInsideSegment(point)` to determine whether the voxel centre is inside the structure;
3. for structure voxels, test the point against each selected beam aperture;
4. increment:
   - total structure sample count;
   - field-1 count;
   - field-2 count;
   - union count;
   - intersection count.

Sampling should be restricted to the structure's mesh-geometry bounding box rather than the full image.

### 12.2 Sample weighting

All image voxels in a given image have the same voxel volume:

\[
V_{voxel}=XRes \times YRes \times ZRes
\]

Therefore, for full-resolution voxel-centre sampling the voxel volume cancels in the percentage:

\[
gIF = 100 \times \frac{N_{infield}}{N_{structure}}
\]

The sampled structure volume can also be calculated:

\[
V_{sampled}=N_{structure}\times V_{voxel}
\]

and compared with `Structure.Volume` as a useful diagnostic.

### 12.3 Performance

Start with full-resolution image-voxel sampling for correctness.

If runtime is excessive, add a configurable integer stride or equivalent coarser sampling only after the full-resolution result exists as a reference. Any default reduction in resolution must be justified by a convergence study.

## 13. Result model

A calculation result should contain at least:

- structure ID;
- total sampled structure points;
- sampled structure volume;
- ESAPI structure volume;
- percentage in field 1;
- percentage in field 2;
- percentage in either field;
- percentage in both fields;
- sample resolution/stride;
- calculation duration;
- warning flags.

The user-facing primary outputs are:

- `gILF%`;
- `gHIF%`.

During development the diagnostic values should also be shown or logged.

## 14. Runtime logging strategy

Logging belongs primarily in `BreastSurrogate.Esapi`, because that is where runtime ESAPI state is acquired.

### Log at startup

- script/library versions;
- patient/course/plan identifiers as already supported by the existing logger workflow;
- image ID, size and resolution;
- patient orientation;
- selected structure IDs;
- selected beam IDs.

### Log per beam

- gantry, collimator and couch angles;
- isocentre;
- source location;
- source-to-isocentre distance;
- jaw positions;
- control-point count;
- MLC model;
- leaf-array dimensions;
- selected/configured MLC geometry definition;
- static-control-point validation result.

### Log per structure calculation

- mesh bounding box;
- voxel index range sampled;
- total candidate voxels;
- total voxels inside structure;
- sampled structure volume;
- ESAPI structure volume;
- field-1/field-2/union/intersection counts and percentages;
- elapsed time.

### Targeted geometry debug logging

Do **not** log every voxel.

When debugging coordinate geometry, explicitly log a small set of selected points and their intermediate values:

- original DICOM point;
- projection parameter `t`;
- projected isocentre-plane point;
- `xBLD`, `yBLD`;
- jaw result;
- selected leaf index;
- MLC bank positions;
- final in/out classification.

This should be enabled only for targeted debug runs.

## 15. ESAPI visual validation support

`Beam.GetStructureOutlines(structure, true)` returns structure outlines projected onto the beam isocentre plane in BEV coordinates.

This should be used as an independent Eclipse-side debugging/validation aid for confirming:

- beam-plane orientation;
- left/right sign;
- superior/inferior sign;
- collimator rotation;
- agreement between the calculated aperture coordinate system and Eclipse BEV.

It is not required for the production calculation itself.

## 16. User interaction

The initial UI should be intentionally simple.

Required selections:

- tangent beam 1;
- tangent beam 2;
- ipsilateral lung;
- heart.

Required output:

```text
Geometric ILF: xx.x %
Geometric HIF: xx.x %
```

During development, display or log the additional per-field diagnostic results.

Automatic tangent detection, automatic laterality selection and threshold interpretation should be deferred until the geometric calculation is validated.

## 17. Safety and failure behaviour

The script must stop with a clear explanation if:

- no plan is loaded;
- no associated structure set/image exists;
- selected structures are empty or invalid;
- a selected beam is not supported;
- couch angle is not within the defined zero-angle tolerance;
- control points describe a changing aperture;
- MLC model is unsupported;
- projection geometry is degenerate;
- no structure sample points are found.

The script must not silently fall back to an approximation.

## 18. Decisions intentionally left open

The following should be resolved during development rather than guessed by Codex:

1. exact BLD axis sign/cross-product convention after Eclipse validation;
2. exact collimator rotation sign;
3. supported TrueBeam MLC model identifier(s);
4. validated leaf-boundary table(s);
5. acceptable static-angle/position tolerances;
6. whether full image-voxel sampling is fast enough for routine use;
7. eventual automatic beam/structure selection rules;
8. clinical thresholds for gILF/gHIF.
