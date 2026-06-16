import type { PrismResultResponse } from "../services/prismApiClient";

interface ResultSectionProps {
  result?: PrismResultResponse;
}

export function ResultSection({ result }: ResultSectionProps) {
  return (
    <section className="section-panel" aria-labelledby="result-heading">
      <div className="section-heading-row">
        <div>
          <p className="eyebrow">Result placeholder</p>
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
      <strong>No resultUrl data loaded.</strong>
      <p>
        The web workbench reads completed or failed job output only from the API resultUrl after
        progress reports a terminal state.
      </p>
    </div>
  );
}

function LoadedResult({ result }: { result: PrismResultResponse }) {
  if (result.kind === "zip") {
    return (
      <dl className="fact-list fact-list-wide">
        <div>
          <dt>Source</dt>
          <dd>resultUrl zip response</dd>
        </div>
        <div>
          <dt>Bytes</dt>
          <dd>{result.size}</dd>
        </div>
        <div>
          <dt>Manifest</dt>
          <dd>Expected inside manifest.json at archive root</dd>
        </div>
      </dl>
    );
  }

  return (
    <dl className="fact-list fact-list-wide">
      <div>
        <dt>Source</dt>
        <dd>resultUrl JSON response</dd>
      </div>
      <div>
        <dt>manifest</dt>
        <dd>{result.manifest ? "Present" : "Not supplied"}</dd>
      </div>
      <div>
        <dt>images</dt>
        <dd>{result.images ? "Present" : "Not supplied"}</dd>
      </div>
      <div>
        <dt>originalImages</dt>
        <dd>{result.originalImages ? "Present" : "Not supplied"}</dd>
      </div>
    </dl>
  );
}
