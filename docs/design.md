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

The reusable execution boundary is `BreastSurrogateRunner.Run(EsapiContext)`.
The Eclipse `VMS.TPS.Script` entry point alone receives `ScriptContext` and
immediately constructs `EsapiContext`. A future standalone executable can
instead construct the same wrapper from the documented `(Patient, PlanSetup)`
constructor. This keeps Eclipse entry-point mechanics out of the calculation
runner and permits both interactive and batch hosts to use the same geometry.

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
- required tangent beam IDs `ANT MED` (field 1) and `POST LAT` (field 2),
  matched case-insensitively during development before the selection UI exists;
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

The unit beam-propagation direction from source to isocentre is:

\[
\hat{d} = \frac{I-S}{|I-S|}
\]

The BLD viewing axis points from the isocentre toward the source because the
collimator/head coordinate system is defined as viewed from the isocentre side:

\[
\hat{w} = \frac{S-I}{|S-I|} = -\hat{d}
\]

### 8.3 Isocentre-plane basis

For the initial HFS, couch-zero implementation, construct an orthonormal basis on the isocentre plane from:

- the isocentre-to-source BLD viewing axis;
- the DICOM patient superior direction;
- the collimator angle.

The implementation must document the chosen cross-product order and collimator rotation sign.

For the HFS, couch-zero convention validated against Eclipse:

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

This convention retains the raw ESAPI collimator angle. It was validated in
Eclipse using opposed gantry angles of 308 and 128 degrees, collimator angles
of 30 and 33 degrees, deliberately asymmetric X and Y jaws, and DICOM points
offset from isocentre in the positive left, posterior and superior directions.
The Core regression tests preserve all observed in/out classifications.

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
rather than silently reordering them. Containment allows a numerical boundary
tolerance of `1e-9 mm` so floating-point projection residue does not classify a
point mathematically on a jaw edge as outside.

Jaw-only geometry is the first complete field model to be implemented and validated.

This allows source position, divergence, gantry geometry, collimator rotation and BLD coordinate signs to be tested before MLC complexity is added.

## 10. MLC aperture

MLC support is added only after jaw-only projection has been validated.

For the initial expected `MLCX` configuration:

1. projected `yBLD` determines the leaf pair;
2. projected `xBLD` is compared with the two leaf-bank positions for that pair;
3. the point must also pass the jaw test.

ESAPI leaf positions are indexed `[bank, leaf]`.

The Core project uses an explicit `MlcGeometryDefinition` containing the
leaf-boundary positions for each supported MLC model. Boundaries are in
millimetres at isocentre, strictly increasing from negative to positive BLD Y.
Leaf intervals are `[lower, upper)`; an internal boundary belongs to the leaf
on its positive-Y side, while the final physical upper boundary belongs to the
final leaf.

The first configured model is the exact clinical identifier `Millennium 120`,
observed in the development logs with `LeafPositions` dimensions `2 x 60`.
Its configured 400 mm span consists of:

- leaf pairs 0-9: 10 mm width, from -200 mm to -100 mm;
- leaf pairs 10-49: 5 mm width, from -100 mm to +100 mm;
- leaf pairs 50-59: 10 mm width, from +100 mm to +200 mm.

Core MLC positions retain the documented ESAPI `[bank, leaf]` convention:
bank 0 is the negative MLC-X bank and bank 1 is the positive MLC-X bank. A
finite-width opening requires `bank0 < bank1`; equal or crossed tips are
classified as closed. Bank edges are inclusive with the same `1e-9 mm`
floating-point tolerance as jaws. The point must pass both jaw and MLC tests.

The ESAPI project selects the configured definition from `Beam.MLC.Model`.
Phase 9 Eclipse review confirmed that the logged representative leaves and bank
positions correspond to the displayed aperture when ESAPI leaf index 0 maps to
the most negative BLD-Y pair. Differences in Eclipse display units/scale did
not change the observed correspondence.

No model should be accepted unless its leaf geometry has been explicitly configured and validated.

The first MLC implementation supports only this known clinical TrueBeam model.
Additional models can be added later after their identifiers and physical
boundaries are explicitly verified.

### 10.1 Deferred effective leaf-tip calibration

The primary gILF/gHIF definition remains the unexpanded geometric jaw-and-MLC
aperture. Eclipse 50%-dose structures may extend beyond this aperture because
they also reflect finite source size, scatter, depth, heterogeneity, beam
profile, transmission and rounded MLC leaf-tip attenuation. A fixed leaf-tip
offset must therefore not be introduced merely to match one plan.

