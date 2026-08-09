# BreastSurrogate — Development Plan

**Status:** Initial implementation plan  
**Approach:** Incremental development with unit-tested geometry first, followed by read-only ESAPI integration and real Eclipse validation.

## 1. Working rules

The project should be developed in small milestones.

For each milestone:

1. inspect the existing implementation and relevant XML documentation;
2. state the assumptions before coding;
3. implement only the current milestone;
4. add or update tests;
5. run the tests;
6. review the diff;
7. run an Eclipse integration check when the milestone depends on ESAPI runtime state;
8. record unresolved geometry assumptions rather than guessing them.

Codex should not be asked to implement the entire tool in one step.

The current `docs/design.md` is the design authority. If implementation requires changing a design decision, update the design document deliberately rather than silently deviating from it.

## 2. Phase 0 — Repository and project scaffold

### Objective

Create a stable solution structure before implementing geometry.

### Tasks

- Create:
  - `src/BreastSurrogate.Core`;
  - `src/BreastSurrogate.Esapi`;
  - `tests/BreastSurrogate.Core.Tests`.
- Add all three projects to `BreastSurrogate.sln`.
- Configure project references:
  - `BreastSurrogate.Esapi -> BreastSurrogate.Core`;
  - `BreastSurrogate.Core.Tests -> BreastSurrogate.Core`.
- Configure `VMS.TPS.Common.Model.Types` in Core and Tests.
- Configure `VMS.TPS.Common.Model.API` only in the ESAPI project.
- Configure `Uclh.XRT.Library` only in the ESAPI project.
- Confirm the target framework and language version match the existing local ESAPI/shared-library environment before changing defaults.
- Ensure `dotnet test` or the chosen local test command runs successfully.
- Add `AGENTS.md` with the project constraints if using Codex routinely.

### Acceptance

- Solution builds with empty/minimal projects.
- Test project runs at least one trivial test.
- Core has no reference to `VMS.TPS.Common.Model.API`.

## 3. Phase 1 — Core vector helpers

### Objective

Provide only the small amount of vector mathematics not already supplied conveniently by `VVector`.

### Likely implementation

`Geometry/VectorMath.cs`

Functions may include:

- dot/scalar product wrapper if useful;
- cross product;
- normalised copy;
- finite-value validation;
- tolerance comparison where the existing shared library is not available to Core.

Avoid creating a duplicate `Point3D` or `Vector3D` hierarchy unless a real requirement emerges. Use `VVector` directly.

### Unit tests

Test:

- addition/subtraction behaviour used by the project;
- dot product;
- cross product orientation;
- cross product orthogonality;
- normalisation;
- zero-length vector rejection;
- numerical tolerance behaviour.

### Acceptance

All tests pass without referencing `VMS.TPS.Common.Model.API`.

## 4. Phase 2 — Beam-plane projection

### Objective

Implement source-to-isocentre-plane projection independently of `Beam` and `ControlPoint`.

### Core models

Suggested starting classes:

```text
BeamCoordinateSystem
BeamProjection
ProjectedBeamPoint
```

The input should be simple data:

- source `VVector`;
- isocentre `VVector`;
- collimator angle or preconstructed beam-plane basis;
- patient/reference superior direction required by the restricted v1 geometry.

The output should expose enough state to debug the calculation:

- projected 3D point;
- `xBLD`;
- `yBLD`;
- optional projection parameter `t`.

### Unit tests

Use analytical geometry where the answer is known exactly.

Minimum tests:

- isocentre projects to `(0, 0)`;
- any valid point on central axis projects to `(0, 0)`;
- points displaced along each beam-plane axis project with the expected magnitude;
- divergence is correct for points before and after isocentre;
- degenerate/parallel geometry is rejected;
- collimator rotation changes the plane coordinates as expected.

### Important restriction

Do not claim the final Varian/IEC sign convention is clinically validated at this phase. The internal mathematics can be tested, but the mapping to Eclipse BEV is confirmed later.

### Acceptance

Projection unit tests pass and no ESAPI API object is used.

## 5. Phase 3 — Jaw aperture

### Objective

Create the first complete geometric field classifier.

### Core classes

Suggested:

```text
JawAperture
StaticBeamAperture
```

`JawAperture` can use `VRect<double>` directly.

`StaticBeamAperture.Contains(patientPoint)` should:

1. project the patient-space point;
2. obtain `xBLD/yBLD`;
3. test the jaw rectangle.

### Unit tests

Test:

- centre of field;
- all four outside directions;
- points exactly on each field edge;
- divergent points at different depths;
- collimator-rotated aperture.

### Acceptance

Jaw-only classification is fully covered by deterministic tests.

## 6. Phase 4 — Read-only ESAPI shell and logging

### Objective

Run a minimal script successfully inside Eclipse before integrating the calculation.

