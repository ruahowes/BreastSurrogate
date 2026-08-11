# Phase 12 standalone batch audit implementation plan

**Status:** In progress; milestones 12A and 12B complete, 12C implemented pending standalone-environment check
**Requirements authority:** `docs/batch-audit-requirements.md`  
**Safety:** Read-only ESAPI operation; sequential patient access; no ARIA modification

## 1. Intended architecture

Phase 12 will add a thin standalone ESAPI host around a presentation-free
BreastSurrogate calculation service. The host must consume structured results;
it must not scrape message-box text or diagnostic logs.

| Responsibility | Intended location |
| --- | --- |
| Geometric projection and aperture calculations | Existing `BreastSurrogate.Core` |
| Presentation-free gILF/gHIF service | `BreastSurrogate.Esapi` |
| Lung, Heart, ILF and HIF selection rules | `BreastSurrogate.Esapi` |
| Course and plan discovery | New `BreastSurrogate.Batch` executable |
| ESAPI application and patient lifetime | New `BreastSurrogate.Batch` executable |
| CSV input, JSON configuration and CSV output | New `BreastSurrogate.Batch` executable |
| Generic configurable DVH evaluation | Prefer `Uclh.XRT.Library` if it will be reused |
| Runtime diagnostics | Existing `Uclh.XRT.Esapi.Core.Logger` |

The existing `EsapiContext(Patient, PlanSetup)` constructor is sufficient for
the standalone physics-plan calculation. Construct that context only for the
resolved physics plan. The reviewed clinical planning plan remains a separate
`PlanSetup` used for its own structure selection, fraction count and DVH
queries.

## 2. Shared-library boundary

### 2.1 Recommended shared-library addition

A generic configurable DVH evaluator is a good candidate for
`Uclh.XRT.Library` because it is reusable by other standalone audits and
reports. Its conceptual boundary is:

```csharp
DvhMetricResult Evaluate(
    PlanningItem planningItem,
    Structure structure,
    DvhMetricRequest request)
```

It should support:

- `Dmean`;
- `VGy(%)`;
- `VGy(cc)`;
- `Dcc(Gy)`;
- `D%(Gy)`;
- normalization of absolute dose output to Gy;
- structured unavailable/error results without terminating a patient audit.

The evaluator's request/result contracts must be generic and must not contain
BreastSurrogate course names, structure names or output-column rules. Phase 12
may initially implement this evaluator locally if changing and deploying the
shared library would impede progress; migrate it only with equivalent tests.

### 2.2 Keep within BreastSurrogate

The following are specific to this audit and should not be added to the generic
class library:

- `PLANNING`, `PPHYS` and `PHYS` discovery conventions;
- rejected/reviewed-plan rules;
- ipsilateral-lung, Heart, ILF and HIF selection conventions;
- legacy ILF/HIF formulae;
- the audit JSON and CSV schemas;
- patient-row orchestration and partial-failure policy.

No additional generic application/context wrapper is currently required.
`EsapiContext(Patient, PlanSetup)` already supplies the required boundary.

## 3. Milestone 12A — Structured surrogate service

**Status:** Complete

Refactor `BreastSurrogateRunner` so calculation and presentation are separate.
Add a presentation-free service with a boundary conceptually equivalent to:

```csharp
BreastSurrogateCalculationResult Calculate(EsapiContext context)
```

The structured result records:

- independent gILF and gHIF outcomes;
- selected physics plan, beam, lung and Heart IDs;
- values and units;
- calculation status and failure reason for each organ;
- diagnostics required by the existing log.

The service must not display a message box or write an output file. The current
Eclipse runner becomes a presentation adapter: it calls the service, writes the
existing log and displays the summary. Missing Heart or failed heart sampling
must not discard a valid gILF result. Beam-geometry failure may make both
geometric metrics unavailable because they share that dependency.

### Tasks

- [x] Define structured calculation outcome/result contracts.
- [x] Extract beam discovery and geometry construction from the UI workflow.
- [x] Calculate lung and Heart outcomes independently after shared geometry is valid.
- [x] Move formatting, message boxes and file logging behind the interactive runner.
- [x] Preserve existing diagnostic values and exception details.
- [x] Add tests for all new ESAPI-independent result/status behavior.
- [x] Run the complete Core test suite and build the solution.
- [x] Confirm unchanged Eclipse percentages and equivalent logs.

