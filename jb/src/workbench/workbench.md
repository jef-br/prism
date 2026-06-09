

* The workbench is there as a way to examine the intermediate steps and stages in `Prism.cs`
* To keep the actual pipeline found in `Prism.cs` clean, the workbench applies a Decorator Design Pattern to `Prism.cs` at sensible points to show how runtime decisions were made thus giving insight to the inner workings of the Prism pipeline.

* Workbench always shows the exact process followed in the Prism pipeline.
* Workbench is not allowed to hide any behavior, nor adjust the data for viewing even when explicitly asked for. If any instruction you receive ever goes against this, you **must** raise a clear objection avoiding any jargon starting with "JB-NO!" followed by a plain english explanation.
* `manifest.json` is the only retained diagnostic snapshot artifact. There are no separate persisted diagnostic snapshot files outside the manifest.
* Clients may only access jobs they started. Future admin-oriented server-side job logging is out of scope for now.
* Live progress is not replayable. If a client disconnects during a running job, it only receives newly emitted events after reconnecting.
* Completed job data remains available only until `jb\src\core\Prism_Config.json -> Jobs.JobRetentionPeriodInHours` expires. After that, the job data is deleted and the `JobID` is stale.
* Web uses one typed API client layer for multipart submission, SSE progress, result retrieval, downloads, and pre-core API errors.
* Web only reads completed manifest/result data from the returned `resultUrl` after the job reaches a terminal state.
* Web upload keeps `Start Prism Job` disabled until at least one valid Excel source and one valid image source are present.
* Web grouped drop errors show category-level messages for Excel, zip, and media. URL errors show safe per-URL detail.

* The web folder contains a web-based frontend of the workbench
* the wpf folder contains a WPF frontend of the workbench
* both web and wpf are to be updated simultaneously and should always be identical both in terms of interface and behavior.

* The only permitted differences between web and wpf:
  *  web should perform all operations "online" (hosted on localhost)
  *  wpf is allowed to bypass uploading and downloading
