import type {
  PrismApiErrorPayload,
  PrismConfigResponse,
  PrismHealthResponse,
  PrismJobStartEnvelope
} from "../services/prismApiClient";

interface StatusPanelProps {
  isApiLoading: boolean;
  isJobLoading: boolean;
  hasAnyInput: boolean;
  apiErrorMessage?: string;
  apiErrorPayload?: PrismApiErrorPayload;
  health?: PrismHealthResponse;
  config?: PrismConfigResponse;
  job?: PrismJobStartEnvelope;
  progressEventCount: number;
  hasResult: boolean;
}

export function StatusPanel({
  isApiLoading,
  isJobLoading,
  hasAnyInput,
  apiErrorMessage,
  apiErrorPayload,
  health,
  config,
  job,
  progressEventCount,
  hasResult
}: StatusPanelProps) {
  const loadingText = getLoadingText(isApiLoading, isJobLoading);

  return (
    <section className="state-panel" aria-label="Workbench visible states">
      <div className={hasAnyInput ? "state-chip" : "state-chip state-chip-active"}>
        <strong>Empty input</strong>
        <span>{hasAnyInput ? "Sources selected" : "Waiting for files or URLs"}</span>
      </div>

      <div className={loadingText ? "state-chip state-chip-active" : "state-chip"}>
        <strong>Loading</strong>
        <span>{loadingText ?? "Idle"}</span>
      </div>

      <div className={apiErrorMessage ? "state-chip state-chip-error" : "state-chip"}>
        <strong>API error</strong>
        <span>{apiErrorMessage ?? "None"}</span>
      </div>

      <div className={progressEventCount === 0 ? "state-chip state-chip-active" : "state-chip"}>
        <strong>Progress placeholder</strong>
        <span>
          {progressEventCount === 0
            ? "No SSE events yet"
            : `${progressEventCount} progress event(s)`}
        </span>
      </div>

      <div className={hasResult ? "state-chip" : "state-chip state-chip-active"}>
        <strong>Result placeholder</strong>
        <span>{hasResult ? "Result loaded" : "Waiting for resultUrl"}</span>
      </div>

      <div className="api-summary">
        <div>
          <strong>Health</strong>
          <span>{health ? "Loaded from /PRISM/health" : "Not loaded"}</span>
        </div>
        <div>
          <strong>Config</strong>
          <span>{config ? "Loaded from /PRISM/config" : "Not loaded"}</span>
        </div>
        <div>
          <strong>Job</strong>
          <span>{getJobLabel(job)}</span>
        </div>
      </div>

      {apiErrorPayload ? (
        <div className="error-detail" role="alert">
          <strong>{apiErrorPayload.code ?? "API_ERROR"}</strong>
          {apiErrorPayload.correlationId ? (
            <span>Correlation ID: {apiErrorPayload.correlationId}</span>
          ) : null}
          {apiErrorPayload.fieldErrors && apiErrorPayload.fieldErrors.length > 0 ? (
            <ul>
              {apiErrorPayload.fieldErrors.map((fieldError) => (
                <li key={fieldError}>{fieldError}</li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}

function getLoadingText(isApiLoading: boolean, isJobLoading: boolean): string | undefined {
  if (isJobLoading) {
    return "Submitting or retrieving a PRISM job";
  }

  if (isApiLoading) {
    return "Checking health and config";
  }

  return undefined;
}

function getJobLabel(job?: PrismJobStartEnvelope): string {
  if (!job) {
    return "No job started";
  }

  return job.JobID ?? job.JobId ?? job.jobID ?? job.jobId ?? "Job acknowledged";
}
