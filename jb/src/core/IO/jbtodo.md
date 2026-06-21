# IO Module Todo

## Fetch strategy stubs


- [ ] Implement Fetch_WeTransfer.cs.
  - File: `jb/src/core/IO/Fetchers/Fetch_WeTransfer.cs` — empty (1 line).
  - Block: No ticket assigned yet. WeTransfer URL support is deferred; not required for V1.
  - Estimated feasibility: **Low**. WeTransfer has no public download API for anonymous links. Resolving a download URL requires either (a) scraping the WeTransfer HTML download page — fragile, breaks when WeTransfer changes markup, and violates their ToS in most interpretations — or (b) the WeTransfer Business API, which requires a paid partner account and API key. Neither path is straightforward. Estimated effort: 3–5 days for the scraping approach, with high maintenance risk.