### ESAPI implementation

Create:

```text
Script.cs
Esapi/BreastSurrogateRunner.cs
Diagnostics/...
```

Use:

- `EsapiContext` to wrap `ScriptContext`;
- `Logger` for runtime diagnostics;
- `BeamAnalyzer.GetTreatmentBeams(...)` where useful.

The script should:

1. confirm a patient/plan/image exists;
2. enumerate treatment beams;
3. log plan/image/beam properties;
4. write the log;
5. display a minimal success message.

### Log initially

For each treatment beam:

- ID;
- gantry angle(s);
- collimator angle(s);
- patient support angle(s);
- isocentre;
- source location;
- jaws;
- control-point count;
- MLC model;
- leaf-array dimensions.

### Acceptance

A log file from a real Eclipse plan contains enough information to inspect the geometry without using the debugger.

## 7. Phase 5 — ESAPI beam-to-Core factory, jaws only

### Objective

Construct a validated Core `StaticBeamAperture` from a real ESAPI `Beam`.

### Suggested class

`Esapi/EsapiBeamGeometryFactory.cs`

Responsibilities:

- use first control point for each treatment field (assume no MLC/Jaw modulation at this point);
- obtain source location using the control-point gantry angle;
- obtain isocentre;
- read angles and jaws;
- verify supported HFS/couch-zero/static geometry;
- validate control-point constancy;
- create the Core geometry object.

Do not add MLC classification yet.

### Eclipse validation

Use several deliberately chosen real/test beams.

Inspect/log:

- source/isocentre;
- central-axis vector;
- beam-plane basis;
- jaw coordinates;
- selected test points;
- projected BLD coordinates.

Validate across different gantry and collimator angles.

Use `Beam.GetStructureOutlines(structure, true)` as an independent BEV reference where useful.

### Acceptance

The jaw-only coordinate system agrees with Eclipse for tested field orientations.

**Do not proceed to MLC classification until this is convincing.**

## 8. Phase 6 — Structure voxel sampler

### Objective

Calculate the number of voxel centres inside an ESAPI structure.

### Suggested class

`Esapi/StructureVoxelSampler.cs`

### Algorithm

1. obtain `Structure.MeshGeometry.Bounds`;
2. convert the bounding limits to image voxel-index ranges;
3. clamp ranges to valid image indices;
4. loop only through those voxels;
5. convert each voxel index to DICOM `VVector` with `VoxelUtilities.VoxelToVVector(...)`;
6. call `Structure.IsPointInsideSegment(point)`;
7. emit/count only structure points.

Start at full image resolution.

### Diagnostics

Log:

- image resolution and size;
- structure bounding box;
- voxel index range;
- candidate voxel count;
- inside-structure count;
- calculated sampled volume;
- `Structure.Volume`;
- elapsed time.

### Eclipse checks

Run on:

- ipsilateral lung;
- heart;
- at least one smaller/simple test structure if available.

The sampled volume does not need to match ESAPI volume exactly, but it should be plausibly close and stable. Large discrepancies must be investigated before continuing.

### Acceptance

Structure voxel sampling produces stable counts and acceptable runtime on real data.

## 9. Phase 7 — Jaw-only gILF/gHIF calculation

### Objective

Complete the full calculation path before MLC support.

### Core result model

Suggested:

```text
InFieldCalculationResult
```

Values:

- total structure points;
- field-1 points;
- field-2 points;
- union points;
- intersection points;
- corresponding percentages.

### Algorithm

For every sampled structure point:

```text
in1 = beam1.Contains(point)
in2 = beam2.Contains(point)

field1 += in1
field2 += in2
union += in1 || in2
intersection += in1 && in2
```

### Eclipse checks

Adjust a jaw deliberately and verify:

- expected direction of change;
- repeatability after rerunning;
- sensible field-1/field-2 diagnostic behaviour.

### Acceptance

The application can produce jaw-only gILF and gHIF values from a real plan.

## 10. Phase 8 — MLC geometry definition

### Objective

Implement MLC aperture logic in Core independently of ESAPI objects.

### Tasks

- identify the actual clinical `Beam.MLC.Model` value from logged plans;
- obtain/verify the physical leaf-boundary geometry for that model;
- create `MlcGeometryDefinition`;
- implement mapping from `yBLD` to leaf pair;
- implement `[bank, leaf]` opening test;
- combine MLC and jaw checks.

### Unit tests

Create synthetic leaf arrays and test:

- each leaf region;
- transition exactly at leaf boundaries;
- closed leaves;
- asymmetric openings;
- bank 0 / bank 1 sign behaviour;
- jaw clipping;
- points outside the MLC leaf span.

### Acceptance

MLC aperture logic is deterministic and independently unit tested.

## 11. Phase 9 — ESAPI MLC integration

