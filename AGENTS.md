# BreastSurrogate Codex Instructions

## Purpose

BreastSurrogate is a read-only Varian ESAPI tool for calculating geometric
ipsilateral-lung-in-field (gILF) and heart-in-field (gHIF) surrogates from
treatment beam geometry without dose calculation or ARIA database modification.

## Repository architecture

- `src/BreastSurrogate.Core`
    - Computational geometry and surrogate calculation.
    - May reference VMS.TPS.Common.Model.Types and use value types such as VVector and VRect.
    - Must not reference VMS.TPS.Common.Model.API.
    - Must not access Patient, PlanSetup, Beam, ControlPoint, Structure, Image or other persistent ESAPI API objects.
    - Must remain independently unit-testable without Eclipse/ARIA.

- `src/BreastSurrogate.Esapi`
  - ESAPI entry point and adapters.
  - Converts ESAPI objects to Core models.
  - May reference the UCLH generic library.

- `tests/BreastSurrogate.Core.Tests`
  - Unit tests for all computational geometry.

- `docs`
  - Design, development and validation documentation.

## Safety constraints

- The production script is read-only.
- Do not call `Patient.BeginModifications()`.
- Do not create or modify plans, structures, beams or dose.
- Do not add write-enabled ESAPI functionality.
- Do not silently broaden supported beam geometry.
- Reject unsupported cases explicitly.

## Initial supported scope

- Static photon tangent beams.
- Head-first supine.
- Couch angle zero.
- One static aperture per beam.
- Supported MLC models must be explicitly configured.

Do not implement VMAT, dynamic IMRT, couch rotation or Halcyon unless
specifically requested.

## Implementation rules

- Keep ESAPI objects out of BreastSurrogate.Core.
- Prefer small, deterministic, pure functions in Core.
- Geometry conventions must be explicit and documented.
- Do not invent ESAPI members. Use only APIs visible in the referenced
  assemblies/XML documentation or existing source.
- If an ESAPI behaviour is uncertain, identify it rather than guessing.

## Testing

Every Core geometry change must include unit tests.

Test at minimum:
- central-axis projection
- beam divergence
- coordinate signs
- jaw boundaries
- MLC leaf selection
- aperture union
- synthetic structure overlap

Run the Core test suite after each meaningful change.

## Working approach

For substantial work:
1. Inspect the relevant code and documentation.
2. Propose the implementation plan.
3. Implement only the agreed milestone.
4. Add/update tests.
5. Run tests.
6. Review the diff for unnecessary changes.
7. Summarize what changed and any unresolved assumptions.