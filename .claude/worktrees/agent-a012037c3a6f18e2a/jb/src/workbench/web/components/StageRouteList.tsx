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
    <ol className="route-list" aria-label="PRISM route stage placeholders">
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
              <span>Source stage: {stage}</span>
              <small>{event ? "SSE data received" : "Placeholder"}</small>
            </div>

            {event ? (
              <dl className="fact-list">
                <div>
                  <dt>Current item</dt>
                  <dd>{currentItem ?? "Not supplied"}</dd>
                </div>
                <div>
                  <dt>Completed</dt>
                  <dd>
                    {completedCount ?? "?"} / {totalCount ?? "?"}
                  </dd>
                </div>
                <div>
                  <dt>Severity</dt>
                  <dd>{severity ?? "Not supplied"}</dd>
                </div>
                <div>
                  <dt>Safe message</dt>
                  <dd>{safeMessage ?? "Not supplied"}</dd>
                </div>
                <div>
                  <dt>Timestamp</dt>
                  <dd>{timestamp ?? "Not supplied"}</dd>
                </div>
              </dl>
            ) : (
              <p className="placeholder-text">
                No source-stage data has been received for {stage}. Friendly display waits for API
                progress or result data.
              </p>
            )}
          </li>
        );
      })}
    </ol>
  );
}
