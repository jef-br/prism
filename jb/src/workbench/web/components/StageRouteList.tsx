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
    <ol className="route-list" aria-label="PRISM pipeline stages">
      {PRISM_ROUTE_STAGES.map((stage) => {
        const event = latestEventByStage.get(stage);
        const completedCount = event
          ? readNumberField(event, ["completedCount", "CompletedCount"])
          : undefined;
        const totalCount = event ? readNumberField(event, ["totalCount", "TotalCount"]) : undefined;
        const currentItem = event ? readStringField(event, ["currentItem", "CurrentItem"]) : undefined;
        const severity = event ? readStringField(event, ["severity", "Severity"]) : undefined;
        const safeMessage = event
          ? readStringField(event, ["safeMessage", "SafeMessage", "message", "Message"])
          : undefined;
        const timestamp = event ? readStringField(event, ["timestamp", "Timestamp"]) : undefined;

        return (
          <li className={event ? "route-stage route-stage-live" : "route-stage"} key={stage}>
            <div className="route-stage-header">
              <span className="route-stage-name">{stage}</span>
              {event && <small className="route-stage-badge">received</small>}
            </div>

            {event ? (
              <dl className="fact-list">
                {(completedCount !== undefined || totalCount !== undefined) && (
                  <div>
                    <dt>Progress</dt>
                    <dd>
                      {completedCount ?? "?"} / {totalCount ?? "?"}
                    </dd>
                  </div>
                )}
                {currentItem && (
                  <div>
                    <dt>Current item</dt>
                    <dd>{currentItem}</dd>
                  </div>
                )}
                {severity && (
                  <div>
                    <dt>Severity</dt>
                    <dd>{severity}</dd>
                  </div>
                )}
                {safeMessage && (
                  <div>
                    <dt>Message</dt>
                    <dd>{safeMessage}</dd>
                  </div>
                )}
                {timestamp && (
                  <div>
                    <dt>Timestamp</dt>
                    <dd>{timestamp}</dd>
                  </div>
                )}
              </dl>
            ) : (
              <p className="placeholder-text">Waiting for progress data.</p>
            )}
          </li>
        );
      })}
    </ol>
  );
}
