# BreastSurrogate standalone batch audit requirements

**Status:** Agreed initial requirements for the future Phase 12 standalone executable  
**Safety:** Read-only ESAPI operation; no patient, course, plan, structure or dose modification

Implementation sequencing and completion checklists are maintained in
`docs/phase12-implementation-plan.md`.

## 1. Purpose

Create a patient-level audit dataset that relates:

- raw geometric gILF/gHIF from the planning-physics tangent plan;
- legacy ILF/HIF derived from configured structures in that plan's structure set;
- configurable lung and heart DVH metrics from the reviewed planning copy of
  the final clinical plan;
- later clinical endpoints or clinical-goal results selected for the audit.

The executable must not use or modify the completed clinical course. The
reviewed copy in the planning course is the dose/DVH source.

## 2. Expected course and plan roles

### 2.1 Completed clinical course

A patient may have a numbered clinical course containing the delivered or
completed clinical plan. This course is deliberately excluded from discovery
and metric extraction. Its plan is not required merely because it may be the
most highly approved plan in the patient record.

### 2.2 Planning course: dose/DVH source

Planning-course candidates have IDs containing `PLANNING`, matched
case-insensitively. More than one such course may exist for different treatment
episodes. A candidate course must contain:

- at least one external plan with
  `PlanSetup.ApprovalStatus == PlanSetupApprovalStatus.Rejected`; and
- exactly one external plan with
  `PlanSetup.ApprovalStatus == PlanSetupApprovalStatus.Reviewed`.

The rejected plan is a course-identification signal only and is never used for
DVH extraction. The unique reviewed plan is the intended dose/DVH source.
Missing dose or a missing metric structure makes the affected metrics
unavailable; it does not remove the patient from the output or stop the batch.

If no planning course has this status signature, reject the patient row. If
more than one planning course has it, reject the row unless the CSV input gives
an exact planning-course ID override. Even with a course override, more than
one reviewed plan in that course prevents clinical-plan metric calculation for
that patient; do not choose by date, display state, plan-name similarity or the
completed clinical course. Still write an output row containing the discovery
failure and any independently calculable physics results.

### 2.3 Planning-physics course: geometric and legacy-surrogate source

The geometry source is the PPHYS course and its PPHYS plan. Historic typing is
not fully consistent: `PHYS` may appear instead of `PPHYS`.

Default discovery uses case-insensitive complete-token matches for `PPHYS` or
the historic typo `PHYS` in both course and plan IDs, for example
`PPHYS RT BRST` or `PHYS RT BRST`. The token may be delimited by spaces,
underscores, hyphens, or an ID boundary; fuzzy spelling matches are not used.
Require exactly one matching course and exactly one matching external plan,
unless exact IDs are supplied in the CSV row. Ambiguous automatic discovery
prevents physics-plan calculations for that row, but the row must still be
exported and the batch must continue.

The selected physics plan must pass the same BreastSurrogate restrictions as
interactive use: HFS, static couch-zero `ANT MED` and `POST LAT` beams,
supported Millennium 120 MLC geometry, valid structure set and image.

## 3. Batch input

The patient list is supplied as CSV so it can be prepared/exported from Excel.
Each row requires:

- patient ID.

Optional per-row overrides should include:

- planning course ID;
- physics course ID;
- physics plan ID.

All resolved IDs and discovery methods must be written to the output. Missing
or ambiguous discovery is a row-level rejection and must not stop the remaining
batch.

## 4. Configurable DVH metrics

DVH metrics are evaluated on the reviewed plan in the planning course, never on
the completed clinical-course plan or the PPHYS/PHYS geometry plan.

The metric configuration should use semantic structure selectors rather than
hard-coding one template name:

- `IpsilateralLung` uses the BreastSurrogate lung selector (`IPS LUNG` first,
  then recognized left/right whole-lung aliases selected using the reviewed
  plan's treatment isocentre convention);
- `Heart` considers non-empty structures whose IDs contain `Heart`,
  case-insensitively. Prefer an exact `Heart` ID. If multiple candidates remain,
  normalize IDs to uppercase alphanumeric text and choose the candidate with
  the smallest edit distance from `HEART`; an equal best-distance tie is
  unavailable rather than resolved by collection order;
- additional structures require explicit selectors.

For reviewed-plan DVH metrics, `IpsilateralLung` must be resolved independently
within the reviewed plan's own structure set; do not reuse the physics-plan
`Structure` object. Use the reviewed plan's single distinct treatment
isocentre as the distance reference and apply the same selection rules as the
interactive calculation:

1. prefer a unique, non-empty case-insensitive `IPS LUNG` match;
2. otherwise consider only recognized whole-lung aliases such as `Lung_L`,
   `Lung_R`, `L Lung`, `R Lung`, `Left Lung` and `Right Lung`;
3. select the usable candidate whose documented `Structure.CenterPoint` is
   closest in three-dimensional DICOM distance to that isocentre;
4. reject missing candidates or a distance tie within `0.01 mm`.

If the reviewed plan has no treatment isocentre or more than one distinct
treatment isocentre, its lung/heart DVH metrics are unavailable.
Multi-isocentre reviewed plans are outside the audit scope and are not rescued
by a structure override. Preserve independently calculable physics results and
log the reviewed-plan isocentres, all lung candidates and the failure reason.

Required metric types and notation are:

- `MeanDose`: `Dmean` reported in Gy;
- `VolumeAtDose` with relative volume: `VxGy(%)`;
- `VolumeAtDose` with absolute volume: `VxGy(cc)`;
- `DoseAtVolume` with absolute volume: `Dxcc(Gy)`;
- `DoseAtVolume` with relative volume: `Dx%(Gy)`.

General configuration is JSON. Metric names remain explicit in the export so
the value, query and units are unambiguous.

```json
{
  "courseDiscovery": {
    "planningCourseIdContains": "PLANNING",
    "requireRejectedPlan": true,
    "requiredReviewedPlanCount": 1,
    "physicsCourseTokenPattern": "(?:^|[ _-])(PPHYS|PHYS)(?:$|[ _-])",
    "physicsPlanTokenPattern": "(?:^|[ _-])(PPHYS|PHYS)(?:$|[ _-])"
  },
  "dvh": {
    "binWidthGy": 0.01,
    "metrics": [
      {
        "name": "IpsilateralLung_V8Gy_Percent",
        "structure": "IpsilateralLung",
        "type": "VolumeAtDose",
        "doseGy": 8.0,
        "volumePresentation": "RelativePercent"
      },
      {
        "name": "IpsilateralLung_V12Gy_Percent",
        "structure": "IpsilateralLung",
        "type": "VolumeAtDose",
        "doseGy": 12.0,
        "volumePresentation": "RelativePercent"
      },
      {
        "name": "Heart_Dmean_Gy",
        "structure": "Heart",
        "type": "MeanDose",
        "dosePresentation": "AbsoluteGy"
      }
    ]
  }
}
```

Implementation should use the documented ESAPI planning-item APIs:

- `GetVolumeAtDose(structure, dose, VolumePresentation.Relative)` for `V8 Gy (%)`;
- `GetVolumeAtDose(..., VolumePresentation.AbsoluteCm3)` for `Vx Gy (cc)`;
- `GetDoseAtVolume(..., VolumePresentation.AbsoluteCm3, ...)` for `Dx cc (Gy)`;
- `GetDoseAtVolume(..., VolumePresentation.Relative, ...)` for `Dx% (Gy)`;
- `GetDVHCumulativeData(...)` and `DVHData.MeanDose` for `Dmean`.

Normalize dose values to Gy in the export and retain the native ESAPI unit in
diagnostics. A null/unavailable DVH is a metric-level failure with a reason; it
must not silently become zero.

## 5. Legacy structure-derived ILF/HIF

Legacy ILF/HIF are calculated from structures in the PPHYS/PHYS plan's
structure set:

- the ILF numerator is the unique non-empty structure whose ID contains `ILF`,
  case-insensitively;
- the HIF numerator is the unique non-empty structure whose ID contains `HIF`,
  case-insensitively;
- the ILF denominator is the selected ipsilateral lung (`IPS LUNG` or the
  recognized left/right whole-lung fallback);
- the HIF denominator is selected with the same contains-`Heart`, exact-first,
  closest-string rule used for reviewed-plan DVH metrics.

The stored ILF/HIF structures are treated as the already-created intersection
volumes:

```text
ILF (%) = 100 * ILF structure volume / ipsilateral-lung volume
HIF (%) = 100 * HIF structure volume / Heart volume
```

Zero or multiple ILF/HIF matches are explicit failures for that legacy metric;
the code must not select the first substring match. If Heart or HIF is absent,
record legacy HIF as unavailable with a reason while retaining valid lung
results.

Failure to calculate any one of gILF, gHIF, ILF or HIF must be represented as
an unavailable value with a status/reason. Calculate all other results whose
dependencies remain available; no individual surrogate failure stops the
patient row or the remaining batch.

## 6. Output and provenance

The initial flat output table contains at least:

- patient ID;
- resolved PPHYS/PHYS plan ID;
- resolved reviewed clinical planning-plan ID;
- prescribed fraction count from the reviewed clinical planning plan;
- selected ipsilateral-lung structure ID;
- selected Heart structure ID;
- selected ILF structure ID;
- selected HIF structure ID;
- gILF (%), gHIF (%), ILF (%) and HIF (%);
- ipsilateral-lung `V8Gy (%)` and `V12Gy (%)`;
- Heart `Dmean (Gy)`;
- a status and failure reason for every unavailable calculation;
- warnings, discovery failures, configuration version/hash and application
  version.

Resolved course IDs, approval statuses, requested overrides, dose checks and
calculation diagnostics may be included as additional provenance columns or in
the accompanying log.

The output must distinguish missing data, unsupported geometry and calculation
failure. The audit will run on the hospital network, so the initial output is
not de-identified. The patient-list CSV remains a separate input file. The
output CSV defaults to the same controlled directory as the application logs,
with an option to configure a similar approved directory.

## 7. Implementation validation points

Before routine use, validate on representative records that:

1. planning-course approval-status discovery and PPHYS/PHYS token discovery
   match local naming practice;
2. the closest-string Heart selector chooses the intended anatomy where more
   than one ID contains `Heart`;
3. the prescribed fraction count and all three initial DVH metrics agree with
   Eclipse reports;
4. partial failures produce populated rows with explicit unavailable statuses;
5. the chosen log/output directory has the required hospital-network access
   controls.
