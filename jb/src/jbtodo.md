# jb/src Todo

- [ ] Define source tree ownership rules: state which top-level folder owns API notes, core pipeline notes, workbench notes, shared docs, and test fixtures.
  - Impact:
    - Project progress: High - Clear ownership prevents duplicated or conflicting pipeline decisions across core, API, and workbench documentation.
    - Effect on other TODOs: Unblocks - It sets the filing rules for README synchronization, fixture placement, and subsystem-specific TODO answers.
  - Industry standard:
    Large image-processing systems usually separate product-level documentation, runtime contracts, test fixtures, and subsystem implementation notes so teams can scale without turning docs into a single inconsistent backlog.
  - Recommended solution:
    Assign `jb/src/core` to pipeline behavior and contracts, `jb/src/api` to HTTP contracts, `jb/src/workbench` to UI behavior, root `jb/src` docs to cross-cutting decisions, and a dedicated test fixture tree to sample inputs and expected outputs.
  - Answer:

- [ ] Define test fixture folder structure: choose where sample inputs, expected manifests, and expected output images will live once tests are added.
  - Impact:
    - Project progress: High - Fixtures are required before large-batch regression tests can prove import, matching, transform, and export behavior.
    - Effect on other TODOs: Unblocks - It supports Excel parsing, IO normalization, matcher evidence, manifest projection, and workbench diagnostics.
  - Industry standard:
    Data aggregators keep test fixtures versioned, deterministic, and separated by scenario, with raw inputs, expected normalized intermediates, and expected outputs stored where automated tests can consume them.
  - Recommended solution:
    Create scenario-based fixtures under a test-owned folder with `input`, `expected-manifest`, and `expected-output` subfolders, keeping production sample data out unless it is sanitized.
  - Answer:

- [ ] Define shared documentation location: say which decisions stay in README-like docs and which decisions move into code comments, config files, or model files.
  - Impact:
    - Project progress: Medium - It keeps design decisions findable, but most implementation can proceed once ownership and contracts are clear.
    - Effect on other TODOs: Influences - It affects how API, config, model, and pipeline answers are recorded after decisions are made.
  - Industry standard:
    Large processing platforms keep durable architecture decisions in docs, executable policy in configuration, and only local implementation rationale in comments near code that enforces it.
  - Recommended solution:
    Put cross-cutting decisions in README-style docs, put tunable limits and thresholds in config files, put public data shapes in model files, and reserve code comments for non-obvious implementation constraints.
  - Answer:

- [ ] Define README synchronization rules: say how `README.md` and `src information.md` stay consistent when product behavior changes.
  - Impact:
    - Project progress: Low - Synchronization improves maintainability but does not directly unblock pipeline contracts or data processing behavior.
    - Effect on other TODOs: Influences - It keeps future answers consistent once core, API, and workbench behavior changes.
  - Industry standard:
    Mature pipelines avoid duplicating authoritative behavior descriptions across many documents; they use one source of truth and require release or PR checks when public behavior changes.
  - Recommended solution:
    Treat `README.md` as the public overview, keep detailed source notes in `src information.md`, and require both to be reviewed only when a change affects user-visible behavior.
  - Answer:
