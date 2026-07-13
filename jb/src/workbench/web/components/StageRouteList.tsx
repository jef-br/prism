import {
  getProgressStage,
  PRISM_ROUTE_STAGES,
  readNumberField,
  readStringField,
  type PrismProgressEvent,
  type PrismRouteStage
} from "../services/prismApiClient";

interface StageRouteListProps {
  events: PrismProgressEvent[];
}

export function StageRouteList({ events }: StageRouteListProps) {
  const latestEventByStage = new Map<PrismRouteStage, PrismProgressEvent>();

  for (const event of events) {
    const stage = getProgressStage(event);

    if (stage) {
      latestEventByStage.set(stage, event);
    }
  }

  return (
    <table className="route-table" aria-label="PRISM pipeline stages">
      <thead>
        <tr>
          <th>Stage</th>
          <th>Progress</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        {PRISM_ROUTE_STAGES.map((stage) => {
          const event = latestEventByStage.get(stage);
          const completedCount = event
            ? readNumberField(event, ["completedCount", "CompletedCount"])
            : undefined;
          const totalCount = event ? readNumberField(event, ["totalCount", "TotalCount"]) : undefined;
          const severity = event ? readStringField(event, ["severity", "Severity"]) : undefined;
          const safeMessage = event
            ? readStringField(event, ["safeMessage", "SafeMessage", "message", "Message"])
            : undefined;
          const percent =
            completedCount !== undefined && totalCount !== undefined && totalCount > 0
              ? Math.round((completedCount / totalCount) * 100)
              : undefined;

          return (
            <tr key={stage} className={event ? "route-row-live" : "route-row-pending"}>
              <td className="route-stage-name">{stage}</td>
              <td>
                {event ? (
                  <div className="progress-cell">
                    {percent !== undefined ? (
                      <div className="progress-bg" style={{ width: `${percent}%` }} />
                    ) : null}
                    <span className="progress-text">
                      {completedCount !== undefined && totalCount !== undefined
                        ? `${completedCount} / ${totalCount}`
                        : (safeMessage ?? "received")}
                    </span>
                  </div>
                ) : (
                  <span className="placeholder-text">Waiting for progress data.</span>
                )}
              </td>
              <td className="route-status">
                {event ? <small>{severity ?? "running"}</small> : <small className="pending">Waiting</small>}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
