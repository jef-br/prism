import type {
  PrismConfigResponse,
  PrismHealthResponse,
  PrismJobStartEnvelope
} from "../services/prismApiClient";

interface ApiBoundarySectionProps {
  health?: PrismHealthResponse;
  config?: PrismConfigResponse;
  job?: PrismJobStartEnvelope;
}

const apiBoundaries = [
  {
    name: "Process",
    behavior: "POST /PRISM/process",
    detail: "Multipart request with request JSON and repeated input file parts."
  },
  {
    name: "Progress",
    behavior: "Returned progressUrl",
    detail: "Live SSE events only; route stages are not replayed."
  },
  {
    name: "Result",
    behavior: "Returned resultUrl",
    detail: "Fetched only after a terminal progress event."
  },
  {
    name: "Config",
    behavior: "GET /PRISM/config",
    detail: "Safe shared API configuration for upload and display hints."
  },
  {
    name: "Health",
    behavior: "GET /PRISM/health",
    detail: "Processing readiness and queue/runtime health."
  }
];

export function ApiBoundarySection({ health, config, job }: ApiBoundarySectionProps) {
  return (
    <section className="section-panel" aria-labelledby="api-heading">
      <div className="section-heading-row">
        <div>
          <p className="eyebrow">Typed API client boundary</p>
          <h2 id="api-heading">Process / Progress / Result / Config / Health</h2>
        </div>
      </div>

      <div className="boundary-grid">
        {apiBoundaries.map((boundary) => (
          <article className="boundary-card" key={boundary.name}>
            <strong>{boundary.name}</strong>
            <span>{boundary.behavior}</span>
            <p>{boundary.detail}</p>
          </article>
        ))}
      </div>

      <dl className="fact-list fact-list-wide">
        <div>
          <dt>Health state</dt>
          <dd>{health ? "Loaded" : "Not loaded"}</dd>
        </div>
        <div>
          <dt>Config state</dt>
          <dd>{config ? "Loaded" : "Not loaded"}</dd>
        </div>
        <div>
          <dt>Job acknowledgement</dt>
          <dd>{job ? "Received from process endpoint" : "No process response yet"}</dd>
        </div>
      </dl>
    </section>
  );
}
