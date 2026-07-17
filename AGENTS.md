# AGENTS.md — agent protocols and style addenda

Session-start reading order, commands, architecture, domain vocabulary, config layout, and the core C# style rules live in `CLAUDE.md` — that file wins on any overlap. This file holds only what CLAUDE.md does not: file-placement rules, style addenda, the todo lifecycle, and protected files.

## File and folder placement

- Never assume or invent folder structure. Follow the existing tree (`Services/`, `lib/`, `Models/`, per-folder config JSON) and the naming patterns already present in it.
- A new file goes where its ticket, spec, or an existing sibling pattern puts it. If none of those determines the location, ask before creating it.
- Whenever you create a file or folder, state in your report **what** was created, **where**, and **why that location** — the user reviews placement decisions after the fact and must be able to reconstruct the reasoning.

## Style addenda (on top of CLAUDE.md's code style)

- Target .NET 8+; modern C# — records for immutable config, collection expressions, pattern matching.
- Explicit, descriptive names for business concepts, image-processing steps, model inputs/outputs, tensor shapes, thresholds, and file paths.
- Avoid clever one-liners and dense LINQ chains unless the surrounding code already uses that pattern.
- Scoring logic must be readable by a 10-year-old.
- Comments follow CLAUDE.md's rule: class-level `/// <summary>` only; minimal inline comments, reserved for what code cannot express (why a workaround exists, ONNX quirks, empirical threshold values, preprocessing requirements).

### ONNX / ML
- Wrap session creation, tensor prep, inference, and output parsing in helper methods named for what they actually do — `Convert<X>To<Y>For<Z>` (e.g. `ConvertImagesToBase64ForImageLabeling`), `ParseTensorToBoundingBox` — never vague names like `PrepareInputTensor` or `RunInference`.
- Name every tensor input/output with a string constant or config value — no scattered raw `"input"`/`"output"` strings.
- State expected input shape, dtype, and normalization mean/std in a comment above tensor construction.
- One named method per preprocessing step (resize, normalize, channel order, HWC→CHW) — no single dense expression.
- Gate diagnostic logging of tensor shapes and raw scores behind a DEBUG flag.

### OpenCV / image processing
- Every `Mat` named for its state (`originalImage`, `resizedForModel`, `normalizedFloat`); release intermediates promptly with `using`/`.Dispose()`; document why any Mat must outlive its scope.
- Wrap raw OpenCV calls in named helper methods so the pipeline reads as a sequence of named steps.
- State the color space (BGR/RGB/grayscale) at every boundary where an image crosses (disk load, model input, output save) — OpenCV loads BGR by default.
- Interpolation flags, border modes, channel counts: constants or config, never inline literals.
- Straightforward defensive checks at boundaries: `Mat` not empty, paths exist, Excel rows non-null, tensor outputs have the expected shape.

### Library / API surface
- Small, stable public surface: one or two entry-point classes with clear `Process(...)` methods; implementation details `internal`.
- Accept file paths and streams as inputs — don't force callers to pre-parse Excel or pre-extract zips.
- Return rich result objects (`ProcessingResult`, `ClassifiedImage`) with both success data and failure details — never raw strings or `bool`.
- Keep vendored or third-party wrapper files isolated; do not rewrite their style unless explicitly asked.

## How to handle Todos
  - `jbtodo.md` files are temporary working notes for unresolved or not-yet-integrated decisions.
  - Accepted project knowledge belongs in `jb\docs`, with `jb\docs\PRISM-index.md` used as the documentation map.
  - **Todos follow this pattern**:

```
  - [ ] <TodoTitle>: <Brief explanation of what needs to be done> (example: state which top-level folder owns API notes, core pipeline notes, workbench notes, shared docs, and test fixtures.)
  - Impact:
    - <Level of Impact on the project (Low|High)> - <One sentence explaining why this is so>
    - Effect on other TODOs: <List all consequences>
  - Industry standard:
    <Describe the industry standard / best practice solution for this todo in one or two sentences. Avoid terminology and jargon as much as possible in favor of using plain english.>
  - Recommended solution:
    <Describe your recommended solution. Avoid scope creep. Do not add new functionality unless explicitly requested or indispensible for working code. Assume my input towards you is not exhaustive. For example, if I were to tell you "we need multi-language support for 5 languages (De, Es,En,Fr,It)"... Implement a solution without external dependencies that uses industry standard practices for multi-language support. Do not implement 4 or 6 languages, or try to "keep it simple for now".>
  - Answer: <followed by nothing. I will manually write an answer here for you to read in my next prompt, or I will ask you to go over every todo with me.>
  ```
  - **Finished todos / closing a todo**:
    - A satisfactory answer is complete + valid + feasible + does not contradict previous knowledge.
    - If the answer is not satisfactory:
         1. Do not close the todo.
         2. If inconsistency or contradiction is suspected, make a second in-depth check by looking closely at impacted files and related todos to verify.
         3. If inconsistency or contradiction is confirmed, rephrase the todo:
              - Incorporate the answers valid parts
              - use information from the in-depth second check to refine the new todo
              - The new todo should pertain to addressing the inconsistency/contradiction
              - Update the impact, industry standard, and recommended solution sections accordingly
              - Set the answer to empty.
    - If the answer is satisfactory:
      - Move the accepted decision to the most appropriate `.md` file inside `jb\docs`.
      - Update `jb\docs\PRISM-index.md` if the decision changes the task-to-file map or adds a new documentation surface.
      - Remove the entire todo from the `jbtodo.md` once the answer is fully integrated in the documentation.
      - Do not keep a header-only `jbtodo.md` after closing the last todo unless the user specifically asks for that in the current task.
      - If the `jbtodo.md` file has no remaining open todos after close-out, delete that `jbtodo.md` file.

  - **Frozen todos**:
    - A todo is frozen only when its answer section contains **FROZEN TODO** or **FROZEN**.
    - A frozen todo stays open. Treat the marker as "not ready to answer", not as an answer.
    - The marker means the user cannot resolve the topic right now because the subject is too difficult, the needed information is missing, or the project currently has too much noise around that decision.
    - Do not make thawing a frozen todo the main goal.
    - If a formerly frozen todo later receives an answer, probe it more deeply than a normal answer before accepting it:
      - check impacted files and related todos;
      - ask targeted follow-up questions when consequences are unclear;
      - only accept the answer when you can explain its consequences back to the user for confirmation or refinement.
    - Frozen todos all thaw at the same time only when every remaining open todo is frozen. When that happens, clearly notify the user and remove the frozen markers.

## Protected files — never delete from git

The following files must remain tracked in git at all times. Never run `git rm` on them.
If you want to stop tracking a file without deleting it from disk, use `git rm --cached <file>` instead.

- `CLAUDE.md` — Claude Code session startup instructions
- `AGENTS.md` — this file; placement rules, style addenda, and agent protocols
- `AGENT-TICKETS.md` — ticket registry (open tickets)
- `AGENT-TICKETS-ARCHIVE.md` — Done tickets, moved there by /ticket-finish
- `AGENTFEEDBACK.md` — accumulated session feedback

A pre-commit hook at `.git/hooks/pre-commit` enforces this by blocking staged deletions of these files.
