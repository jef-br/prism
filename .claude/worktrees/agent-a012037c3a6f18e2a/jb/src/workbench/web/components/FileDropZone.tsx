import { SUPPORTED_UPLOAD_ACCEPT } from "../services/workbenchSources";

interface FileDropZoneProps {
  isDragging: boolean;
  onFilesSelected: (files: File[]) => void;
}

export function FileDropZone({ isDragging, onFilesSelected }: FileDropZoneProps) {
  return (
    <label className={isDragging ? "drop-zone drop-zone-active" : "drop-zone"}>
      <span className="drop-zone-title">Drop PRISM sources anywhere</span>
      <span className="drop-zone-text">
        Images, Excel workbooks, zip archives, and URLs are collected for the API request.
      </span>
      <span className="button-like">Choose files</span>
      <input
        className="visually-hidden"
        type="file"
        multiple
        accept={SUPPORTED_UPLOAD_ACCEPT}
        onChange={(event) => {
          onFilesSelected(Array.from(event.currentTarget.files ?? []));
          event.currentTarget.value = "";
        }}
      />
    </label>
  );
}
