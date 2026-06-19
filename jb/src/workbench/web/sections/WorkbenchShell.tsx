"use client";

import { useEffect, useMemo, useRef, useState } from "react";

import { JobParameterPanel } from "../components/JobParameterPanel";
import { StatusPanel } from "../components/StatusPanel";
import {
  getVisibleApiErrorMessage,
  getVisibleApiErrorPayload,
  getJobProgressUrl,
  getJobResultUrl,
  isTerminalProgressEvent,
  PrismApiClient,
  type PrismApiErrorPayload,
  type PrismConfigResponse,
  type PrismHealthResponse,
  type PrismJobStartEnvelope,
  type PrismProcessingParameters,
  type PrismProgressEvent,
  type PrismResultResponse
} from "../services/prismApiClient";
import {
  buildWorkbenchSourceSummary,
  makeClientRequestToken,
  mergeFileSelections
} from "../services/workbenchSources";
import { ApiBoundarySection } from "./ApiBoundarySection";
import { ResultSection } from "./ResultSection";
import { RouteSection } from "./RouteSection";
import { UploadSection } from "./UploadSection";

async function resolveDroppedItems(items: DataTransferItemList): Promise<File[]> {
  const entries: FileSystemEntry[] = [];
  for (let i = 0; i < items.length; i++) {
    const entry = items[i].webkitGetAsEntry();
    if (entry) entries.push(entry);
  }

  const files: File[] = [];
  await Promise.all(entries.map((entry) => collectFilesFromEntry(entry, files)));
  return files;
}

async function collectFilesFromEntry(entry: FileSystemEntry, files: File[]): Promise<void> {
  if (entry.isFile) {
    const file = await readFileEntry(entry as FileSystemFileEntry);
    files.push(file);
  } else if (entry.isDirectory) {
    const children = await readDirectoryEntry(entry as FileSystemDirectoryEntry);
    await Promise.all(children.map((child) => collectFilesFromEntry(child, files)));
  }
}

function readFileEntry(entry: FileSystemFileEntry): Promise<File> {
  return new Promise((resolve, reject) => entry.file(resolve, reject));
}

function readDirectoryEntry(entry: FileSystemDirectoryEntry): Promise<FileSystemEntry[]> {
  return new Promise((resolve, reject) => {
    const reader = entry.createReader();
    const all: FileSystemEntry[] = [];

    function readBatch() {
      reader.readEntries((batch) => {
        if (batch.length === 0) {
          resolve(all);
        } else {
          all.push(...batch);
          readBatch();
        }
      }, reject);
    }

    readBatch();
  });
}

const defaultParameters: PrismProcessingParameters = {
  rename: true,
  transform: true,
  generation: true,
  ReturnOriginalImages: false,
  format: "zip"
};