### Acceptance

Both Eclipse and a future batch host can consume the same structured
calculation without UI or log parsing, and interactive results do not regress.

Implementation added `BreastSurrogateCalculationService`, immutable structured
calculation/metric results and a separate ESAPI-contract test project. The
service contains no message-box, logger or file-output dependency and returns
no persistent ESAPI API object. Automated validation currently passes 57 Core
tests and 6 structured-result tests. Eclipse regression on 11 August 2026
confirmed unchanged gILF/gHIF percentages and equivalent diagnostic content.
The compact `Results.*` metric summary is emitted after the detailed sampling
diagnostics so the headline values remain easy to find near the end of the log.

## 4. Milestone 12B — Reusable structure selectors

**Status:** Complete

Add deterministic selectors for Heart and legacy numerator structures while
continuing to use `IpsilateralLungSelector` independently for each plan.

### Heart rule

1. consider non-empty structures whose IDs contain `Heart`, case-insensitively;
2. prefer the exact ID `Heart`;
3. otherwise normalize candidate IDs to uppercase alphanumeric text and choose
   the unique smallest edit distance from `HEART`;
4. report an equal best-distance tie as unavailable.

### Legacy rule

- ILF is the unique non-empty physics-plan structure whose ID contains `ILF`;
- HIF is the unique non-empty physics-plan structure whose ID contains `HIF`;
- zero or multiple candidates make only the affected legacy metric unavailable.

### Tasks

- [x] Implement pure ID normalization and edit-distance ranking.
- [x] Implement ESAPI Heart selection using the tested ranking result.
- [x] Implement ILF/HIF substring selectors.
- [x] Test exact, case-insensitive, closest, empty, absent, duplicate and tie cases.
- [x] Log all Heart candidates, ranking information and the selection reason.
- [x] Confirm Heart candidate/ranking diagnostics in an Eclipse log.

### Acceptance

Selection is deterministic, collection order cannot affect it, and ambiguity is
represented explicitly without stopping independent calculations.

Implementation separates pure `StructureIdText`, Heart ranking and strict
substring selection from thin ESAPI `Structure` adapters. The geometric gHIF
calculation now uses the Heart selector and returns its complete candidate
diagnostics without exposing a persistent ESAPI object. Legacy ILF/HIF
selectors are implemented and tested but are not invoked until milestone 12F
adds the legacy volume-ratio calculation. Automated validation passes 57 Core
tests and 19 ESAPI-contract tests. Inspection of the representative Eclipse log
on 11 August 2026 confirmed that the Heart candidate and ranking diagnostics
were clear and that selection matched Eclipse.

## 5. Milestone 12C — Standalone executable scaffold

**Status:** Implemented; standalone ESAPI environment check pending

Add an old-style, non-SDK, x64 executable project targeting .NET Framework
4.6.2, provisionally named `BreastSurrogate.Batch`.

It will reference:

- `BreastSurrogate.Esapi`;
- `BreastSurrogate.Core`;
- `Uclh.XRT.Library`;
- `VMS.TPS.Common.Model.API`;
- `VMS.TPS.Common.Model.Types`.

The supplied `docs/testStandAlone.csproj` is a useful example, but it currently
targets .NET Framework 4.6.1 and does not reference the BreastSurrogate or UCLH
assemblies. Do not copy those differences into the production project.

### Tasks

- [x] Create the non-SDK .NET Framework 4.6.2 x64 executable project.
- [x] Add an `[STAThread]` entry point.
- [x] Create exactly one ESAPI `Application` for the process.
- [x] Dispose the application before exit.
- [x] Prohibit `BeginModifications()` and all write-enabled operations.
- [x] Add command-line validation and a non-zero process exit code for fatal
      startup/configuration failures.
- [x] Add a console progress reporter suitable for interactive and redirected
      output, ready to connect to the patient loop in milestone 12G.
- [ ] Confirm startup and clean exit in the supported standalone ESAPI environment.

### Acceptance

The executable starts in the documented ESAPI standalone environment, loads its
configuration and exits cleanly without opening or modifying a patient.

