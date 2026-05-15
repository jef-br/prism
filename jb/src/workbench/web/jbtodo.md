# Web Workbench Todo

- [ ] Define API client behavior: say how the web app sends multipart data and handles zip, JSON, errors, and progress.
  - Impact:
    - Project progress: High - The API client is the bridge between browser workflow and the processing service.
    - Effect on other TODOs: Blocks - It depends on API request/response models and drives upload, progress, error, and output preview behavior.
  - Industry standard:
    Web clients for long-running media jobs separate submission, progress tracking, result download, and error rendering, using typed response handling for binary and JSON outcomes.
  - Recommended solution:
    Implement one API client layer that submits canonical multipart requests with a complete `PrismProcessingParameters` payload, tracks job progress, handles zip streams and JSON output, and maps structured API errors to UI states.
  - Answer:

- [ ] Define upload component behavior: say how drag-and-drop collects files and external URLs for `/PRISM/process`.
  - Impact:
    - Project progress: High - Upload behavior determines the browser-side input shape before API validation.
    - Effect on other TODOs: Unblocks - It feeds multipart fields, drag-and-drop errors, request size validation, and upload validation states.
  - Industry standard:
    Browser upload flows pre-classify selected files, show counts and limits, collect URLs separately, and leave authoritative validation to the server.
  - Recommended solution:
    Collect images, Excel files, zip files, URL text, and all job parameters into the canonical request model, with job parameters configured in one UI location and binary parameters grouped together.
  - Answer:

- [ ] Define progress visualization behavior: say which stages are visible and what data appears while a batch runs.
  - Impact:
    - Project progress: High - Progress visibility is essential for batches that may process thousands of images.
    - Effect on other TODOs: Unblocks - It consumes API progress streaming and shared workbench behavior.
  - Industry standard:
    Large-batch UIs show stable stage names, item counts, warnings, current item, elapsed time, and completion state based on backend events rather than local guesses.
  - Recommended solution:
    Render canonical stages from progress events and show counts, current item, severity, message, and optional diagnostic snapshot references.
  - Answer:

- [ ] Define section data shapes: list the data each section expects for uploader, Excel model, image collection, match results, and output preview.
  - Impact:
    - Project progress: High - Section data shapes prevent UI components from inventing incompatible interpretations of core results.
    - Effect on other TODOs: Influences - It maps to core models, manifest projection, progress events, and diagnostics.
  - Industry standard:
    Pipeline dashboards use typed view models derived from backend contracts, keeping raw pipeline data separate from presentation state.
  - Recommended solution:
    Define view models for upload state, job parameters, Excel summary, normalized images, matcher results, transform/output preview, and KO groups derived from API/core models.
  - Answer:

- [ ] Define drag-and-drop error states: say what users see for rejected files, invalid URLs, and oversized inputs.
  - Impact:
    - Project progress: Medium - Clear rejection states reduce support burden before users run batches.
    - Effect on other TODOs: Influences - It reflects API error payloads, URL validation, and request size validation.
  - Industry standard:
    Upload UIs distinguish local preflight errors from authoritative server rejections and show user-safe reasons with recoverable actions.
  - Recommended solution:
    Show per-input rejection rows with reason code/message, whether the item can be removed or corrected, and a clear separation between warnings and blocking errors.
  - Answer:

- [ ] Define upload validation states: say how the UI reports missing Excel files, missing images, and invalid option combinations.
  - Impact:
    - Project progress: Medium - Validation states improve the submission path but are constrained by the API request model.
    - Effect on other TODOs: Influences - It uses config response limits and API error payload conventions.
  - Industry standard:
    Batch intake forms validate required input classes and option compatibility before submission while still relying on backend validation for final acceptance.
  - Recommended solution:
    Disable submission until at least one image source and one Excel source are present, flag incompatible options inline, and mirror server errors if backend validation rejects the request.
  - Answer:

- [ ] Define Next.js project layout: choose where pages, sections, components, API client code, and CSS files live.
  - Impact:
    - Project progress: Low - Project layout matters for maintainability but follows UI contract decisions.
    - Effect on other TODOs: Independent - It does not change API contracts, pipeline behavior, or model shapes.
  - Industry standard:
    Next.js apps keep route files thin, isolate feature sections, centralize API clients, and put reusable UI primitives and styles in predictable folders.
  - Recommended solution:
    Use route-level files for pages, feature folders for pipeline sections, a shared API client module, and dedicated style/token files.
  - Answer:

- [ ] Define CSS variable file: name the file that contains colors, fonts, spacing values, and other design tokens.
  - Impact:
    - Project progress: Low - Design tokens help consistency but do not unblock pipeline functionality.
    - Effect on other TODOs: Independent - It mainly affects web styling implementation.
  - Industry standard:
    Frontend systems store design tokens in one importable stylesheet or token module so themes and components do not duplicate values.
  - Recommended solution:
    Put web workbench tokens in a single CSS variable file imported once at the app root.
  - Answer:

- [ ] Define CSS class file: name the file that contains reusable classes for the web workbench.
  - Impact:
    - Project progress: Low - Reusable classes improve UI consistency after data and behavior contracts are stable.
    - Effect on other TODOs: Independent - It does not affect API, core, or pipeline decisions.
  - Industry standard:
    Shared CSS utilities are kept separate from component-specific styles to avoid accidental coupling and visual drift.
  - Recommended solution:
    Keep reusable layout and state classes in one workbench CSS module or global utility stylesheet, with component-specific styles near components.
  - Answer:
