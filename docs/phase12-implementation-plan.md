# Phase 12 standalone batch audit implementation plan

**Status:** In progress; milestones 12A-12G implemented, 12H validation pending
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

**Status:** Complete

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
      output; connect it to the patient loop in milestone 12G.
- [x] Confirm startup and clean exit in the supported standalone ESAPI environment.

### Acceptance

The executable starts in the documented ESAPI standalone environment, loads its
configuration and exits cleanly without opening or modifying a patient.

Implementation adds `BreastSurrogate.Batch` as an old-style .NET Framework
4.6.2 x64 executable with an STA entry point, exactly one process-level ESAPI
`Application`, deterministic disposal, input-path validation and explicit exit
codes. An explicit `--check-esapi` mode, also offered as `T` after an
interactive no-argument launch, verifies application creation and disposal
without requiring placeholder input files. It deliberately does not open a
patient. The supplied
`docs/ConsoleUtility.cs` informed a tested ASCII progress reporter that updates
one console line interactively and emits durable lines when output is
redirected; milestone 12G now uses it for patient-by-patient reporting.
The hospital-network check on 11 August 2026 confirmed successful ESAPI
application creation and disposal, completing milestone 12C.

The standalone deployment follows the working `docs/Main.cs` and
AutoRegression pattern: copy the public Model.API and Model.Types reference
assemblies beside the executable, use a simple .NET Framework startup config
without application-level ESAPI binding redirects, and rely on the installed
matching ESAPI runtime for private Interface assemblies. Never deploy private
Varian Interface DLLs or combine public assemblies from different releases.

## 6. Milestone 12D — Configuration and table I/O

**Status:** Complete

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

- [x] Define versioned configuration and input-row contracts.
- [x] Parse and validate JSON before creating the ESAPI application where practical.
- [x] Parse quoted CSV fields, permit repeated patient IDs with different
      treatment overrides, and report malformed or exact duplicate rows clearly.
- [x] Define stable output columns and invariant-culture numeric formatting.
- [x] Represent unavailable values as blank values plus explicit status/reason.
- [x] Default output to the log directory, allowing a similar approved directory.
- [x] Test quoting, commas, blank overrides, invalid JSON and invalid metric requests.
- [x] Permit interactive entry of file paths or directories after a no-argument launch.
- [x] Confirm the interactive prompts and validation summary in the hospital environment.

### Acceptance

Input and configuration errors are reported before patient processing, and CSV
round trips preserve identifiers and diagnostic text safely.

Implementation uses only .NET Framework libraries. Version-1 JSON is validated
before ESAPI startup and records an exact SHA-256 configuration hash. The CSV
reader handles quoted commas, escaped quotes and embedded line breaks; exact
duplicate rows are rejected while repeated patient IDs with different
overrides are retained. The output contract has stable provenance and
per-metric value/unit/status/reason columns, uses invariant numbers, and leaves
unavailable values blank. Output defaults to the configured log directory.

A no-argument launch now prompts for the patient CSV and JSON config. Each
entry may be a full filename or a directory containing `patients.csv` or
`config.json`; command-line arguments accept the same directory shorthand.
Copy-ready inputs are in `docs/batch-example`. Automated validation currently
passes 57 Core, 19 ESAPI-contract and 44 Batch tests. The interactive input and
ESAPI startup path was confirmed from within the hospital Citrix environment on
11 August 2026. Patient opening and durable result-row production are now
implemented by milestone 12G.

## 7. Milestone 12E — Deterministic plan discovery

**Status:** Complete

Resolve the clinical and physics branches independently after opening a patient.

### Clinical planning branch

- course ID contains `PLANNING`, case-insensitively;
- candidate course contains at least one rejected external plan;
- candidate course contains exactly one reviewed external plan;
- if no reviewed plan exists, a unique non-rejected plan may be selected when
  its ID exactly matches an x-prefixed rejected plan after removing only the
  leading x;
- an exact planning-course override may disambiguate courses;
- multiple reviewed plans are never resolved heuristically;
- reviewed plans with no treatment isocentre or multiple distinct treatment
  isocentres are unsupported for clinical DVH extraction.