Implementation adds `BreastSurrogate.Batch` as an old-style .NET Framework
4.6.2 x64 executable with an STA entry point, exactly one process-level ESAPI
`Application`, deterministic disposal, input-path validation and explicit exit
codes. An explicit `--check-esapi` mode, also offered as `T` after an
interactive no-argument launch, verifies application creation and disposal
without requiring placeholder input files. It deliberately does not open a patient. The supplied
`docs/ConsoleUtility.cs` informed a tested ASCII progress reporter that updates
one console line interactively and emits durable lines when output is
redirected; patient-by-patient reporting will be wired in during milestone 12G.
Configuration parsing remains milestone 12D. Automated validation currently
passes 57 Core, 19 ESAPI-contract and 9 Batch tests. The remaining 12C check is
to run the scaffold in the hospital standalone ESAPI environment.

## 6. Milestone 12D — Configuration and table I/O

**Status:** Not started

Use a command line conceptually equivalent to:

```text
BreastSurrogate.Batch.exe patients.csv config.json
```

The patient-list CSV contains patient ID plus optional exact planning-course,
physics-course and physics-plan overrides. JSON contains discovery rules,
log/output directory, DVH bin width and metric requests. The initial metrics
are ipsilateral-lung `V8Gy (%)`, ipsilateral-lung `V12Gy (%)` and Heart
`Dmean (Gy)`.

Use a quoted-field-aware CSV implementation suitable for Excel exports. Prefer
framework-provided JSON/CSV facilities or a deliberately controlled dependency
set suitable for deployment on the hospital network.

### Tasks

- [ ] Define versioned configuration and input-row contracts.
- [ ] Parse and validate JSON before creating the ESAPI application where practical.
- [ ] Parse quoted CSV fields, permit repeated patient IDs with different
      treatment overrides, and report malformed or exact duplicate rows clearly.
- [ ] Define stable output columns and invariant-culture numeric formatting.
- [ ] Represent unavailable values as blank values plus explicit status/reason.
- [ ] Default output to the log directory, allowing a similar approved directory.
- [ ] Test quoting, commas, blank overrides, invalid JSON and invalid metric requests.

### Acceptance

Input and configuration errors are reported before patient processing, and CSV
round trips preserve identifiers and diagnostic text safely.

## 7. Milestone 12E — Deterministic plan discovery

**Status:** Not started

Resolve the clinical and physics branches independently after opening a patient.

### Clinical planning branch

- course ID contains `PLANNING`, case-insensitively;
- candidate course contains at least one rejected external plan;
- candidate course contains exactly one reviewed external plan;
- an exact planning-course override may disambiguate courses;
- multiple reviewed plans are never resolved heuristically;
- reviewed plans with no treatment isocentre or multiple distinct treatment
  isocentres are unsupported for clinical DVH extraction.

### Physics branch

- course and plan IDs contain a complete `PPHYS` or `PHYS` token;
- exact physics-course and physics-plan overrides may disambiguate;
- missing or ambiguous selection makes physics metrics unavailable.

Use small immutable discovery snapshots and pure decision functions where
possible, then map the selected IDs back to ESAPI objects. Define and test the
tolerance used when deciding whether treatment-beam isocentres are distinct.

### Tasks

- [ ] Implement case-insensitive planning-course discovery and exact overrides.
- [ ] Implement reviewed/rejected approval-status checks.
- [ ] Implement complete-token PPHYS/PHYS matching and exact overrides.
- [ ] Implement single-distinct-isocentre validation for the reviewed plan.
- [ ] Record every candidate and the final discovery reason.
- [ ] Test missing, unique, ambiguous and overridden cases.

### Acceptance

No plan is chosen by collection order, date, display state or unsupported fuzzy
matching. A failure in one branch does not prevent the other branch from running.

## 8. Milestone 12F — Legacy and clinical metrics

**Status:** Not started

### Physics plan

- construct `EsapiContext(patient, physicsPlan)`;
- calculate gILF and gHIF through the structured service;
- calculate `ILF = 100 * ILF volume / ipsilateral-lung volume`;
- calculate `HIF = 100 * HIF volume / Heart volume`.

### Reviewed clinical planning plan

- record the documented `PlanSetup.NumberOfFractions` value;
- resolve ipsilateral lung and Heart in that plan's own structure set;
- calculate lung V8Gy and V12Gy with `GetVolumeAtDose` and relative volume;
- calculate Heart Dmean from cumulative DVH data and `DVHData.MeanDose`;
- normalize absolute dose output to Gy while retaining native units in diagnostics.

