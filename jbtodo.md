
Optimize the pipelining architecture so that Import and Match are fused togther to remove double image I/O.
Keep in mind that we want to try and keep the matchingservice open to the public as well.

-----

## Import→Match memory handoff: no ceiling, no spillover to disk (raised 2026-07-14, from T-3500)

**Answer:** OPEN

### What we changed, in plain English

Before T-3500, importing an image worked like this: Import opened the source file, decoded it,
resized/flattened it, and wrote a normalized JPG to the job's temp folder on disk. It then threw the
image out of memory. Later, Match opened that same file again from disk and decoded it a second time.
So every image got decoded twice, and the only thing kept between the two stages was a file path.

After T-3500, Import keeps the normalized JPEG **bytes in memory** and hands them straight to Match, so
Match can decode from memory instead of re-reading the file. The file is still written to disk exactly
as before (nothing downstream changed), and the bytes are dropped as soon as Match has used them.

That saves one disk read + one decode per image. Good. But it also means something new is true:

### The problem

**Between the moment Import finishes and the moment Match consumes each image, the normalized JPEG
bytes for EVERY image in the job are sitting in RAM at the same time.** Before this change, that number
was always zero — the bytes lived on disk and nowhere else.

There is **no ceiling and no spillover-to-disk**. The bytes are just a plain `byte[]` hanging off each
image record. Nothing checks how big the batch is. Nothing checks how much memory is free. Nothing says
"this job is too big, fall back to reading from disk". If the batch is huge, we simply allocate until
we run out of memory and the job dies — and it dies at the Import→Match boundary, after all the import
work has already been paid for.

### Why this matters more than it looks

Two multipliers make this bigger than a normal optimization trade-off:

1. **One job can be enormous.** `Prism_Config.json` sets `MAXIMUM_REQUEST_SIZE` to 26,843,545,600 bytes
   — that is **25 GiB of input per job**. Normalized images are capped at 2000×2000 (`Output.Images.
   Processed.MAXIMUM_SIZE_IN_PIXELS`), so each one re-encodes to roughly 0.5–1.5 MB of JPEG. A batch of
   2,500 images (the "heavy batch" size already referenced in our own matching perf notes) therefore
   parks somewhere around **1.5–4 GB in RAM** before Match starts draining it. A batch that actually
   approaches the 25 GiB input ceiling would be far worse.

2. **Concurrent jobs multiply it.** Peak memory is now *per running job*, and jobs run concurrently.
   Several large jobs that happen to sit at their Import→Match boundary at the same moment each hold
   their own full set of bytes. Nothing coordinates them or backs any of them off. Two or three
   max-size jobs landing together is enough to exhaust a normal box.

This is exactly the scenario we said we want to load-test: **multiple concurrent max-size jobs.** As the
code stands today, that test is likely to OOM, and it will look like a mysterious crash rather than an
obvious "batch too big" error.

### To be fair — what the change did do right

- It carries the **encoded JPEG bytes**, not a decoded bitmap. A decoded 2000×2000 RGBA image is ~16 MB;
  the encoded JPEG is ~1 MB. So the version we have is already ~16x cheaper than the obvious naive one.
- The bytes are set to null the moment Match consumes each image, so memory drains progressively during
  Match rather than being held for the whole job.
- The cross-process path is unaffected: the bytes are `[JsonIgnore]`d, so a remote Matching service still
  reads from disk exactly as before. Only the in-process path holds anything.

So the peak is "total normalized bytes of one batch", not "total decoded pixels", and it's a spike at one
boundary rather than a permanent occupancy. That is much better than it could have been. It is still
unbounded.

### The open question

Is one saved decode per image worth an unbounded, un-spillable memory spike that scales with batch size
and job concurrency? Options, roughly in order of least-to-most work:

1. **Put a ceiling on it.** Only carry bytes forward when the batch is below some configured limit
   (image count and/or total bytes). Above the limit, leave the field null — Match then transparently
   falls back to reading from disk, which is exactly today's pre-T-3500 behavior. This keeps the win for
   the common small/medium batch and makes the big-batch case degrade gracefully instead of dying. It's
   a handful of lines and one config key.
2. **Interleave Import and Match** so bytes are only held for the chunk Match is about to process, never
   the whole batch. This makes peak memory constant regardless of batch size, which is the actually-correct
   answer — but it means restructuring the stage handoff, which is real work and touches the pipeline
   contract.
3. **Revert the optimization.** Measure what one avoided decode per image is actually worth end-to-end
   first. If it's a couple of percent of total job time (plausible — CLIP, YOLO and upscaling dominate),
   the memory risk may simply not be worth buying.

**Before deciding, measure:** (a) what the saved decode is actually worth as a share of total job wall
time, and (b) real peak RSS on a large batch, so we're trading known against known instead of guessing.
