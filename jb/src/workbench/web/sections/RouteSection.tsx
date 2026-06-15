import { StageRouteList } from "../components/StageRouteList";
import { readStringField, type PrismProgressEvent } from "../services/prismApiClient";

interface RouteSectionProps {
  events: PrismProgressEvent[];
}

export function RouteSection({ events }: RouteSectionProps) {
  const jobStatus = getLatestJobStatus(events);

  return (
    <section className="section-panel" aria-labelledby="route-heading">
      <div className="section-heading-row">
        <div>
          <p className="eyebrow">Definitive route order</p>
          <h2 id="route-heading">
            Imported &gt; Classified &gt; Matched &gt; Ordered &gt; Renamed &gt; Generated &gt;
            Transformed &gt; Exported
          </h2>
        </div>
        {jobStatus && <JobStatusBadge status={jobStatus} />}
      </div>

      <StageRouteList events={events} />
    </section>
  );
}

function JobStatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase();
  const isTerminal = ["completed", "complete"].includes(normalized);
  const isFailed = ["failed", "failure", "cancelled", "canceled"].includes(normalized);
  const className = isTerminal
    ? "job-status-badge job-status-done"
    : isFailed
      ? "job-status-badge job-status-failed"
      : "job-status-badge job-status-running";

  return <span className={className}>{status}</span>;
}

function getLatestJobStatus(events: PrismProgressEvent[]): string | undefined {
  for (let i = events.length - 1; i >= 0; i--) {
    const status = readStringField(events[i], ["status", "Status", "jobStatus", "JobStatus"]);
    if (status) return status;
  }

  return undefined;
}