During the later audit/clinical-validation phase, retain the raw geometric
result and investigate whether a separate, explicitly labelled effective
leaf-tip offset consistently reduces bias against the legacy 50%-dose metric.
If investigated, an offset `delta` expands only the MLC opening by changing the
negative-X bank position to `position - delta` and the positive-X bank position
to `position + delta`. The locally commissioned dosimetric leaf gap may inform
candidate values, but no value is accepted without multi-plan validation. Raw
and adjusted results must remain distinguishable and the raw result must not be
silently replaced.

## 11. Static control-point validation

A nominally static beam can still contain more than one control point.

Before constructing the aperture, verify that all relevant control points have unchanged:

- gantry angle;
- collimator angle;
- patient support angle;
- jaw positions;
- leaf positions.

If these differ materially, the beam is outside the version-1 scope and the calculation should stop for that beam.

Phase 5 introduced provisional integration tolerances of 0.01 degrees for
gantry, collimator and patient-support angles, and 0.01 mm for jaw and leaf
positions. Angles are compared circularly. Phase 9 validates that each control
point has a finite `2 x 60` leaf array and that every leaf position is unchanged
within the position tolerance. Control point 0 then supplies the source angle,
collimator, jaws and leaf positions used to construct the static Core aperture.
These tolerances must be reviewed against a wider set of clinical plans before
version 1 validation is complete.

## 12. Structure sampling

### 12.1 Primary approach

Sample the structure using the associated image voxel grid. At full image
resolution, process each `(y, z)` row as an X-axis scanline:

1. convert the first and last candidate voxel centres to DICOM `VVector` values;
2. obtain all row membership values with one documented
   `Structure.GetSegmentProfile(start, stop, BitArray)` call;
3. for structure voxels, test the point against each selected beam aperture;
4. increment:
   - total structure sample count;
   - field-1 count;
   - field-2 count;
   - union count;
   - intersection count.

Sampling should be restricted to the structure's mesh-geometry bounding box rather than the full image.

The Phase 6 ESAPI sampler converts both DICOM endpoints of each mesh-bound
axis with the shared `VoxelUtilities.DicomToVoxel_*` helpers. Because those
helpers return truncated integer indices, the candidate range is expanded by
one voxel on each side and then clamped to the valid image dimensions. This
prevents a contour-edge voxel from being omitted due to truncation while
retaining the bounding-box performance benefit.

For ESAPI integration, structure ID matching is case-insensitive. Ipsilateral
lung selection first looks for the established ID `IPS LUNG`. If it is present,
it must be non-empty and is used without applying a fallback. If it is absent,
the selector considers a fixed set of left/right whole-lung aliases. Separators
and case are normalized, allowing `Lung_L`, `L Lung`, `Left Lung`, `LT Lung`
and the corresponding right-sided forms. The normalized value must still match
one of those complete aliases; names such as `Lung-PTV` remain ineligible. This
prevents derived lung structures from being selected merely because their IDs
contain the word "lung".

Usable fallback candidates must have a segment and be non-empty. Their
documented ESAPI `Structure.CenterPoint` values are ranked by three-dimensional
Euclidean distance in DICOM millimetres to the `ANT MED` beam isocentre. The
nearest candidate is selected. A distance tie within `0.01 mm`, duplicate
case-insensitive `IPS LUNG` matches, invalid centres or no usable recognized
candidate cause an explicit rejection. The reference isocentre, selection
method, every candidate centre/distance and the selected ID are logged.

`Heart` remains optional. When it is absent, lung sampling continues normally
and the absence is recorded in the log.

The initial per-voxel `Structure.IsPointInsideSegment` implementation remains
the Eclipse validation reference. Segment-profile counts must agree with those
reference counts before the batched method is accepted.

### 12.2 Sample weighting

All image voxels in a given image have the same voxel volume:

\[
V_{voxel}=XRes \times YRes \times ZRes
\]

Phase 6 showed close agreement between sampled and ESAPI-reported structure
volumes on the initial full lung and heart. The calculation will therefore use
the ESAPI `Structure.Volume` value in cubic centimetres as the denominator,
while the sampled in-field voxel volume supplies the numerator:

\[
gIF = 100 \times
\frac{N_{infield} \times V_{voxel}}
{1000 \times V_{ESAPI}}
\]

The sampled structure volume can also be calculated:

\[
V_{sampled}=N_{structure}\times V_{voxel}
\]

and compared with `Structure.Volume` as a useful diagnostic.

### 12.3 Performance

Retain full-resolution image-voxel sampling for correctness, but batch
structure-membership queries into X-axis segment profiles. If runtime remains
excessive during the jaw-only calculation, the next optimisation is to identify
the portions of each scanline that intersect the union of the selected jaw
apertures and request structure membership only over those portions. Any such
clipping must use the divergent 3D aperture classifiers; a fixed rectangular
patient-space crop would not be geometrically valid.

A configurable stride or other coarser sampling remains deferred until it has
been compared with the full-resolution reference in a convergence study.

