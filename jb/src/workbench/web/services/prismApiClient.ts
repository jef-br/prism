export const PRISM_ROUTE_STAGES = [
  "Imported",
  "Classified",
  "Matched",
  "Ordered",
  "Renamed",
  "Generated",
  "Transformed",
  "Exported"
] as const;

export type PrismRouteStage = (typeof PRISM_ROUTE_STAGES)[number];
export type PrismOutputFormat = "zip" | "json";

export interface PrismProcessingParameters {
  rename: boolean;
  transform: boolean;
  generation: boolean;
  ReturnOriginalImages: boolean;
  format: PrismOutputFormat;
}

export interface PrismProcessRequest extends PrismProcessingParameters {
  ClientRequestToken: string;
  Input: string[];
}

export interface PrismJobStartEnvelope {
  JobID?: string;
  JobId?: string;
  jobID?: string;
  jobId?: string;
  ClientRequestToken?: string;
  progressUrl?: string;
  ProgressUrl?: string;
  resultUrl?: string;
  ResultUrl?: string;
  status?: string;
}

export interface PrismProgressEvent {
  JobID?: string;
  JobId?: string;
  jobID?: string;
  jobId?: string;
  stage?: string;
  Stage?: string;
  routeStage?: string;
  RouteStage?: string;
  routeStageName?: string;
  RouteStageName?: string;
  status?: string;
  Status?: string;
  jobStatus?: string;
  JobStatus?: string;
  currentItem?: string;
  CurrentItem?: string;
  completedCount?: number;
  CompletedCount?: number;
  totalCount?: number;
  TotalCount?: number;
  severity?: string;
  Severity?: string;
  safeMessage?: string;
  SafeMessage?: string;
  message?: string;
  Message?: string;
  timestamp?: string;
  Timestamp?: string;
  [key: string]: unknown;
}

export interface PrismApiErrorPayload {
  correlationId?: string;
  code?: string;
  message?: string;
  details?: string[];
  fieldErrors?: string[];
  retryable?: boolean;
}

export type PrismHealthResponse = Record<string, unknown>;
export type PrismConfigResponse = Record<string, unknown>;

/**
 * Mirrors the C# record PrismJobSummary (jb/src/api/PrismJobCoordinator.cs).
 * PascalCase because the API sets PropertyNamingPolicy = null (Program.cs).
 */
export interface PrismJobSummary {
  JobID: string;
  Status: string;
  IsTerminal: boolean;
  CreatedAt: string;
  CompletedAt: string | null;
  ProgressUrl: string;
  ResultUrl: string;
  OkImages: number;
  KoImages: number;
}

export interface PrismJsonResultResponse {
  kind: "json";
  manifest?: unknown;
  images?: unknown;
  originalImages?: unknown;
  raw: unknown;
}

export interface PrismZipResultResponse {
  kind: "zip";
  blob: Blob;
  size: number;
}

export type PrismResultResponse = PrismJsonResultResponse | PrismZipResultResponse;

export interface PrismProcessJobInput {
  files: File[];
  urls: string[];
  parameters: PrismProcessingParameters;
  clientRequestToken: string;
}

export interface PrismMatchRequestInput {
  files: File[];
  urls: string[];
  clientRequestToken: string;
}

/**
 * Mirrors MatchOnlyResult.FileNameMap (jb/src/core/Models/MatchOnlyResult.cs) — the API's match routes
 * return this dictionary directly as the response body, not the full MatchOnlyResult record.
 */
export type PrismMatchFileNameMap = Record<string, string | null>;

export interface ProgressSubscriptionHandlers {
  onProgress: (event: PrismProgressEvent) => void;
  onError: (error: Error) => void;
}

export class PrismApiError extends Error {
  public readonly status: number;
  public readonly payload?: PrismApiErrorPayload;

  public constructor(message: string, status: number, payload?: PrismApiErrorPayload) {
    super(message);
    this.name = "PrismApiError";
    this.status = status;
    this.payload = payload;
  }
}

export class PrismApiClient {
  private readonly baseUrl: string;

  public constructor(baseUrl = process.env.NEXT_PUBLIC_PRISM_API_BASE_URL ?? "") {
    this.baseUrl = baseUrl.trim();
  }

  public async getHealth(): Promise<PrismHealthResponse> {
    return this.getJson<PrismHealthResponse>("/PRISM/health");
  }

  public async getConfig(): Promise<PrismConfigResponse> {
    return this.getJson<PrismConfigResponse>("/PRISM/config");
  }

  public async getJobs(): Promise<PrismJobSummary[]> {
    return this.getJson<PrismJobSummary[]>("/PRISM/jobs");
  }