### Objective

Populate the tested Core MLC model from a real static `ControlPoint`.

### Tasks

- read `ControlPoint.LeafPositions`;
- verify array dimensions;
- identify the MLC model;
- reject unsupported models;
- verify leaf positions are unchanged over relevant control points;
- construct the Core MLC aperture.

### Logging

Log:

- MLC model;
- leaf-array dimensions;
- leaf-boundary definition selected;
- representative bank positions;
- selected leaf index and bank values for targeted debug points.

Do not log the complete leaf array on every run unless specifically needed.

### Eclipse validation

Use simple deliberately shaped fields where the expected opening is visually obvious.

Validate:

- collimator 0°;
- a rotated collimator;
- medial and lateral tangent directions;
- both left and right breast geometry if available.

Compare the projected structure/aperture orientation with Eclipse BEV.

### Acceptance

MLC + jaw point classification agrees with Eclipse for representative static fields.

## 12. Phase 10 — Minimal user interface

### Objective

Make the tool convenient enough for repeated clinical-development testing.

### Initial UI

Allow explicit selection of:

- tangent 1;
- tangent 2;
- ipsilateral lung;
- heart.

Display:

- gILF%;
- gHIF%;
- optionally field-1/field-2 values during development.

Display unsupported-input errors clearly.

Do not implement automatic tangent/laterality inference yet.

### Acceptance

A user can select inputs and recalculate after field changes with minimal steps.

## 13. Phase 11 — Performance and sampling convergence

### Objective

Determine whether full image-voxel sampling is sufficiently fast.

### Measure

For representative lung/heart structures record:

- voxel resolution;
- candidate count;
- structure count;
- projection/classification time;
- total runtime.

If required, implement a sampling stride and compare, for example:

- full resolution;
- every second x/y voxel;
- other clinically sensible reduced samplings.

The full-resolution calculation is the reference.

### Acceptance

Choose the simplest sampling setting with adequate reproducibility and practical runtime. Do not optimise prematurely.

## 14. Phase 12 — Clinical validation and surrogate calibration

### Objective

Determine whether the geometric metric is a useful predictor of final optimised dose and how it relates to the existing 50%-dose surrogate.

This is described separately in `validation-plan.md`.

No existing clinical gILF/gHIF threshold should be assumed valid until this phase is complete.

## 15. Logging checklist during development

The existing `Logger` should be considered part of the development instrumentation.

Recommended pattern:

```text
Execute
 ├─ create context/logger
 ├─ LogMethodStart
 ├─ log selected objects
 ├─ build beam geometry
 │   └─ log geometry inputs/outputs
 ├─ sample structure
 │   └─ LogTiming
 ├─ calculate in-field result
 │   └─ log counts/percentages
 └─ WriteToFile(patientId)
```

During coordinate debugging, log a few deliberately selected points through every transformation step.

Avoid per-voxel logging in normal runs because it will distort performance and create unusable log files.

## 16. How to use Codex for this project

Use Codex for one milestone at a time.

A good task should state:

- the files/documentation to inspect first;
- exactly what is in scope;
- what is explicitly out of scope;
- the tests that must be added;
- the command that must be run;
- what uncertainty must be reported rather than guessed.

Example:

```text
Read AGENTS.md, docs/design.md and docs/development-plan.md.

Implement Phase 2 only: source-to-isocentre-plane projection in
BreastSurrogate.Core.

Use VMS.TPS.Common.Model.Types.VVector directly. Do not reference
VMS.TPS.Common.Model.API. Do not implement jaws or MLCs.

Before changing code, state the proposed beam-plane basis and identify any
coordinate-sign assumptions.

Add analytical unit tests for central-axis projection, off-axis projection,
divergence and degenerate geometry. Run the test suite and summarise the result.
```

For ESAPI milestones, explicitly tell Codex to inspect the matching XML documentation before using an API member and not to invent undocumented members.

A useful separate review prompt is:

```text
Review the current implementation against docs/design.md.

Do not add features. Look specifically for coordinate-system assumptions,
sign errors, unit inconsistencies, unsupported ESAPI geometry being accepted,
missing validation, and tests that could pass despite a mirrored beam
coordinate system.
```

## 17. Version-1 definition of done

Version 1 is complete when:

- the script is read-only;
- two static couch-zero tangents can be selected;
- ipsilateral lung and heart can be selected;
- jaw and validated MLC geometry are included;
- gILF/gHIF are calculated from sampled structure points;
- unsupported geometry is rejected;
- Core geometry has automated unit tests;
- Eclipse integration geometry has been manually validated;
- sampling behaviour/runtime has been characterised;
- debug logging provides sufficient evidence to diagnose geometry problems;
- a clinical validation dataset has been defined or collected before any surrogate thresholds are used operationally.

