Refine the web workbench layout:
- Should be a bit less "beige"
- Format doesn't allow a compact and complete review mechanism for matching and transforming
- Too much scrolling needed to see relevant information causing drowned download zip link
- Upscaling currently not explicitly mentioned is a good thing.
- No real feedback during the import & export stage. Hard to know whether a job is blocked or not.





-----


Getting this error in the http server when doing a test on any folder:
```cmd
2026-06-25 21:20:44.5942636 [W:onnxruntime:, session_state.cc:1327 onnxruntime::VerifyEachNodeIsAssignedToAnEp] Some nodes were not assigned to the preferred execution providers which may or may not have an negative impact on performance. e.g. ORT explicitly assigns shape related ops to CPU to improve perf.
2026-06-25 21:20:44.6063421 [W:onnxruntime:, session_state.cc:1329 onnxruntime::VerifyEachNodeIsAssignedToAnEp] Rerunning with verbose output on a non-minimal build will show node assignments.
```