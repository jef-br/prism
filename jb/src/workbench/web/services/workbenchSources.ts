export const SUPPORTED_IMAGE_EXTENSIONS = [
  ".jpg",
  ".jpeg",
  ".png",
  ".tif",
  ".tiff",
  ".pdf",
  ".webp",
  ".bmp",
  ".gif"
] as const;

export const SUPPORTED_UPLOAD_ACCEPT = [
  ...SUPPORTED_IMAGE_EXTENSIONS,
  ".xlsx",
  ".zip"
].join(",");

export interface ParsedWorkbenchUrl {
  value: string;
  category: WorkbenchSourceCategory;
}

export interface InvalidWorkbenchUrl {
  value: string;
  reason: string;
}

export type WorkbenchSourceCategory = "image" | "excel" | "zip" | "remote";

export interface WorkbenchSourceSummary {
  imageFiles: File[];
  excelFiles: File[];
  zipFiles: File[];
  unsupportedFiles: File[];
  validUrls: ParsedWorkbenchUrl[];
  invalidUrls: InvalidWorkbenchUrl[];
  imageUrls: ParsedWorkbenchUrl[];
  excelUrls: ParsedWorkbenchUrl[];
  zipUrls: ParsedWorkbenchUrl[];
  remoteUrls: ParsedWorkbenchUrl[];
  submittableFiles: File[];
  submittableUrls: string[];
  hasAnyInput: boolean;
  hasMinimumStartSources: boolean;
}

export function buildWorkbenchSourceSummary(
  files: File[],
  urlText: string
): WorkbenchSourceSummary {
  const imageFiles: File[] = [];
  const excelFiles: File[] = [];
  const zipFiles: File[] = [];
  const unsupportedFiles: File[] = [];

  for (const file of files) {
    const category = categorizeSourceName(file.name);

    if (category === "image") {
      imageFiles.push(file);
    } else if (category === "excel") {
      excelFiles.push(file);
    } else if (category === "zip") {
      zipFiles.push(file);
    } else {
      unsupportedFiles.push(file);
    }
  }

  const parsedUrls = parseUrlText(urlText);
  const validUrls = parsedUrls.validUrls;
  const imageUrls = validUrls.filter((url) => url.category === "image");
  const excelUrls = validUrls.filter((url) => url.category === "excel");
  const zipUrls = validUrls.filter((url) => url.category === "zip");
  const remoteUrls = validUrls.filter((url) => url.category === "remote");
  const submittableFiles = [...imageFiles, ...excelFiles, ...zipFiles];
  const submittableUrls = validUrls.map((url) => url.value);
  const hasAnyInput = files.length > 0 || urlText.trim().length > 0;
  const hasMinimumStartSources =
    imageFiles.length + imageUrls.length > 0 && excelFiles.length + excelUrls.length > 0;

  return {
    imageFiles,
    excelFiles,
    zipFiles,
    unsupportedFiles,
    validUrls,
    invalidUrls: parsedUrls.invalidUrls,
    imageUrls,
    excelUrls,
    zipUrls,
    remoteUrls,
    submittableFiles,
    submittableUrls,
    hasAnyInput,
    hasMinimumStartSources
  };
}

export function makeClientRequestToken(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `prism-web-${Date.now().toString(36)}`;
}

export function mergeFileSelections(existingFiles: File[], incomingFiles: File[]): File[] {
  const knownFileKeys = new Set(existingFiles.map(createFileKey));
  const mergedFiles = [...existingFiles];

  for (const file of incomingFiles) {
    const fileKey = createFileKey(file);

    if (!knownFileKeys.has(fileKey)) {
      knownFileKeys.add(fileKey);
      mergedFiles.push(file);
    }
  }

  return mergedFiles;
}

function parseUrlText(urlText: string): {
  validUrls: ParsedWorkbenchUrl[];
  invalidUrls: InvalidWorkbenchUrl[];
} {
  const tokens = urlText
    .split(/[\s,]+/)
    .map((token) => token.trim())
    .filter((token) => token.length > 0);
  const validUrls: ParsedWorkbenchUrl[] = [];
  const invalidUrls: InvalidWorkbenchUrl[] = [];

  for (const token of tokens) {
    const parsedUrl = parseHttpUrl(token);

    if (!parsedUrl) {
      invalidUrls.push({
        value: token,
        reason: "URL must start with http:// or https://."
      });
      continue;
    }

    validUrls.push({
      value: token,
      category: categorizeSourceName(parsedUrl.pathname)
    });
  }

  return {
    validUrls,
    invalidUrls
  };
}

function parseHttpUrl(value: string): URL | undefined {
  try {
    const url = new URL(value);

    if (url.protocol !== "http:" && url.protocol !== "https:") {
      return undefined;
    }

    return url;
  } catch {
    return undefined;
  }
}

function categorizeSourceName(sourceName: string): WorkbenchSourceCategory {
  const normalizedName = sourceName.toLowerCase();
  const extensionStart = normalizedName.lastIndexOf(".");
  const extension = extensionStart >= 0 ? normalizedName.slice(extensionStart) : "";

  if (SUPPORTED_IMAGE_EXTENSIONS.includes(extension as (typeof SUPPORTED_IMAGE_EXTENSIONS)[number])) {
    return "image";
  }

  if (extension === ".xlsx") {
    return "excel";
  }

  if (extension === ".zip") {
    return "zip";
  }

  return "remote";
}

function createFileKey(file: File): string {
  return `${file.name}:${file.size}:${file.lastModified}`;
}