export function WorkbenchShell() {
  const apiClient = useMemo(() => new PrismApiClient(), []);
  const unsubscribeFromProgressRef = useRef<(() => void) | undefined>(undefined);
  const [files, setFiles] = useState<File[]>([]);
  const [urlText, setUrlText] = useState("");
  const [parameters, setParameters] = useState<PrismProcessingParameters>(defaultParameters);
  const [isDragging, setIsDragging] = useState(false);
  const [isApiLoading, setIsApiLoading] = useState(true);
  const [isJobLoading, setIsJobLoading] = useState(false);
  const [apiErrorMessage, setApiErrorMessage] = useState<string | undefined>(undefined);
  const [apiErrorPayload, setApiErrorPayload] = useState<PrismApiErrorPayload | undefined>(
    undefined
  );
  const [health, setHealth] = useState<PrismHealthResponse | undefined>(undefined);
  const [config, setConfig] = useState<PrismConfigResponse | undefined>(undefined);
  const [job, setJob] = useState<PrismJobStartEnvelope | undefined>(undefined);
  const [progressEvents, setProgressEvents] = useState<PrismProgressEvent[]>([]);
  const [result, setResult] = useState<PrismResultResponse | undefined>(undefined);
  const sourceSummary = useMemo(
    () => buildWorkbenchSourceSummary(files, urlText),
    [files, urlText]
  );
  const canStartJob =
    sourceSummary.hasMinimumStartSources && sourceSummary.invalidUrls.length === 0 && !isJobLoading;

  useEffect(() => {
    let isActive = true;

    async function loadApiState() {
      setIsApiLoading(true);

      const [healthResult, configResult] = await Promise.allSettled([
        apiClient.getHealth(),
        apiClient.getConfig()
      ]);

      if (!isActive) {
        return;
      }

      if (healthResult.status === "fulfilled") {
        setHealth(healthResult.value);
      }

      if (configResult.status === "fulfilled") {
        setConfig(configResult.value);
      }

      const rejectedResult =
        healthResult.status === "rejected"
          ? healthResult.reason
          : configResult.status === "rejected"
            ? configResult.reason
            : undefined;

      if (rejectedResult) {
        setApiErrorMessage(getVisibleApiErrorMessage(rejectedResult));
        setApiErrorPayload(getVisibleApiErrorPayload(rejectedResult));
      }

      setIsApiLoading(false);
    }

    void loadApiState();

    return () => {
      isActive = false;
    };
  }, [apiClient]);

  useEffect(() => {
    function handleDragOver(event: DragEvent) {
      event.preventDefault();
      setIsDragging(true);
    }

    function handleDragLeave(event: DragEvent) {
      if (event.relatedTarget === null) {
        setIsDragging(false);
      }
    }

    function handleDrop(event: DragEvent) {
      event.preventDefault();
      setIsDragging(false);

      const items = event.dataTransfer?.items;
      if (items && items.length > 0) {
        void resolveDroppedItems(items).then(addFiles);
      } else if (event.dataTransfer?.files) {
        addFiles(Array.from(event.dataTransfer.files));
      }
    }

    window.addEventListener("dragover", handleDragOver);
    window.addEventListener("dragleave", handleDragLeave);
    window.addEventListener("drop", handleDrop);

    return () => {
      window.removeEventListener("dragover", handleDragOver);
      window.removeEventListener("dragleave", handleDragLeave);
      window.removeEventListener("drop", handleDrop);
      unsubscribeFromProgressRef.current?.();
    };
  }, []);

  function addFiles(incomingFiles: File[]) {
    setFiles((currentFiles) => mergeFileSelections(currentFiles, incomingFiles));
  }

  function removeFile(fileToRemove: File) {
    setFiles((currentFiles) =>
      currentFiles.filter((file) => {
        return !(
          file.name === fileToRemove.name &&
          file.size === fileToRemove.size &&
          file.lastModified === fileToRemove.lastModified
        );
      })
    );
  }

  async function startJob() {
    const currentSummary = buildWorkbenchSourceSummary(files, urlText);

    if (!currentSummary.hasMinimumStartSources) {
      setApiErrorMessage("A PRISM job needs at least one image source and one Excel source.");
      setApiErrorPayload(undefined);
      return;
    }

    if (currentSummary.invalidUrls.length > 0) {
      setApiErrorMessage("Fix invalid URL entries before starting a PRISM job.");
      setApiErrorPayload(undefined);
      return;
    }

    unsubscribeFromProgressRef.current?.();
    setIsJobLoading(true);
    setApiErrorMessage(undefined);
    setApiErrorPayload(undefined);
    setJob(undefined);
    setProgressEvents([]);
    setResult(undefined);

    const requestToken = makeClientRequestToken();
    const requestedFormat = parameters.format;

    try {
      const jobEnvelope = await apiClient.submitProcessJob({
        files: currentSummary.submittableFiles,
        urls: currentSummary.submittableUrls,
        parameters,
        clientRequestToken: requestToken
      });

      setJob(jobEnvelope);
      subscribeToProgress(jobEnvelope, requestedFormat);
    } catch (error) {
      setApiErrorMessage(getVisibleApiErrorMessage(error));
      setApiErrorPayload(getVisibleApiErrorPayload(error));
    } finally {
      setIsJobLoading(false);
    }
  }

  function subscribeToProgress(jobEnvelope: PrismJobStartEnvelope, requestedFormat: "zip" | "json") {
    const progressUrl = getJobProgressUrl(jobEnvelope);
    const resultUrl = getJobResultUrl(jobEnvelope);

    if (!progressUrl || !resultUrl) {
      setApiErrorMessage("The PRISM job envelope did not include progressUrl and resultUrl.");
      setApiErrorPayload(undefined);
      return;
    }

    unsubscribeFromProgressRef.current = apiClient.subscribeToProgress(progressUrl, {
      onProgress: (event) => {
        setProgressEvents((currentEvents) => [...currentEvents.slice(-99), event]);

        if (isTerminalProgressEvent(event)) {
          unsubscribeFromProgressRef.current?.();
          void loadResult(resultUrl, requestedFormat);
        }
      },
      onError: (error) => {
        setApiErrorMessage(error.message);
        setApiErrorPayload(undefined);
      }
    });
  }

  async function loadResult(resultUrl: string, requestedFormat: "zip" | "json") {
    setIsJobLoading(true);

    try {
      const loadedResult = await apiClient.getResult(resultUrl, requestedFormat);
      setResult(loadedResult);
    } catch (error) {
      setApiErrorMessage(getVisibleApiErrorMessage(error));
      setApiErrorPayload(getVisibleApiErrorPayload(error));
    } finally {
      setIsJobLoading(false);
    }
  }

  return (
    <main className={isDragging ? "workbench-shell workbench-shell-dragging" : "workbench-shell"}>
      <header className="workbench-header">
        <div>
          <p className="eyebrow">PRISM web workbench</p>
          <h1>Pipeline inspection </h1>
        </div>
        <p>
          Uploads and URLs are submitted to the API. Route facts appear only when progress or result
          payloads provide source-stage data.
        </p>
      </header>

      <StatusPanel
        isApiLoading={isApiLoading}
        isJobLoading={isJobLoading}
        hasAnyInput={sourceSummary.hasAnyInput}
        apiErrorMessage={apiErrorMessage}
        apiErrorPayload={apiErrorPayload}
        health={health}
        config={config}
        job={job}
        progressEventCount={progressEvents.length}
        hasResult={Boolean(result)}
      />

      <div className="workbench-grid">
        <div className="workbench-main-column">
          <UploadSection
            files={files}
            urlText={urlText}
            sourceSummary={sourceSummary}
            isDragging={isDragging}
            canStartJob={canStartJob}
            isSubmitting={isJobLoading}
            onFilesSelected={addFiles}
            onFileRemoved={removeFile}
            onUrlTextChanged={setUrlText}
            onStartJob={startJob}
          />
          <RouteSection events={progressEvents} />
          <ResultSection result={result} />
        </div>

        <aside className="workbench-side-column">
          <JobParameterPanel parameters={parameters} onParametersChanged={setParameters} />
          <ApiBoundarySection health={health} config={config} job={job} />
        </aside>
      </div>
    </main>
  );
}