Phase 11 is complete for the current single-plan calculation. Full-resolution
segment-profile sampling and jaw/MLC classification were observed to be fast
enough for present interactive use, so no stride or reduced-resolution mode is
currently justified. Performance should be reassessed for a future search that
evaluates many synthetic beam candidates per patient.

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

## 16. Batch audit and user interaction

### 16.1 Batch-ready execution boundary

`BreastSurrogateRunner` accepts the shared-library `EsapiContext`, not
`ScriptContext`. Patient opening, course/plan lookup and context lifetime belong
to the host. This repository must not acquire broader ARIA access merely because
a separate standalone host is planned.

The future batch host will be a separate, read-only ESAPI executable. Its input
will identify patient, course and plan records. For each record it should open
the patient through the supported ESAPI application workflow, locate the
requested plan explicitly, construct `EsapiContext(patient, plan)`, run the
geometry calculation, record success or a structured rejection, and close the
patient before continuing sequentially.

The audit dataset is intended to combine:

- raw geometric gILF/gHIF;
- legacy ILF/HIF calculated from explicitly configured structures in the
  structure set;
- lung and heart clinical-goal/DVH metrics from the explicitly identified
  reviewed planning-course copy of the final clinical plan;
- plan, beam, structure and calculation diagnostics needed to interpret a
  failure or discrepancy.

Detailed discovery, metric configuration and provenance requirements are
maintained in `docs/batch-audit-requirements.md`. In summary, the completed
clinical course is excluded. A planning-course candidate has `PLANNING` in its
ID and contains both a rejected plan and exactly one reviewed external plan.
More than one reviewed plan prevents clinical-plan metric calculation for that
patient row. Raw geometry and legacy structures come from a uniquely resolved
PPHYS/PHYS plan; DVH metrics come from the reviewed planning-course plan. A
reviewed plan with no isocentre or more than one distinct isocentre is
unsupported for DVH extraction. Each independent calculation records an
unavailable status on failure so the row and remaining batch are retained.

The reviewed-plan DVH calculation resolves `IpsilateralLung` semantically in
that plan's own structure set. It prefers `IPS LUNG`; otherwise it applies the
same recognized whole-lung alias and centre-to-isocentre selection using the
reviewed plan's own single treatment isocentre. Physics-plan ESAPI `Structure`
objects must not be reused for reviewed-plan DVH queries.

The batch patient list is CSV and general configuration is JSON. Supported DVH
requests are mean dose, volume at absolute dose in percent or cubic centimetres,
and dose at absolute or relative volume. Legacy numerator structures are found
by IDs containing `ILF` or `HIF`; their denominators are the selected
ipsilateral lung and Heart respectively. Ambiguous structures or plans are
reported explicitly rather than selected heuristically. Heart selection is an
exception with a defined rule: exact `Heart` first, then the unique closest
normalized string among IDs containing `Heart`. Batch outputs and logs are
patient-identifiable and remain on the hospital network in the same or a
similarly controlled directory.

For true unattended use, calculation and presentation will need separating so
that a reusable service returns structured results without displaying message
boxes. The current runner/context change is the first enabling step, not the
complete batch host.

### 16.2 Deferred interactive UI

The initial UI should be intentionally simple.

Required selections:

- tangent beam 1;
- tangent beam 2;
- ipsilateral lung;
- heart, when present.

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

1. acceptable static-angle/position tolerances;
2. eventual automatic beam/structure selection rules;
3. clinical thresholds for gILF/gHIF.

## 19. Future research: geometric tangent candidate search

A possible post-version-1 extension is a read-only search over candidate
tangent geometries. It could vary gantry angle, collimator angle, jaws and
static MLC positions, derive an aperture that covers an explicitly selected PTV
contour in BEV with defined margins, and rank deliverable candidates using raw
gILF/gHIF and other declared geometric objectives.

This must be described as geometric candidate ranking, not automatic selection
of the clinically best plan. Minimizing gILF alone does not establish adequate
target dose, acceptable heart/breast/contralateral exposure, robustness,
clearance or overall deliverability. Candidate generation therefore requires
explicit coverage constraints, machine limits, allowed angle ranges, collision
rules, laterality conventions and manual clinical review. Promising geometric
candidates must be validated against calculated dose before clinical use.

The extension remains read-only: it simulates candidate geometry in Core and
must not create or modify Eclipse beams or plans. ESAPI should extract the
required contours and structure samples once; candidate construction and
ranking should then use plain/Core data. If the candidate count makes runtime
important, consider staged coarse-to-fine angle searches, reusing sampled organ
points, rejecting candidates by cheap coverage/deliverability checks first,
and parallelizing only ESAPI-independent Core work after all API data have been
extracted safely.
