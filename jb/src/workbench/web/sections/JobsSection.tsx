"use client";

import { useEffect, useMemo, useState } from "react";

import {
  PrismApiClient,
  getVisibleApiErrorMessage,
  type PrismJobSummary
} from "../services/prismApiClient";

/**
 * Barebones listing of GET /PRISM/jobs. Fetches once on mount — refresh with F5.
 *
 * Jobs are held in memory by PrismJobCoordinator, so an empty list is normal: it stays empty until a
 * job is submitted to the running API process, is wiped on API restart, and expires after the
 * configured retention period. The raw JSON is rendered as-is, so an empty [] is visible proof the
 * endpoint is reachable rather than an ambiguous blank page.
 */
export function JobsSection() {
  const apiClient = useMemo(() => new PrismApiClient(), []);

  const [jobs, setJobs] = useState<PrismJobSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);

  useEffect(() => {
    let isActive = true;

    async function loadJobs() {
      try {
        const list = await apiClient.getJobs();
        if (!isActive) return;
        setJobs(list);
      } catch (error) {
        if (!isActive) return;
        setErrorMessage(getVisibleApiErrorMessage(error));
      } finally {
        if (isActive) setIsLoading(false);
      }
    }

    void loadJobs();
    return () => {
      isActive = false;
    };
  }, [apiClient]);

  return (
    <main>
      <h1>PRISM jobs</h1>
      {isLoading && <p>Loading…</p>}
      {!isLoading && errorMessage && <p>Error: {errorMessage}</p>}
      {!isLoading && !errorMessage && <pre>{JSON.stringify(jobs, null, 2)}</pre>}
    </main>
  );
}
