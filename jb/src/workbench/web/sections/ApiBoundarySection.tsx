import { useMemo } from "react";

import {
  getJobProgressUrl,
  getJobResultUrl,
  PrismApiClient,
  type PrismConfigResponse,
  type PrismHealthResponse,
  type PrismJobStartEnvelope
} from "../services/prismApiClient";

interface ApiBoundarySectionProps {
  health?: PrismHealthResponse;
  config?: PrismConfigResponse;
  job?: PrismJobStartEnvelope;
}

interface ApiRouteBoundary {
  name: string;
  method: "GET" | "POST";
  path: string;
  detail: string;
  /** True for routes templated on a live jobID (Progress/Result) — only clickable once a job exists. */
  isDynamic?: boolean;
}

/**
 * One card per route mapped in jb/src/api/Program.cs. Keep in sync with that file — this list is the
 * only place the workbench documents the full API surface.
 */
const apiBoundaries: ApiRouteBoundary[] = [
  {
    name: "Health",
    method: "GET",
    path: "/PRISM/health",
    detail: "Processing readiness and queue/runtime health."
  },
  {
    name: "Config",
    method: "GET",
    path: "/PRISM/config",
    detail: "Safe shared API configuration for upload and display hints."
  },
  {
    name: "Jobs",
    method: "GET",
    path: "/PRISM/jobs",
    detail: "Lists jobs held in memory by the coordinator."
  },
  {
    name: "Process",
    method: "POST",
    path: "/PRISM/process",
    detail: "Multipart request with request JSON and repeated input file parts."
  },
  {
    name: "Match",
    method: "POST",
    path: "/PRISM/match",
    detail: "Same multipart shape as Process — match and order only, no PRISM enrichment."
  },
  {
    name: "Match lite",
    method: "POST",
    path: "/PRISM/match/lite",
    detail: "Filenames and Excel only — no image bytes uploaded."
  },
  {
    name: "Progress",
    method: "GET",
    path: "/PRISM/jobs/{jobID}/progress",
    detail: "Live SSE events only; route stages are not replayed.",
    isDynamic: true
  },
  {
    name: "Result",
    method: "GET",
    path: "/PRISM/jobs/{jobID}/result",
    detail: "Fetched only after a terminal progress event.",
    isDynamic: true
  }
];

export function ApiBoundarySection({ health, config, job }: ApiBoundarySectionProps) {
  const apiClient = useMemo(() => new PrismApiClient(), []);

  return (
    <section className="section-panel" aria-labelledby="api-heading">
      <div className="section-heading-row">
        <div>
          <p className="eyebrow">Typed API client boundary</p>
          <h2 id="api-heading">
            Health / Config / Jobs / Process / Match / Match lite / Progress / Result
          </h2>
        </div>
      </div>

      <div className="boundary-grid">
        {apiBoundaries.map((boundary) => {
          const liveUrl =
            boundary.name === "Progress"
              ? (job && getJobProgressUrl(job)) || undefined
              : boundary.name === "Result"
                ? (job && getJobResultUrl(job)) || undefined
                : undefined;

          const url = liveUrl ?? apiClient.resolveUrl(boundary.path);
          const isClickable = !boundary.isDynamic || Boolean(liveUrl);

          return (
            <article className="boundary-card" key={boundary.name}>
              <strong>{boundary.name}</strong>
              <span>
                {boundary.method} {boundary.path}
              </span>
              <p>{boundary.detail}</p>
              {isClickable ? (
                <a className="boundary-url" href={url} target="_blank" rel="noreferrer noopener">
                  {url}
                </a>
              ) : (
                <code className="boundary-url boundary-url-template" title="Real URL is returned by Process once a job is submitted.">
                  {url}
                </code>
              )}
            </article>
          );
        })}
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