  public async submitProcessJob(input: PrismProcessJobInput): Promise<PrismJobStartEnvelope> {
    const formData = new FormData();
    const request: PrismProcessRequest = {
      ClientRequestToken: input.clientRequestToken,
      rename: input.parameters.rename,
      transform: input.parameters.transform,
      generation: input.parameters.generation,
      format: input.parameters.format,
      ReturnOriginalImages: input.parameters.ReturnOriginalImages,
      Input: input.urls
    };

    formData.append("request", new Blob([JSON.stringify(request)], { type: "application/json" }));

    for (const file of input.files) {
      formData.append("input", file, file.name);
    }

    const response = await fetch(this.resolveUrl("/PRISM/process"), {
      method: "POST",
      body: formData
    });

    return this.readJsonResponse<PrismJobStartEnvelope>(response);
  }

  public async submitMatch(input: PrismMatchRequestInput): Promise<PrismMatchFileNameMap> {
    const formData = new FormData();
    const request: PrismProcessRequest = {
      ClientRequestToken: input.clientRequestToken,
      rename: false,
      transform: false,
      generation: false,
      format: "json",
      ReturnOriginalImages: false,
      Input: input.urls
    };

    formData.append("request", new Blob([JSON.stringify(request)], { type: "application/json" }));

    for (const file of input.files) {
      formData.append("input", file, file.name);
    }

    const response = await fetch(this.resolveUrl("/PRISM/match"), {
      method: "POST",
      body: formData
    });

    return this.readJsonResponse<PrismMatchFileNameMap>(response);
  }

  public async submitMatchLite(files: File[]): Promise<PrismMatchFileNameMap> {
    const formData = new FormData();

    for (const file of files) {
      formData.append("input", file, file.name);
    }

    const response = await fetch(this.resolveUrl("/PRISM/match/lite"), {
      method: "POST",
      body: formData
    });

    return this.readJsonResponse<PrismMatchFileNameMap>(response);
  }

  public subscribeToProgress(
    progressUrl: string,
    handlers: ProgressSubscriptionHandlers
  ): () => void {
    const eventSource = new EventSource(this.resolveUrl(progressUrl));
    let terminalSeen = false;
    const handleMessage = (event: Event) => {
      if (!(event instanceof MessageEvent)) {
        handlers.onError(new Error("The PRISM progress stream returned an invalid event type."));
        return;
      }

      if (typeof event.data !== "string") {
        handlers.onError(new Error("The PRISM progress stream returned a non-string event."));
        return;
      }

      try {
        const progressEvent = parseProgressEvent(event.data);
        if (isTerminalProgressEvent(progressEvent)) {
          terminalSeen = true;
        }
        handlers.onProgress(progressEvent);
      } catch (error) {
        handlers.onError(
          error instanceof Error
            ? error
            : new Error("The PRISM progress stream returned an invalid event.")
        );
      }
    };

    eventSource.onmessage = handleMessage;
    eventSource.addEventListener("progress", handleMessage);
    eventSource.addEventListener("status", handleMessage);
    eventSource.onerror = () => {
      // The API closes the SSE stream once the job reaches a terminal state; the browser reports that
      // normal close as an error. Only surface it when the stream dropped before any terminal event.
      if (terminalSeen) {
        eventSource.close();
        return;
      }

      handlers.onError(new Error("The PRISM progress stream is unavailable."));
    };

    return () => {
      eventSource.close();
    };
  }

  public async getResult(
    resultUrl: string,
    format: PrismOutputFormat
  ): Promise<PrismResultResponse> {
    const response = await fetch(this.resolveUrl(resultUrl), {
      method: "GET"
    });

    if (!response.ok) {
      throw await createPrismApiError(response);
    }

    if (format === "zip") {
      const blob = await response.blob();
      return {
        kind: "zip",
        blob,
        size: blob.size
      };
    }

    const raw = await response.json();
    const resultRecord = asRecord(raw);

    return {
      kind: "json",
      manifest: resultRecord?.manifest ?? resultRecord?.Manifest,
      images: resultRecord?.images ?? resultRecord?.Images,
      originalImages: resultRecord?.originalImages ?? resultRecord?.OriginalImages,
      raw
    };
  }

  private async getJson<TResponse>(path: string): Promise<TResponse> {
    const response = await fetch(this.resolveUrl(path), {
      method: "GET"
    });

    return this.readJsonResponse<TResponse>(response);
  }

  private async readJsonResponse<TResponse>(response: Response): Promise<TResponse> {
    if (!response.ok) {
      throw await createPrismApiError(response);
    }

    return response.json() as Promise<TResponse>;
  }

