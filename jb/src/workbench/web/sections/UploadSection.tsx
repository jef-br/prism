import { FileDropZone } from "../components/FileDropZone";
import type { WorkbenchSourceSummary } from "../services/workbenchSources";

interface UploadSectionProps {
  files: File[];
  urlText: string;
  sourceSummary: WorkbenchSourceSummary;
  isDragging: boolean;
  canStartJob: boolean;
  isSubmitting: boolean;
  onFilesSelected: (files: File[]) => void;
  onFileRemoved: (file: File) => void;
  onUrlTextChanged: (urlText: string) => void;
  onStartJob: () => void;
}

export function UploadSection({
  files,
  urlText,
  sourceSummary,
  isDragging,
  canStartJob,
  isSubmitting,
  onFilesSelected,
  onFileRemoved,
  onUrlTextChanged,
  onStartJob
}: UploadSectionProps) {
  return (
    <section className="section-panel upload-panel" aria-labelledby="upload-heading">
      <div className="section-heading-row">
        <div>
          <p className="eyebrow">Upload surface</p>
          <h2 id="upload-heading">Images, Excel, zip, and URLs</h2>
        </div>
        <button className="primary-button" disabled={!canStartJob || isSubmitting} onClick={onStartJob}>
          {isSubmitting ? "Starting..." : "Start Prism Job"}
        </button>
      </div>

      <FileDropZone isDragging={isDragging} onFilesSelected={onFilesSelected} />

      <label className="field-label" htmlFor="url-input">
        URL text
      </label>
      <textarea
        id="url-input"
        className="url-input"
        value={urlText}
        onChange={(event) => onUrlTextChanged(event.currentTarget.value)}
        placeholder="One or more http/https sources separated by spaces, commas, or new lines."
      />

      <div className="source-grid">
        <SourceBucket title="Images" count={sourceSummary.imageFiles.length + sourceSummary.imageUrls.length} />
        <SourceBucket title="Excel" count={sourceSummary.excelFiles.length + sourceSummary.excelUrls.length} />
        <SourceBucket title="Zip" count={sourceSummary.zipFiles.length + sourceSummary.zipUrls.length} />
        <SourceBucket title="Other URLs" count={sourceSummary.remoteUrls.length} />
      </div>

      {!sourceSummary.hasAnyInput ? (
        <div className="placeholder-box">
          <strong>Empty input state.</strong>
          <p>Select local files or add URL text before starting a PRISM job.</p>
        </div>
      ) : null}

      {!sourceSummary.hasMinimumStartSources ? (
        <p className="validation-note">
          Start stays disabled until local input includes at least one image source and one Excel
          source. Server validation remains authoritative.
        </p>
      ) : null}

      <GroupedValidationMessages sourceSummary={sourceSummary} />

      {files.length > 0 ? (
        <ul className="file-list" aria-label="Selected local files">
          {files.map((file) => (
            <li key={`${file.name}:${file.size}:${file.lastModified}`}>
              <span>{file.name}</span>
              <small>{formatBytes(file.size)}</small>
              <button type="button" onClick={() => onFileRemoved(file)}>
                Remove
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

function SourceBucket({ title, count }: { title: string; count: number }) {
  return (
    <div className="source-bucket">
      <strong>{count}</strong>
      <span>{title}</span>
    </div>
  );
}

function GroupedValidationMessages({
  sourceSummary
}: {
  sourceSummary: WorkbenchSourceSummary;
}) {
  return (
    <div className="validation-stack" aria-live="polite">
      {sourceSummary.unsupportedFiles.length > 0 ? (
        <p>
          Only jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif are supported. Unsupported local
          files are not submitted.
        </p>
      ) : null}

      {sourceSummary.invalidUrls.length > 0 ? (
        <div>
          <strong>URL validation</strong>
          <ul>
            {sourceSummary.invalidUrls.map((invalidUrl) => (
              <li key={invalidUrl.value}>
                {invalidUrl.value}: {invalidUrl.reason}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }

  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
