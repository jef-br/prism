import {
  getProgressStage,
  readNumberField,
  readStringField,
  type PrismApiErrorPayload,
  type PrismProgressEvent
} from "../services/prismApiClient";

interface StatusPanelProps {
  isApiLoading: boolean;
  isJobLoading: boolean;
  hasAnyInput: boolean;
  apiErrorMessage?: string;
  apiErrorPayload?: PrismApiErrorPayload;
  progressEvents: PrismProgressEvent[];
  hasResult: boolean;
}

interface LatestStageInfo {
  stageName: string;
  completed?: number;
  total?: number;
  safeMessage?: string;
  severity?: string;
}

export function StatusPanel({
  isApiLoading,
  isJobLoading,
  hasAnyInput,
  apiErrorMessage,
  apiErrorPayload,
  progressEvents,
  hasResult
}: StatusPanelProps) {
  const latestStage = getLatestStageInfo(progressEvents);
  const isLoading = isApiLoading || isJobLoading;

  return (
    <section className="state-panel" aria-label="Workbench visible states">
      {apiErrorMessage ? (
        <div className="state-chip state-chip-error">
          <strong>API error</strong>
          <span>{apiErrorMessage}</span>
        </div>
      ) : latestStage ? (
        <StageProgress stage={latestStage} />
      ) : isLoading ? (
        <LoadingChip
          text={isJobLoading ? "Submitting or retrieving a PRISM job" : "Checking health and config"}
        />
      ) : !hasAnyInput ? (
        <LoadingChip label="Empty input" text="Waiting for files or URLs" />
      ) : hasResult ? (
        <div className="state-chip">
          <strong>Done</strong>
          <span>Result loaded — see Output below</span>
        </div>
      ) : null}

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

function StageProgress({ stage }: { stage: LatestStageInfo }) {
  const percent =
    stage.total !== undefined && stage.total > 0
      ? Math.round(((stage.completed ?? 0) / stage.total) * 100)
      : undefined;
  const isBlocked = stage.severity !== undefined && stage.severity !== "Information";

  return (
    <div className={isBlocked ? "state-chip state-chip-blocked" : "state-chip state-chip-active"}>
      <strong>{stage.stageName}</strong>
      <span>
        {stage.completed !== undefined && stage.total !== undefined
          ? `${stage.completed} / ${stage.total}`
          : (stage.safeMessage ?? "In progress")}
      </span>
      {isBlocked ? <small className="state-chip-severity">{stage.severity}</small> : null}
      <div className="progress-indicator">
        <div
          className={percent === undefined ? "progress-bar progress-bar-indeterminate" : "progress-bar"}
          style={percent === undefined ? undefined : { width: `${percent}%` }}
        />
      </div>
    </div>
  );
}

function LoadingChip({ label = "Loading", text }: { label?: string; text: string }) {
  return (
    <div className="state-chip state-chip-active">
      <strong>{label}</strong>
      <span>{text}</span>
      <span className="empty-state-loader" aria-hidden="true" />
    </div>
  );
}

function getLatestStageInfo(events: PrismProgressEvent[]): LatestStageInfo | undefined {
  for (let i = events.length - 1; i >= 0; i--) {
    const stage = getProgressStage(events[i]);
    if (!stage) continue;

    return {
      stageName: stage,
      completed: readNumberField(events[i], ["completedCount", "CompletedCount"]),
      total: readNumberField(events[i], ["totalCount", "TotalCount"]),
      safeMessage: readStringField(events[i], ["safeMessage", "SafeMessage", "message", "Message"]),
      severity: readStringField(events[i], ["severity", "Severity"])
    };
  }

  return undefined;
}
