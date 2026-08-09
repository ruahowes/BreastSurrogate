# BreastSurrogate — Validation Plan

**Status:** Preliminary. This document is intentionally high-level and should be expanded once the implementation and available Eclipse test cases are known.

## 1. Purpose

Validation has two distinct objectives:

1. **Software/geometric validation:** establish that the script correctly classifies patient-space points as inside or outside the intended static beam aperture.
2. **Clinical surrogate validation:** establish how the new geometric gILF/gHIF metrics relate to the current 50%-isodose-derived ILF/HIF values and to final optimised plan dose endpoints.

These should not be conflated. A geometrically correct implementation does not automatically prove that an existing clinical threshold remains valid.

## 2. Stage A — Core unit testing

This stage is performed without Eclipse patient data.

Validate:

- vector operations;
- source-to-isocentre-plane projection;
- beam divergence;
- BLD-plane basis construction;
- collimator rotation;
- jaw boundary classification;
- MLC leaf-pair mapping;
- MLC bank classification;
- union/intersection logic for two beams.

Use synthetic geometries with analytically known answers.

Particular attention should be paid to tests that detect mirrored or sign-reversed coordinate systems rather than testing only symmetric fields.

## 3. Stage B — ESAPI beam geometry validation in Eclipse

Use simple plans/test patients with known field geometry.

For each test, retain the debug log.

Check:

- `Beam.IsocenterPosition`;
- `Beam.GetSourceLocation(...)`;
- gantry/collimator/couch angles;
- jaw positions;
- MLC model;
- leaf positions;
- calculated beam-plane axes;
- selected test-point projections.

Run several gantry orientations and at least collimator 0° plus a clearly rotated collimator.

The purpose is to prove that Core BLD coordinates correspond to the orientation shown by Eclipse.

`Beam.GetStructureOutlines(structure, true)` should be used as an independent BEV reference where helpful.

Do not accept the MLC implementation until the jaw-only coordinate system has first been validated.

## 4. Stage C — Structure sampling validation

For representative structures:

- log the image size/resolution;
- log the sampled voxel-index range;
- calculate sampled structure volume;
- compare with `Structure.Volume`;
- rerun to establish repeatability.

Suggested structures:

- ipsilateral lung;
- heart;
- a small/simple structure where available;
- a deliberately created geometric test structure if available in the test patient.

The sampled volume is not expected to be exactly identical to ESAPI's reported volume because the methods of representing the contour boundary may differ. The comparison is primarily a sanity and convergence check.

Large or orientation-dependent discrepancies require investigation.

## 5. Stage D — Jaw-only end-to-end behaviour

Before including the MLC, calculate jaw-only in-field percentages.

Make deliberate jaw movements and verify:

- moving a relevant jaw into the lung increases/decreases gILF in the expected direction;
- moving it away reverses the change;
- field-1 and field-2 diagnostic values identify the expected field;
- repeated runs without plan changes produce identical results.

This provides an end-to-end test of:

```text
ESAPI beam → geometry → structure sampling → aperture classification → result
```

without the added MLC complexity.

## 6. Stage E — MLC validation

Use fields with visually simple MLC apertures.

Where practical include:

- large rectangular opening;
- a closed or nearly closed region;
- asymmetric leaf opening;
- field edge passing through a simple structure;
- collimator 0°;
- rotated collimator;
- medial and lateral tangents;
- left and right breast plans.

For targeted points near the field boundary, log:

- DICOM point;
- projected `xBLD/yBLD`;
- selected leaf index;
- bank positions;
- final in/out result.

Check these against the Eclipse BEV/aperture display.

## 7. Stage F — Sampling convergence and runtime

Full-resolution image-voxel sampling is the reference implementation.

If reduced sampling is required for speed, compare each candidate sampling scheme with full resolution on representative cases.

Record:

- sampling resolution/stride;
- gILF;
- gHIF;
- sampled structure volume;
- calculation time.

Choose a reduced sampling scheme only if the change in output is demonstrably negligible for the intended clinical use.

Numerical acceptance limits should be set after observing the real data rather than chosen arbitrarily in advance.

## 8. Stage G — Comparison with the current clinical workflow

For a retrospective set of breast cases, collect where available:

- current dose-derived ILF%;
- new gILF%;
- final optimised lung dose endpoint(s);
- current dose-derived HIF%;
- new gHIF%;
- final optimised heart dose endpoint(s);
- laterality;
- relevant technique/fractionation information.

Assess:

1. relationship between gILF and current ILF;
2. relationship between gHIF and current HIF;
3. relationship between gILF and final lung endpoint;
4. relationship between gHIF and final heart endpoint;
5. systematic offset between geometric and dose-derived surrogates;
6. outliers and whether they correspond to identifiable geometric/clinical factors.

Regression/correlation can assess predictive relationship.

Agreement analysis such as Bland–Altman is useful when comparing the new geometric metric with the existing dose-derived metric because strong correlation alone does not exclude systematic bias.

## 9. Clinical thresholds

Do **not** directly reuse the existing ILF/HIF thresholds unless validation demonstrates that this is justified.

Possible outcomes include:

- the geometric result closely matches the existing metric and the same threshold remains appropriate;
- the geometric result is systematically shifted and needs a recalibrated threshold;
- the geometric result has a different but useful relationship with the final dose endpoint;
- the geometric result is not sufficiently predictive for one of the organs.

Threshold derivation should therefore be treated as a later clinical-validation task, not as part of the geometry implementation.

## 10. Evidence to retain

During development/validation retain:

- code version/commit;
- shared-library version;
- ESAPI version;
- debug logs;
- test patient/plan identifiers;
- relevant screenshots of Eclipse BEV where used;
- calculated gILF/gHIF;
- current dose-derived ILF/HIF where compared;
- any reference spreadsheet/statistical analysis.

This evidence can later be converted into the formal local software verification/clinical validation record.

## 11. Items to define later

The following remain intentionally TBD:

- formal number of Eclipse geometry test cases;
- required range of gantry/collimator angles;
- exact supported MLC model(s);
- quantitative software acceptance tolerances;
- acceptable sampling convergence tolerance;
- required clinical validation sample size;
- final statistical method for threshold derivation;
- production release/approval criteria.