### Physics branch

- course and plan IDs contain a complete `PPHYS` or `PHYS` token;
- only when exact tokens are absent, permit a unique delimited token one edit
  from `PPHYS` or `PHYS` (for example `PPHY`), with unique PlanningApproved
  status allowed to disambiguate similar-token plans;
- exact physics-course and physics-plan overrides may disambiguate;
- missing or ambiguous selection makes physics metrics unavailable.

Use small immutable discovery snapshots and pure decision functions where
possible, then map the selected IDs back to ESAPI objects. Define and test the
tolerance used when deciding whether treatment-beam isocentres are distinct.

### Tasks

- [x] Implement case-insensitive planning-course discovery and exact overrides.
- [x] Implement reviewed/rejected approval-status checks.
- [x] Implement complete-token PPHYS/PHYS matching and exact overrides.
- [x] Implement single-distinct-isocentre validation for the reviewed plan.
- [x] Record every candidate and the final discovery reason.
- [x] Test missing, unique, ambiguous and overridden cases.

### Acceptance

No plan is chosen by collection order, date, display state or unsupported fuzzy
matching. A failure in one branch does not prevent the other branch from running.

Implementation snapshots only course IDs, external-plan IDs, approval states and
non-setup treatment-beam isocentres from ESAPI before applying pure discovery
rules. Exact CSV overrides are case-sensitive. Automatic `PLANNING`, `PHYS` and
`PPHYS` matching is case-insensitive; physics tokens must be complete configured
tokens. The clinical and physics branches produce independent status, method,
reason and candidate diagnostics.

Following the first five-patient hospital audit on 12 August 2026, discovery
also has two deliberately narrow compatibility fallbacks. A planning course
without a reviewed plan can map rejected `xL BRST` to the unique non-rejected
exact ID `L BRST`. If no exact PHYS/PPHYS token exists, a delimited token exactly
one edit away can match the observed `PPHY` form; a unique PlanningApproved
similar-token plan can resolve multiple such plan candidates. Primary reviewed
and exact-token matches always win, and fallback ambiguity remains unavailable.

Reviewed-plan treatment-beam isocentres are treated as one isocentre when every
pair lies within `0.01 mm`; the deterministic reference is their coordinate-wise
mean. No treatment-beam isocentre or a separation above that tolerance is
unsupported, while retaining the resolved course and plan IDs for diagnosis.
The output schema reserves discovery and clinical-isocentre provenance columns.
Pure discovery behavior is covered by Batch tests. Milestone 12G now opens each
patient sequentially, maps the selected IDs back to ESAPI plans and writes the
populated result row.

## 8. Milestone 12F — Legacy and clinical metrics

**Status:** Complete; live value agreement remains in 12H

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

- [x] Implement the generic DVH evaluator in the shared library or locally
      according to the decision in section 2.
- [x] Implement legacy volume-ratio evaluation with denominator validation.
- [x] Implement the three initial configured DVH metrics.
- [x] Keep physics-plan and reviewed-plan structures strictly separate.
- [x] Return a status/reason for null dose, null DVH and invalid values.
- [x] Test metric request dispatch and unit normalization independently of ESAPI
      where possible.

### Acceptance

Every requested value is either a finite value with an explicit unit or an
unavailable outcome with a specific reason; unavailable values never become zero.

The generic evaluator is implemented locally in `BreastSurrogate.Esapi` behind
an ESAPI-independent `IDvhDataSource` boundary so it can later move to
`Uclh.XRT.Library` without taking BreastSurrogate naming or output conventions
with it. Its thin ESAPI adapter uses the documented `PlanningItem` cumulative
DVH, volume-at-dose and dose-at-volume methods. It supports `Dmean`, `VGy(%)`,
`VGy(cc)`, `Dcc(Gy)` and `D%(Gy)`, normalizes Gy/cGy results to Gy and records
the native dose unit.

