import { useCallback } from "react";
import type { PrismResultResponse } from "../services/prismApiClient";

interface ResultSectionProps {
  result?: PrismResultResponse;
}

export function ResultSection({ result }: ResultSectionProps) {
  return (
    <section className="section-panel" aria-labelledby="result-heading">
      <div className="section-heading-row">
        <div>
          <p className="eyebrow">Output</p>
          <h2 id="result-heading">Manifest and output preview</h2>
        </div>
      </div>

      {result ? <LoadedResult result={result} /> : <EmptyResult />}
    </section>
  );
}

function EmptyResult() {
  return (
    <div className="placeholder-box">
      <strong>No result loaded.</strong>
      <p>
        The workbench fetches completed or failed job output from the API result URL after the
        progress stream reports a terminal state.
      </p>
    </div>
  );
}

function LoadedResult({ result }: { result: PrismResultResponse }) {
  if (result.kind === "zip") {
    return <ZipResult result={result} />;
  }

  return <JsonResult result={result} />;
}

function ZipResult({ result }: { result: Extract<PrismResultResponse, { kind: "zip" }> }) {
  const handleDownload = useCallback(() => {
    const url = URL.createObjectURL(result.blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "prism-output.zip";
    anchor.click();
    URL.revokeObjectURL(url);
  }, [result.blob]);

  return (
    <div>
      <dl className="fact-list fact-list-wide">
        <div>
          <dt>Format</dt>
          <dd>ZIP archive</dd>
        </div>
        <div>
          <dt>Size</dt>
          <dd>{formatBytes(result.size)}</dd>
        </div>
        <div>
          <dt>Contents</dt>
          <dd>manifest.json · OK/ · KO/ · source Excel file</dd>
        </div>
      </dl>
      <button className="action-button" onClick={handleDownload}>
        Download ZIP
      </button>
    </div>
  );
}

function JsonResult({ result }: { result: Extract<PrismResultResponse, { kind: "json" }> }) {
  const manifest = parseManifest(result.manifest);

  if (!manifest) {
    return (
      <div className="placeholder-box">
        <strong>Manifest not present in result.</strong>
        <p>The API result did not include a parseable manifest field.</p>
      </div>
    );
  }

  return (
    <div>
      <ManifestSummary manifest={manifest} />
      {manifest.routeSummaries.length > 0 && <RouteSummaries summaries={manifest.routeSummaries} />}
      {manifest.warnings.length > 0 && <ManifestWarnings warnings={manifest.warnings} />}
    </div>
  );
}

interface ParsedManifest {
  jobId: string | undefined;
  imageCount: number | undefined;
  excelCount: number | undefined;
  zipCount: number | undefined;
  okRenamed: number | undefined;
  koRecords: number | undefined;
  routeSummaries: string[];
  warnings: string[];
}

function ManifestSummary({ manifest }: { manifest: ParsedManifest }) {
  return (
    <dl className="fact-list fact-list-wide">
      {manifest.jobId && (
        <div>
          <dt>Job ID</dt>
          <dd>{manifest.jobId}</dd>
        </div>
      )}
      <div>
        <dt>Images accepted</dt>
        <dd>{manifest.imageCount ?? "—"}</dd>
      </div>
      <div>
        <dt>Excel files</dt>
        <dd>{manifest.excelCount ?? "—"}</dd>
      </div>
      {(manifest.zipCount ?? 0) > 0 && (
        <div>
          <dt>Zip files</dt>
          <dd>{manifest.zipCount}</dd>
        </div>
      )}
      <div>
        <dt>OK outputs</dt>
        <dd>{manifest.okRenamed ?? "—"}</dd>
      </div>
      <div>
        <dt>KO records</dt>
        <dd>{manifest.koRecords ?? "—"}</dd>
      </div>
    </dl>
  );
}

function RouteSummaries({ summaries }: { summaries: string[] }) {
  return (
    <div className="manifest-section">
      <h3 className="manifest-section-heading">Stage summaries</h3>
      <ol className="manifest-stage-list">
        {summaries.map((summary, index) => (
          <li key={index}>{summary}</li>
        ))}
      </ol>
    </div>
  );
}

function ManifestWarnings({ warnings }: { warnings: string[] }) {
  return (
    <div className="manifest-section">
      <h3 className="manifest-section-heading">Warnings</h3>
      <ul className="manifest-warning-list">
        {warnings.map((warning, index) => (
          <li key={index}>{warning}</li>
        ))}
      </ul>
    </div>
  );
}

function parseManifest(raw: unknown): ParsedManifest | null {
  if (typeof raw !== "object" || raw === null) return null;

  const m = raw as Record<string, unknown>;
  const summary = asRecord(m.Summary ?? m.summary);
  const routeSummaries = m.RouteSummaries ?? m.routeSummaries;
  const warnings = m.Warnings ?? m.warnings;
  const jobId = m.JobID ?? m.jobID;

  return {
    jobId: typeof jobId === "string" ? jobId : undefined,
    imageCount: getNumber(summary, "ImageCount", "imageCount"),
    excelCount: getNumber(summary, "ExcelCount", "excelCount"),
    zipCount: getNumber(summary, "ZipCount", "zipCount"),
    okRenamed: getNumber(summary, "OkRenamed", "okRenamed"),
    koRecords: getNumber(summary, "KoRecords", "koRecords"),
    routeSummaries: Array.isArray(routeSummaries)
      ? routeSummaries.filter((s): s is string => typeof s === "string")
      : [],
    warnings: Array.isArray(warnings)
      ? warnings.filter((w): w is string => typeof w === "string")
      : []
  };
}

function getNumber(
  source: Record<string, unknown> | undefined,
  ...keys: string[]
): number | undefined {
  if (!source) return undefined;

  for (const key of keys) {
    const value = source[key];
    if (typeof value === "number" && Number.isFinite(value)) return value;
  }

  return undefined;
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  if (typeof value !== "object" || value === null || Array.isArray(value)) return undefined;
  return value as Record<string, unknown>;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