  /**
   * Resolves a PRISM API path to the absolute URL fetch()/links use. Host comes from
   * NEXT_PUBLIC_PRISM_API_BASE_URL when configured, falling back to the page's own origin — never
   * hardcoded, so this doubles as the URL shown for "check this route" links in the UI.
   */
  public resolveUrl(pathOrUrl: string): string {
    if (pathOrUrl.startsWith("http://") || pathOrUrl.startsWith("https://")) {
      return pathOrUrl;
    }

    const base = this.baseUrl.length > 0
      ? this.baseUrl
      : typeof window !== "undefined"
        ? window.location.origin
        : "";

    if (base.length === 0) {
      return pathOrUrl;
    }

    return new URL(pathOrUrl, ensureTrailingSlash(base)).toString();
  }
}

export function getProgressStage(event: PrismProgressEvent): PrismRouteStage | undefined {
  const rawStage = readStringField(event, [
    "stage",
    "Stage",
    "routeStage",
    "RouteStage",
    "routeStageName",
    "RouteStageName"
  ]);

  if (!rawStage) {
    return undefined;
  }

  return PRISM_ROUTE_STAGES.find((stage) => stage.toLowerCase() === rawStage.toLowerCase());
}

export function isTerminalProgressEvent(event: PrismProgressEvent): boolean {
  // The API carries the terminal job status ("Completed"/"Failed") in the Stage field of the final
  // progress event (PipelineProgressEvent has no Status field), so Stage must be inspected too.
  const status = readStringField(event, ["status", "Status", "jobStatus", "JobStatus", "stage", "Stage"]);

  if (!status) {
    return false;
  }

  const normalizedStatus = status.toLowerCase();
  return ["completed", "complete", "failed", "failure", "cancelled", "canceled"].includes(
    normalizedStatus
  );
}

export function readStringField(
  source: Record<string, unknown>,
  fieldNames: string[]
): string | undefined {
  for (const fieldName of fieldNames) {
    const value = source[fieldName];

    if (typeof value === "string" && value.trim().length > 0) {
      return value;
    }
  }

  return undefined;
}

export function getJobProgressUrl(job: PrismJobStartEnvelope): string {
  return job.progressUrl ?? job.ProgressUrl ?? "";
}

export function getJobResultUrl(job: PrismJobStartEnvelope): string {
  return job.resultUrl ?? job.ResultUrl ?? "";
}

export function readNumberField(
  source: Record<string, unknown>,
  fieldNames: string[]
): number | undefined {
  for (const fieldName of fieldNames) {
    const value = source[fieldName];

    if (typeof value === "number" && Number.isFinite(value)) {
      return value;
    }
  }

  return undefined;
}

export function getVisibleApiErrorMessage(error: unknown): string {
  if (error instanceof PrismApiError) {
    return error.payload?.message ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "The PRISM API request failed.";
}

export function getVisibleApiErrorPayload(error: unknown): PrismApiErrorPayload | undefined {
  if (error instanceof PrismApiError) {
    return error.payload;
  }

  return undefined;
}

function parseProgressEvent(data: string): PrismProgressEvent {
  const parsed = JSON.parse(data) as unknown;
  const record = asRecord(parsed);

  if (!record) {
    throw new Error("The PRISM progress stream returned an invalid event.");
  }

  return record as PrismProgressEvent;
}

async function createPrismApiError(response: Response): Promise<PrismApiError> {
  const payload = await readApiErrorPayload(response);
  const message = payload?.message ?? `PRISM API request failed with status ${response.status}.`;

  return new PrismApiError(message, response.status, payload);
}

async function readApiErrorPayload(response: Response): Promise<PrismApiErrorPayload | undefined> {
  const contentType = response.headers.get("content-type") ?? "";

  if (!contentType.toLowerCase().includes("application/json")) {
    return undefined;
  }

  const body = (await response.json()) as unknown;
  const record = asRecord(body);

  if (!record) {
    return undefined;
  }

  return {
    correlationId: readStringField(record, ["correlationId", "CorrelationId"]),
    code: readStringField(record, ["code", "Code"]),
    message: readStringField(record, ["message", "Message"]),
    details: readStringArrayField(record, ["details", "Details"]),
    fieldErrors: readStringArrayField(record, ["fieldErrors", "FieldErrors"]),
    retryable: readBooleanField(record, ["retryable", "Retryable"])
  };
}

function readStringArrayField(
  source: Record<string, unknown>,
  fieldNames: string[]
): string[] | undefined {
  for (const fieldName of fieldNames) {
    const value = source[fieldName];

    if (!Array.isArray(value)) {
      continue;
    }

    const strings = value.filter((item): item is string => typeof item === "string");
    return strings.length > 0 ? strings : undefined;
  }

  return undefined;
}

function readBooleanField(
  source: Record<string, unknown>,
  fieldNames: string[]
): boolean | undefined {
  for (const fieldName of fieldNames) {
    const value = source[fieldName];

    if (typeof value === "boolean") {
      return value;
    }
  }

  return undefined;
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return undefined;
  }

  return value as Record<string, unknown>;
}

function ensureTrailingSlash(value: string): string {
  return value.endsWith("/") ? value : `${value}/`;
}