`PhysicsPlanMetricService` constructs `EsapiContext(patient, physicsPlan)` and
returns the existing structured gILF/gHIF result together with independent
legacy ILF/HIF volume ratios. Legacy numerator, lung and Heart structures are
selected only from that physics plan. `ReviewedPlanMetricService` independently
selects lung and Heart only from the supplied reviewed plan's structure set,
records `PlanSetup.NumberOfFractions`, and evaluates the mapped JSON requests.
Missing dose, null DVH, missing or ambiguous structures, unsupported dose units,
query exceptions and invalid values become metric-level unavailable results.

Automated validation passes 57 Core, 29 ESAPI-contract and 45 Batch tests. The
new tests cover all five request forms, dispatch, cGy-to-Gy conversion, native
units, null DVH, missing dose, query exceptions, invalid values, legacy ratios
and JSON-to-evaluator request mapping. Milestone 12G now maps discovered live
plans into these services and exports their results; comparison with Eclipse
DVH values remains milestone 12H.

## 9. Milestone 12G — Patient loop, fault isolation and export

**Status:** Implemented; hospital integration validation remains in 12H

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

- [x] Implement dependency-aware calculation orchestration.
- [x] Guarantee `Application.ClosePatient()` in `finally` after every successful open.
- [x] Write and flush results after each patient for crash recovery.
- [x] Write per-patient logs and a batch summary in the controlled directory.
- [x] Return a summary exit code without treating expected row failures as a crash.
- [x] Test dependency combinations and stable output ordering.

### Acceptance

A row-level failure cannot stop later patients, no ESAPI object is accessed after
patient closure, and every opened patient is closed exactly once.

The executable now creates a timestamped result CSV and batch log before opening
the first patient, then processes the validated input rows sequentially on the
STA thread. `EsapiPatientAuditSession` is the only live-patient boundary. The
runner attempts to dispose each successfully opened session exactly once in a
`finally` block, which calls the documented `Application.ClosePatient()` before
any copied result is logged or written. Exact discovered course/plan IDs are
resolved within the still-open patient; physics and clinical branches invoke
the Phase 12F services independently.

Every row is written and flushed immediately. Missing patients, discovery
failures, unsupported geometry, missing structures or dose, individual metric
exceptions, and unexpected patient-level exceptions produce explicit values,
statuses and reasons without stopping later rows. Expected row failures still
return a successful process exit; fatal application or output initialization/
write failures remain process failures. The output now distinguishes clinical
and physics lung/Heart structure IDs and retains all requested/resolved IDs,
isocentre provenance, fractions, metrics, configuration hash and application
version.

Each row receives an indexed, filename-safe per-patient log in the configured
log directory, while the timestamped batch log contains all patient sections
and a final total/fully-available/unavailable summary. Console progress works
both interactively and when redirected. Automated validation passes 57 Core,
29 ESAPI-contract and 51 Batch tests. Tests cover independent branches, partial
metric availability, a failed first patient followed by continued processing,
missing-patient rows, stable CSV output and exactly one disposal attempt for
each opened session. Live lifecycle and value checks remain milestone 12H.

## 10. Milestone 12H — Integration and hospital validation

**Status:** In progress; initial five-patient run completed, discovery and jaw-only fallbacks pending retest

Run automated tests after each meaningful change and validate the completed
workflow inside the supported hospital-network ESAPI environment.

An initial five-patient run on 12 August 2026 completed the full workflow for
three patients. One remaining physics branch used the observed `PPHY` plan ID,
and one planning course had no reviewed plan but contained rejected `xL BRST`
and intended plan `L BRST`. The narrow Phase 12E compatibility fallbacks now
cover both cases and require a repeat hospital run before those validation
points are closed.

One PPHYS plan also used no MLC and reported `MLCPlanType.NotDefined`. This is
now treated as an explicit jaw-only static aperture: the ordinary beam, couch,
angle and jaw checks still apply, while MLC hardware and leaf checks are skipped.
Static Millennium fields are unchanged, and all dynamic MLC plan types remain
unsupported. This case also requires repeat hospital validation.

Automated validation currently passes 57 Core, 32 ESAPI-contract and 58 Batch
tests (147 total).

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