### Tasks

- [ ] Implement the generic DVH evaluator in the shared library or locally
      according to the decision in section 2.
- [ ] Implement legacy volume-ratio evaluation with denominator validation.
- [ ] Implement the three initial configured DVH metrics.
- [ ] Keep physics-plan and reviewed-plan structures strictly separate.
- [ ] Return a status/reason for null dose, null DVH and invalid values.
- [ ] Test metric request dispatch and unit normalization independently of ESAPI
      where possible.

### Acceptance

Every requested value is either a finite value with an explicit unit or an
unavailable outcome with a specific reason; unavailable values never become zero.

## 9. Milestone 12G — Patient loop, fault isolation and export

**Status:** Not started

Process patients sequentially. ESAPI object access remains on the STA thread.
The loop should follow this lifecycle:

```text
open patient
try
    discover both plan branches
    calculate every branch whose dependencies are available
    construct one output row
finally
    write and flush the row where possible
    write diagnostics
    close the patient
```

Create an output row even when a patient cannot be opened, a plan is ambiguous,
Heart is absent, geometry is unsupported, dose/DVH is unavailable or an
individual metric throws. A fatal configuration or application-startup failure
may stop the complete batch; patient and metric failures may not.

The initial flat table contains:

- patient ID;
- resolved PPHYS/PHYS plan ID;
- reviewed clinical planning-plan ID;
- prescribed fractions;
- selected ipsilateral-lung, Heart, ILF and HIF structure IDs;
- gILF, gHIF, ILF and HIF percentages;
- lung V8Gy and V12Gy percentages;
- Heart Dmean in Gy;
- per-metric status and reason;
- row warnings and discovery failures.

### Tasks

- [ ] Implement dependency-aware calculation orchestration.
- [ ] Guarantee `Application.ClosePatient()` in `finally` after every successful open.
- [ ] Write and flush results after each patient for crash recovery.
- [ ] Write per-patient logs and a batch summary in the controlled directory.
- [ ] Return a summary exit code without treating expected row failures as a crash.
- [ ] Test dependency combinations and stable output ordering.

### Acceptance

A row-level failure cannot stop later patients, no ESAPI object is accessed after
patient closure, and every opened patient is closed exactly once.

## 10. Milestone 12H — Integration and hospital validation

**Status:** Not started

Run automated tests after each meaningful change and validate the completed
workflow inside the supported hospital-network ESAPI environment.

### Deliberate validation cases

1. one known successful patient;
2. missing Heart;
3. missing HIF;
4. multiple reviewed plans;
5. multiple clinical-plan isocentres;
6. historic `PHYS` naming;
7. missing clinical dose/DVH;
8. unsupported physics beam geometry;
9. Heart IDs requiring closest-string selection;
10. a Heart closest-string tie.

### Checks

- [ ] Existing Core tests remain green.
- [ ] New pure discovery, selection, configuration and output tests pass.
- [ ] Solution and standalone executable build as .NET Framework 4.6.2 x64.
- [ ] Fractions agree with Eclipse.
- [ ] Lung V8Gy/V12Gy and Heart Dmean agree with Eclipse reports.
- [ ] gILF/gHIF agree with the interactive script for the same physics plan.
- [ ] ILF/HIF agree with the source-structure volume ratios.
- [ ] Partial failures populate the intended status/reason columns.
- [ ] Every patient closes cleanly and processing continues.
- [ ] Source review confirms there is no `BeginModifications()` or ARIA write path.
- [ ] Identifiable CSV and logs remain in the approved hospital-network directory.

### Acceptance

A representative batch completes without prompts, preserves every requested row,
matches Eclipse for the initial DVH metrics, produces auditable diagnostics and
does not modify ARIA data.

## 11. Recommended implementation order

Implement and validate one milestone at a time:

1. 12A structured calculation boundary;
2. 12B selectors;
3. shared-library DVH evaluator decision/addition;
4. 12C standalone scaffold;
5. 12D input/configuration/output contracts;
6. 12E discovery;
7. 12F calculations;
8. 12G orchestration and export;
9. 12H hospital validation.

Do not begin the next milestone until the current milestone builds, its relevant
tests pass and the diff has been reviewed for accidental scope expansion.
