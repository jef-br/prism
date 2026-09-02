#!/usr/bin/env python3
"""Builds every .drawio.svg in jb/docs/architecture/.

Run:  python3 jb/docs/architecture/_src/build.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from drawio import Diagram  # noqa: E402

OUT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))


# ---------------------------------------------------------------------------
# 1 — System context
# ---------------------------------------------------------------------------
def system_context() -> Diagram:
    d = Diagram("system-context", 1340, 700,
                "PRISM — system context",
                "Who calls PRISM, what runs inside one API process, and what it reads off disk.")

    d.band("bandA", "CALLERS", 30, 96, 210, 200)
    d.node("wb", "Web workbench", 48, 124, 174, 64, "client", "round",
           sub="Next.js · :3000\nread-only decorator")
    d.node("cli", "Direct API clients", 48, 204, 174, 64, "client", "round",
           sub="scripts · CI · tests")
    d.node("src", "Input sources", 30, 330, 210, 110, "external", "round",
           sub="multipart parts or URLs\nuploads · .xlsx · zip\nDropbox · WeTransfer · HTTPS")

    d.band("bandB", "API HOST — ONE PROCESS", 280, 96, 290, 470)
    d.node("api", "Prism.Api", 298, 124, 254, 50, "core", "box", bold=True,
           sub="ASP.NET Core 10 minimal API")
    d.node("routes",
           "/PRISM/health · /PRISM/config\n"
           "/PRISM/process — multipart\n"
           "/PRISM/jobs/{id}/progress — SSE\n"
           "/PRISM/jobs/{id}/result\n"
           "/PRISM/match · /PRISM/match/lite",
           298, 186, 254, 104, "plain", "box", font=11, align="left")
    d.node("queue", "PrismJobCoordinator", 298, 302, 254, 56, "core", "box",
           sub="bounded Channel + N workers")
    d.node("prism", "PrismService", 298, 370, 254, 56, "core", "box",
           sub="facade: validate → Pipeline → result")
    d.node("pipe", "Pipeline", 298, 438, 254, 56, "core", "box",
           sub="8 stages · typed hand-offs")
    d.node("store", "LocalArtifactStore", 298, 506, 254, 44, "core", "box")

    d.node("factory", "PipelineServiceFactory", 610, 300, 190, 90, "core", "round", font=11,
           sub="in-process by default;\nswaps in an HTTP client\nwhen PRISM_*_URL is set")

    d.band("bandC", "PUBLIC SERVICES — REMOTABLE", 840, 96, 250, 300)
    d.node("match", "Matching", 856, 126, 218, 52, "service", "box",
           sub="classify · match · order · rename")
    d.node("gen", "Generate", 856, 190, 218, 52, "service", "box",
           sub="synthetic images (gated off)")
    d.node("tx", "Transform", 856, 254, 218, 52, "service", "box",
           sub="crop · stretch · headcut")
    d.node("up", "Upscale", 856, 319, 218, 52, "service", "box",
           sub="Real-ESRGAN ×2")

    d.band("bandD", "CORE — NEVER REMOTED", 840, 420, 250, 146)
    d.node("ingest", "Ingest", 856, 452, 218, 44, "lib", "box",
           sub="import · normalize · IEM")
    d.node("export", "Export", 856, 508, 218, 44, "lib", "box",
           sub="manifest.json (+ ZIP)")

    d.band("bandE", "ON DISK — SHARED", 1116, 100, 210, 384)
    d.node("models", "ONNX assets", 1130, 126, 180, 96, "external", "cyl",
           sub="CLIP + YOLO26 → Matching\nReal-ESRGAN → Upscale\nresolved off the source tree")
    d.node("temp", "Job temp folder", 1130, 250, 180, 96, "external", "cyl",
           sub="%TEMP%/PRISM/{jobId}\nthe artifact bus")
    d.node("cfg", "core/config/*.json", 1130, 374, 180, 96, "external", "cyl",
           sub="14 files · validated at\nstartup · fail loud")

    d.edge("wb", "api")
    d.edge("cli", "api")
    d.edge("src", "api", waypoints=[(264, 385), (264, 149)])
    d.edge("api", "routes", exit="s", entry="n")
    d.edge("routes", "queue", "accepted job", exit="s", entry="n")
    d.edge("queue", "prism", "a worker picks it up", exit="s", entry="n")
    d.edge("prism", "pipe", exit="s", entry="n")
    d.edge("queue", "wb", "SSE progress", exit="w", entry="e", dashed=True,
           waypoints=[(252, 330), (252, 156)], label_dy=50)
    d.edge("pipe", "factory", "builds")
    d.edge("factory", "match", waypoints=[(816, 345), (816, 152)])
    d.edge("factory", "gen", waypoints=[(826, 345), (826, 216)])
    d.edge("factory", "tx", waypoints=[(836, 345), (836, 280)])
    d.edge("factory", "up")
    d.edge("pipe", "ingest", "ImportAsync", waypoints=[(730, 466), (730, 474)])
    d.edge("pipe", "export", "ExportAsync", waypoints=[(690, 466), (690, 530)])
    d.edge("match", "models")
    d.edge("ingest", "temp", "writes", label_dy=-60)

    d.note(30, 600,
           "Ingest, Matching and Export share one filesystem by contract: IngestResult carries an absolute\n"
           "NormalizedJpgPath, not bytes. A Matching host that cannot read the job temp folder fails loud.\n"
           "Only the four public services may be moved out of process; media enters PRISM through in-process ingress only.")
    return d


# ---------------------------------------------------------------------------
# 2 — Deployment topologies
# ---------------------------------------------------------------------------
def deployment() -> Diagram:
    d = Diagram("deployment-topologies", 1240, 700,
                "PRISM — deployment topologies",
                "One code path. Environment variables decide whether a service call is a method call or an HTTP hop.")

    d.band("A", "A · MODULAR MONOLITH — the default, no PRISM_*_URL set", 30, 96, 560, 440)
    d.node("a_api", "Prism.Api — one process", 56, 132, 508, 46, "core", "box", bold=True)
    d.node("a_ing", "Ingest", 70, 200, 230, 44, "lib")
    d.node("a_exp", "Export", 320, 200, 230, 44, "lib")
    d.node("a_match", "Matching", 70, 264, 230, 44, "service")
    d.node("a_gen", "Generate", 320, 264, 230, 44, "service")
    d.node("a_tx", "Transform", 70, 328, 230, 44, "service")
    d.node("a_up", "Upscale", 320, 328, 230, 44, "service")
    d.node("a_fs", "One job temp folder · one CLIP session · one Real-ESRGAN session",
           70, 396, 480, 42, "external", "round", font=11)
    d.node("a_desc",
           "Every service call is a method call. The Import → Match hand-off is a\n"
           "file path on the local disk — measured and kept that way deliberately.",
           70, 458, 480, 56, "plain", "round", font=10.5)
    d.edge("a_api", "a_ing", exit="s", entry="n", waypoints=[(310, 184), (185, 184)])
    d.edge("a_api", "a_exp", exit="s", entry="n", waypoints=[(310, 184), (435, 184)])

    d.band("B", "B · DISTRIBUTED — one URL set per remoted service", 650, 96, 560, 440)
    d.node("b_api", "Prism.Api — core process", 676, 132, 508, 46, "core", "box", bold=True)
    d.node("b_ing", "Ingest  (always in-process)", 690, 192, 230, 40, "lib", font=11)
    d.node("b_exp", "Export  (always in-process)", 940, 192, 244, 40, "lib", font=11)
    d.node("b_bus", "HTTP — PipelineServiceFactory returns a client for each service whose URL is set",
           690, 254, 480, 26, "band", "round", font=10)
    d.node("b_match", "Prism.ServiceHost", 690, 296, 240, 52, "service", font=11,
           sub="PRISM_SERVICE=matching")
    d.node("b_gen", "Prism.ServiceHost", 944, 296, 240, 52, "service", font=11,
           sub="PRISM_SERVICE=generate")
    d.node("b_tx", "Prism.ServiceHost", 690, 368, 240, 52, "service", font=11,
           sub="PRISM_SERVICE=transform")
    d.node("b_up", "Prism.ServiceHost", 944, 368, 240, 52, "service", font=11,
           sub="PRISM_SERVICE=upscale")
    d.node("b_fs", "Shared filesystem — a Matching host must see the files Ingest wrote, or it fails loud",
           676, 444, 508, 42, "external", "round", font=10.5)
    d.edge("b_api", "b_bus", exit="s", entry="n")
    d.edge("b_bus", "b_match", waypoints=[(810, 280)], exit="s", entry="n")
    d.edge("b_bus", "b_gen", waypoints=[(1064, 280)], exit="s", entry="n")
    d.edge("b_bus", "b_tx", waypoints=[(668, 280), (668, 394)], exit="s", entry="w")
    d.edge("b_bus", "b_up", waypoints=[(1198, 280), (1198, 394)], exit="s", entry="e")

    d.node("env",
           "PRISM_MATCHING_URL      PRISM_GENERATE_URL      PRISM_TRANSFORM_URL      PRISM_UPSCALE_URL",
           30, 566, 1180, 44, "contract", "round", font=12, bold=True)
    d.note(30, 638,
           "Unset → PipelineServiceFactory constructs the in-process implementation. Set → it constructs the matching HTTP client. "
           "Pipeline never learns which.\nThe two transform variables compose: an in-process Transform can delegate only its upscaling "
           "to a remote Upscale host, so no local Real-ESRGAN session is loaded.")
    return d


# ---------------------------------------------------------------------------
# 3 — Assembly map
# ---------------------------------------------------------------------------
def assemblies() -> Diagram:
    d = Diagram("assembly-map", 1240, 700,
                "PRISM — assemblies and dependency direction",
                "13 projects in jb/src/PRISM.sln. Arrows point at what a project references. Nothing points back up.")

    d.node("api", "Prism.Api", 150, 116, 250, 58, "core", bold=True, sub="api/ — HTTP surface, queue, SSE")
    d.node("host", "Prism.ServiceHost", 430, 116, 250, 58, "core", bold=True,
           sub="services/ — one public service per host")
    d.node("web", "workbench/web", 820, 116, 280, 58, "client", "round", dashed=True,
           sub="Next.js · npm · not in the .sln")

    d.node("core", "Prism.Core", 150, 224, 530, 84, "core", bold=True,
           sub="core/ — Pipeline, PrismService, Services/Matching, Services/Generate,\n"
               "Services/Transform, Services/Upscale, lib/Excel, lib/Ingress, lib/Export, lib/Zip")

    d.node("classify", "Prism.Services.\nMatching.Classify", 150, 358, 165, 68, "service", font=11,
           sub="ONNX / CLIP engine")
    d.node("txeng", "Prism.Services.\nTransform", 332, 358, 165, 68, "service", font=11,
           sub="transform engine")
    d.node("upeng", "Prism.Services.\nUpscale", 514, 358, 165, 68, "service", font=11,
           sub="Real-ESRGAN engine")

    d.node("contracts", "Prism.Core.Contracts", 150, 480, 529, 76, "contract", bold=True,
           sub="core/Models/ — every record, BatchManifest, PipelineProgressEvent,\n"
               "ConfigLoader, ModelAssetLocator, PrismConfigurationException")

    d.band("T", "TEST PROJECTS", 820, 224, 300, 332)
    d.node("tshared", "Prism.Tests.Shared", 840, 254, 260, 46, "lib", font=11,
           sub="PipelineFixture — classlib, not a test project")
    d.node("tcore", "Prism.Core.Tests", 840, 316, 260, 40, "lib", font=11)
    d.node("tmatch", "Prism.Services.Matching.Tests", 840, 366, 260, 40, "lib", font=11)
    d.node("tgen", "Prism.Services.Generate.Tests", 840, 416, 260, 40, "lib", font=11)
    d.node("ttx", "Prism.Services.Transform.Tests", 840, 466, 260, 40, "lib", font=11)
    d.node("tup", "Prism.Services.Upscale.Tests", 840, 516, 260, 34, "lib", font=11)

    d.edge("api", "core", exit="s", entry="n", waypoints=[(275, 198), (350, 198)])
    d.edge("host", "core", exit="s", entry="n", waypoints=[(555, 198), (480, 198)])
    d.edge("web", "api", "HTTP + SSE", dashed=True, exit="w", entry="n",
           waypoints=[(760, 145), (760, 92), (275, 92)], label_dy=-8)
    d.edge("core", "classify", exit="s", entry="n", waypoints=[(300, 332), (232, 332)])
    d.edge("core", "txeng", exit="s", entry="n", waypoints=[(415, 332)])
    d.edge("core", "upeng", exit="s", entry="n", waypoints=[(530, 332), (596, 332)])
    d.edge("classify", "contracts", exit="s", entry="n", waypoints=[(232, 452), (300, 452)])
    d.edge("txeng", "contracts", exit="s", entry="n", waypoints=[(415, 452)])
    d.edge("upeng", "contracts", exit="s", entry="n", waypoints=[(596, 452), (530, 452)])
    d.edge("core", "contracts", "direct", exit="w", entry="w",
           waypoints=[(96, 266), (96, 518)])
    d.edge("tshared", "core", exit="w", entry="e", dashed=True)
    d.note(824, 578, "Every test project references Prism.Core and\nPrism.Core.Contracts; namespaces are unchanged\n"
                     "by the split, so a --filter on PrismCoreTests.<Suite>\nstill works whichever project a suite lives in.", size=10.5)

    d.note(150, 596,
           "Physical folder ≠ assembly. Prism.Core.Contracts uses explicit <Compile Include> links to pull ~20 files out of\n"
           "core/lib/ and core/Services/ into one contract assembly, so records stay next to the code that owns them while\n"
           "every host still depends on one small, dependency-free package. Generate has no separate assembly — it lives in\n"
           "Prism.Core and is a “public service” by deployment, not by compilation.")
    return d


# ---------------------------------------------------------------------------
# 4 — Pipeline stages
# ---------------------------------------------------------------------------
def pipeline() -> Diagram:
    d = Diagram("pipeline-stages", 1340, 540,
                "PRISM — the pipeline",
                "Stage order is immutable. Each group emits its own progress events and hands the next one a typed record.")

    d.band("g1", "IngestService", 30, 110, 160, 186)
    d.band("g2", "MatchingService — one call", 190, 110, 640, 186)
    d.band("g3", "GenerateService", 830, 110, 160, 186)
    d.band("g4", "TransformService", 990, 110, 160, 186)
    d.band("g5", "Pipeline / Exporter", 1150, 110, 160, 186)

    stages = [
        ("Imported", "normalize → JPG\nExcel → IEM\nunzip · fetch URLs"),
        ("Classified", "features + CLIP\nvisual-hash dedup\nprovisional phenotype"),
        ("Matched", "waterfall → FamilyID\nthen phenotype refine"),
        ("Ordered", "phenotype → DetOrder\n_det slot per family"),
        ("Renamed", "FID_det#.jpg\ncollision check"),
        ("Generated", "hero copy + variant\nbackend gated off"),
        ("Transformed", "route → Tx_*\ncrop · stretch · fill"),
        ("Exported", "manifest.json\nZIP or JSON"),
    ]
    for i, (name, sub) in enumerate(stages):
        x = 40 + i * 160
        d.node(f"s{i}", name, x, 148, 140, 110, "core", "box", bold=True, font=12, sub=sub)
        if i:
            d.edge(f"s{i-1}", f"s{i}")

    d.note(40, 328, "TYPED HAND-OFFS  —  no shared mutable context, no stage reaches back", size=11)
    pills = [
        ("PrismJobRequest", "client"),
        ("IngestResult", "record"),
        ("MatchingResult", "record"),
        ("GenerateResult", "record"),
        ("TransformResult", "record"),
        ("ExportArtifacts", "record"),
        ("PrismJobResult", "client"),
    ]
    for i, (name, kind) in enumerate(pills):
        x = 40 + i * 185
        d.node(f"p{i}", name, x, 352, 165, 42, kind, "pill", font=11)
        if i:
            d.edge(f"p{i-1}", f"p{i}")

    d.node("ko", "User-file KO", 40, 434, 300, 62, "ko", "round", font=11, bold=True,
           sub="corrupt media · bad zip member · unmatched image\nrecorded in the manifest — the job continues")
    d.node("ffail", "PRISM-owned failure (FFAIL)", 370, 434, 340, 62, "ko", "round", font=11, bold=True,
           sub="missing config · missing or corrupt model · dead storage\nthrows PrismConfigurationException — the job stops")
    d.node("prog", "PipelineProgressEvent", 740, 434, 300, 62, "core", "round", font=11, bold=True,
           sub="one Started/Completed pair per stage,\nmonotonic per job, streamed live over SSE")
    d.node("noc", "No cancellation stage", 1070, 434, 240, 62, "plain", "round", font=11, bold=True,
           sub="an accepted job runs to\nnatural completion")
    return d


# ---------------------------------------------------------------------------
# 5 — Record lifecycle
# ---------------------------------------------------------------------------
def records() -> Diagram:
    d = Diagram("record-lifecycle", 1240, 700,
                "PRISM — record lifecycle",
                "ImageRecord_LAMBDA is the hub: every stage enriches the same instance, and the manifest projects from it.")

    d.node("base", "ImageRecord_Base", 470, 108, 300, 52, "contract", bold=True,
           sub="InitialFullName · Width · Height · NewName")

    d.node("iri", "ImageRecord_INPUT   (IRI)", 60, 208, 250, 76, "record", bold=True, font=11,
           sub="source kind · import status\nNormalizedJpgPath (absolute)\noriginal bytes never in the manifest")

    d.node("irl", "ImageRecord_LAMBDA   (IRL)", 380, 208, 400, 96, "record", bold=True, font=13,
           sub="the lifecycle hub — carries the whole route\nFeatures · SelectedPhenotype · Family · DetOrder\n"
               "BoundingBox · Subject · IsKo / KoReasonCode")

    d.node("iro", "ImageRecord_OUTPUT   (IRO)", 850, 208, 250, 76, "record", bold=True, font=11,
           sub="transform block (Transformed)\n+ export block (Exported)\nwritten by two stages")

    d.node("irg", "ImageRecord_GENERATED   (IRG)", 850, 322, 250, 62, "record", bold=True, font=11,
           sub="generation method · parameters\nquality decision · KO reason")

    d.band("ev", "BOUNDED EVIDENCE HUNG OFF THE LAMBDA", 380, 336, 340, 158)
    d.node("me", "MatchEvidence", 392, 366, 155, 44, "plain", font=11)
    d.node("ifs", "ImageFeatureSnapshot", 555, 366, 155, 44, "plain", font=10.5)
    d.node("subj", "SubjectDetection", 392, 424, 155, 44, "plain", font=11)
    d.node("ord", "OrderEvidence", 555, 424, 155, 44, "plain", font=11)

    d.node("iem", "InternalExcelModel   (IEM)", 60, 340, 250, 58, "lib", font=11, bold=True,
           sub="collated, deduplicated worksheets")
    d.node("fr", "FamilyIDRecord   (FR)", 60, 428, 250, 66, "lib", font=11, bold=True,
           sub="FamilyID + classified columns\n+ normalized tokens")

    d.node("bm", "BatchManifest   (BM)", 400, 548, 360, 80, "contract", bold=True,
           sub="Summary (BMS) · Models (BMMT) · ImageRows\nKO groups · route summaries · config snapshot\n"
               "the one audit contract both export formats project from")
    d.node("mir", "ManifestImageRow  (MIR)", 850, 548, 250, 40, "plain", font=11)
    d.node("journey", "ImageJourneyItem", 850, 600, 250, 40, "plain", font=11)

    d.edge("base", "iri", "is-a", exit="w", entry="n", arrow=False, dashed=True)
    d.edge("base", "irl", "is-a", exit="s", entry="n", arrow=False, dashed=True)
    d.edge("base", "iro", "is-a", exit="e", entry="n", arrow=False, dashed=True)
    d.edge("base", "irg", "is-a", exit="e", entry="e", arrow=False, dashed=True,
           waypoints=[(1170, 134), (1170, 353)])
    d.edge("iri", "irl", "Classified")
    d.edge("irl", "iro", "Transformed")
    d.edge("irl", "irg", "Generated", exit="e", entry="w", waypoints=[(815, 280), (815, 353)])
    d.edge("iem", "fr", exit="s", entry="n")
    d.edge("fr", "irl", "Matched → Family", exit="e", entry="s",
           waypoints=[(345, 461), (345, 326), (580, 326)])
    d.edge("irl", "bm", "Exported", exit="s", entry="n",
           waypoints=[(580, 314), (782, 314), (782, 520), (580, 520)])
    d.edge("bm", "mir", waypoints=[(824, 588), (824, 568)])
    d.edge("bm", "journey", exit="e", entry="w", waypoints=[(800, 588), (800, 620)])
    d.edge("iro", "mir", "safe fields only", exit="e", entry="e",
           waypoints=[(1160, 246), (1160, 568)])

    d.note(60, 546,
           "Original input bytes leave PRISM only when\nPPP.ReturnOriginalImages is true, and never\n"
           "through manifest.json. The manifest is the\naudit contract; byte-heavy payloads live in\n"
           "export-specific fields instead.", size=11)
    return d


# ---------------------------------------------------------------------------
# 6 — Job lifecycle at the API
# ---------------------------------------------------------------------------
def joblife() -> Diagram:
    d = Diagram("job-lifecycle", 1180, 760,
                "PRISM — job lifecycle at the API",
                "Submission is synchronous and cheap; processing is queued; progress is live-only.")

    d.node("post", "POST /PRISM/process", 60, 110, 300, 52, "client", "round", bold=True,
           sub="multipart: one request JSON part + N input parts")
    d.node("read", "PrismProcessIngressReader", 60, 190, 300, 88, "core", font=12, bold=True,
           sub="edge validation · URL policy (HostRules.json)\nfetch remote inputs to the job temp folder\nbuild PrismJobRequest — no API types cross into core")
    d.node("err", "400 pre-core error", 440, 190, 260, 62, "ko", "round", font=11, bold=True,
           sub="correlationId · code · fieldErrors\nno manifest.json is produced")
    d.node("enq", "PrismJobCoordinator.TryEnqueue", 60, 306, 300, 62, "core", font=12, bold=True,
           sub="bounded Channel<PrismApiJob>")
    d.node("full", "429 QUEUE_FULL", 440, 306, 260, 44, "ko", "round", font=11, bold=True)
    d.node("acc", "202 Accepted", 60, 396, 300, 68, "client", "round", bold=True,
           sub="JobID · ClientRequestToken\nprogressUrl · resultUrl · status Queued")

    d.node("worker", "Background worker", 60, 500, 300, 68, "core", font=12, bold=True,
           sub="MaxConcurrentJobs workers read the channel\nPrismService.Process(request, progress, ct)")
    d.node("sse", "GET /jobs/{id}/progress", 440, 500, 300, 88, "client", "round", bold=True, font=12,
           sub="SSE, live only — no replay, no polling\nPipelineProgressEvent per stage\nonly the originating client may subscribe")
    d.node("res", "GET /jobs/{id}/result", 440, 620, 300, 78, "client", "round", bold=True, font=12,
           sub="call after a terminal progress event\nformat=zip → manifest.json + OK/ + KO/\nformat=json → manifest + images{ok,ko}")
    d.node("ret", "Retention", 800, 620, 300, 78, "external", "round", font=11, bold=True,
           sub="Jobs.JobRetentionPeriodInHours\nafter expiry the JobID is stale and\nthe client drops it from its state")

    d.node("health", "GET /PRISM/health", 800, 110, 320, 96, "client", "round", bold=True, font=12,
           sub="acceptance status · active and queued counts\nMaxQueuedJobs · MaxConcurrentJobs\n"
               "config, model-asset and disk readiness\nsupported ONNX runtime providers")
    d.node("config", "GET /PRISM/config", 800, 226, 320, 96, "client", "round", bold=True, font=12,
           sub="accepted media types · size and count limits\noutput formats · visible feature flags\n"
               "safe values only — never local paths\nor private provider settings")
    d.node("gone", "Host shutdown", 800, 500, 300, 62, "plain", "round", font=11, bold=True,
           sub="queued and running jobs are process-local;\nV1 makes no restart-recovery guarantee")

    d.edge("post", "read", exit="s", entry="n")
    d.edge("read", "err", "invalid")
    d.edge("read", "enq", "valid PJR", exit="s", entry="n")
    d.edge("enq", "full", "queue full")
    d.edge("enq", "acc", "accepted", exit="s", entry="n")
    d.edge("acc", "worker", exit="s", entry="n")
    d.edge("worker", "sse", "progress")
    d.edge("sse", "res", "terminal event", exit="s", entry="n")
    d.edge("res", "ret", exit="e", entry="w")

    d.note(60, 620,
           "Two synchronous side doors bypass the queue\nentirely and answer in the request:\n\n"
           "POST /PRISM/match — full import → match → order\n"
           "POST /PRISM/match/lite — filenames + Excel only,\n"
           "no decode, no disk writes\n\n"
           "Both return just the old-name → new-name map.", size=11)
    return d


# ---------------------------------------------------------------------------
# 7 — Matching waterfall
# ---------------------------------------------------------------------------
def waterfall() -> Diagram:
    d = Diagram("matching-waterfall", 1180, 830,
                "PRISM — the matching waterfall",
                "One pass. A matched image leaves the pool, so each bracket only ever sees what the one above it could not claim.")

    d.node("in", "Every LAMBDA  +  FamilyRecords from the IEM", 300, 108, 520, 46, "lib", "round", bold=True)

    rows = [
        ("b1", "Bracket 1 — numeric, single token",
         "one filename token equals a family numeric value, exactly.\nTCD 0, fixed confidence 1.0. No edit-distance tolerance, ever."),
        ("b2", "Bracket 2 — numeric, multiple tokens",
         "in-order tokens concatenate to exactly the family value.\nRanked by fewest tokens used; accepted while TCD ≤ maxDistance."),
        ("b3", "Bracket 3 — string tokens",
         "accepted only when the image matches exactly one family AND that\nfamily has no matched record with the same phenotype yet."),
        ("b4", "Bracket 4 — semantic (CLIP + numeric + string)",
         "candidates limited to families with zero images so far.\nCLIP ProductType is a hard filter, ProductColor a conditional one;\n"
         "numeric tokens narrow, string tokens score. One winner ≥ SemanticThreshold."),
    ]
    y = 186
    for i, (nid, title, sub) in enumerate(rows):
        h = 88 if i < 3 else 100
        d.node(nid, title, 300, y, 520, h, "core", bold=True, font=12, sub=sub)
        d.node(f"{nid}_out", "matched", 880, y + h / 2 - 21, 130, 42, "lib", "pill", font=11)
        d.edge(nid, f"{nid}_out")
        if i:
            d.edge(rows[i - 1][0], nid, "unmatched", exit="s", entry="n")
            d.edge(f"{rows[i - 1][0]}_out", f"{nid}_out", exit="s", entry="n")
        y += h + 34

    d.edge("in", "b1", exit="s", entry="n")

    d.node("clean", "Cleanup — KO the rest", 300, y, 520, 66, "ko", bold=True, font=12,
           sub="no rename; the original filename is kept as provenance in the manifest.\n"
               "Still a candidate for two or more families → MATCHES_MULTIPLE_FAMILYIDS.")
    d.edge("b4", "clean", "still unmatched", exit="s", entry="n")

    d.node("fin", "Finalize", 880, y, 240, 78, "core", "round", bold=True, font=12,
           sub="cluster into FamilyID groups;\n+0.25 when two of numeric /\nstring / CLIP evidence converge")
    d.edge("b4_out", "fin", exit="s", entry="n")

    d.band("aux", "SUPPORTING PARTS — used by the brackets, not brackets themselves", 30, 268, 250, 394)
    d.node("sib", "SiblingPropagator", 44, 300, 222, 62, "plain", "round", font=11, bold=True,
           sub="carries a family's evidence\nacross its loose relations")
    d.node("rescue", "SubstringRescue", 44, 388, 222, 62, "plain", "round", font=11, bold=True,
           sub="digit-run index scan over\nwhat is still unmatched")
    d.node("noise", "NoiseFilter", 44, 476, 222, 74, "plain", "round", font=11, bold=True,
           sub="strips dimensions, dates and\nunit-adjacent numbers before\nscoring — never filename tokens")
    d.node("trans", "TranslationDictionary", 44, 576, 222, 62, "plain", "round", font=11, bold=True,
           sub="multilingual synonyms and\nstop words · no auto-detection")

    d.note(300, y + 100,
           "Ties are not resolved by det position. An image that stayed a candidate for more than one family is KO'd, deliberately —\n"
           "a V1 decision to be revisited once there is more match signal to weigh.")
    return d


# ---------------------------------------------------------------------------
# 8 — Classify / phenotype chain
# ---------------------------------------------------------------------------
def classify() -> Diagram:
    d = Diagram("classify-chain", 1300, 775,
                "PRISM — feature → phenotype → det slot",
                "Measurement happens twice: cheap and family-blind before matching, then again with the family known.")

    d.band("p1", "PHASE 1 · Classified stage — before matching, chunks of 8", 30, 100, 610, 400)
    d.node("load", "Load normalized JPEG", 56, 136, 250, 52, "core", font=11, bold=True,
           sub="from Import's in-memory bytes,\nelse from NormalizedJpgPath")
    d.node("par", "Parallel per chunk", 56, 204, 250, 100, "core", font=11, bold=True,
           sub="VisualHasher → perceptual hash\nFeatureAnalysisService.Analyze\n→ geometry, edges, background\nfailure here KOs the image")
    d.node("clip", "Batched CLIP run", 56, 320, 250, 88, "service", font=11, bold=True,
           sub="one InferenceSession.Run per chunk\nprocess-wide session, serialized\nfailure degrades, never KOs")
    d.node("prov", "Provisional phenotype", 56, 424, 250, 56, "core", font=11, bold=True,
           sub="PhenotypeRuleSet.EvaluateCandidates")
    d.node("dedup", "Visual dedup", 350, 204, 260, 76, "lib", font=11, bold=True,
           sub="highest resolution wins the group;\nnon-canonical copies get no output")
    d.node("tags", "Tags.Influential  /  Tags.Trivial", 350, 320, 260, 88, "record", font=11, bold=True,
           sub="≥ Confidence_Threshold → Influential\n≥ Cutoff, < Confidence → Trivial\nbelow Cutoff → discarded\nper-feature overrides live in CFG")

    d.node("match", "Matched stage — the waterfall runs", 680, 136, 590, 52, "core", "round", bold=True,
           sub="from here on the image's FamilyIDRecord is known")

    d.band("p2", "PHASE 2 · Refine — post-match, serial, cheap-first / most-eliminating-first", 660, 204, 610, 400)
    d.node("w1", "Wave 1 · IEM + filename", 686, 240, 560, 62, "core", font=11, bold=True,
           sub="Analyzer_ProductType (from the family's columns) · Analyzer_FilenameEvidence\n"
               "phase-1 edge intersections already sit in the snapshot, so this is the big cut")
    d.node("w2", "Wave 2 · human evidence", 686, 318, 560, 52, "core", font=11, bold=True,
           sub="YOLO26 person detections → Analyzer_HasHuman")
    d.node("w3", "Wave 3 · visual analyzers", 686, 386, 560, 118, "core", font=11, bold=True,
           sub="Analyzer_SubjectGeometry (shares one subject box) → DominantColors → ProductColor\n"
               "→ BackgroundColor → Exposure → MultipleProducts\n"
               "then SubjectDetector (classical CV, chroma + texture, never lightness)\n"
               "→ Analyzer_ShadowPresence.  Last, because it is steered by the colours above.")
    d.node("fin", "FinalizePhenotype", 686, 520, 560, 62, "core", font=11, bold=True,
           sub="first fully-satisfied rule in ImageRoles.json wins; otherwise the provisional pick\n"
               "survives only while the pool still contains it")

    d.node("pool", "PhenotypePool", 30, 540, 260, 76, "plain", "round", font=11, bold=True,
           sub="starts holding every phenotype;\neach wave eliminates the ones with\nstrong contra-evidence")

    d.node("order", "Ordered stage", 400, 620, 400, 76, "core", "round", bold=True,
           sub="DetOrderRules.json maps phenotype → _det slot per product type\nfilename hints and confidence only break ties")
    d.node("txin", "Transformed stage", 870, 620, 400, 76, "service", "round", bold=True,
           sub="reads intersects-* and salient-bbox only\nSelectedPhenotype no longer gates routing")

    d.edge("load", "par", exit="s", entry="n")
    d.edge("par", "clip", exit="s", entry="n")
    d.edge("clip", "prov", exit="s", entry="n")
    d.edge("par", "dedup")
    d.edge("clip", "tags")
    d.edge("prov", "match", exit="e", entry="w", waypoints=[(650, 452), (650, 162)])
    d.edge("match", "w1", exit="s", entry="n")
    d.edge("w1", "w2", exit="s", entry="n")
    d.edge("w2", "w3", exit="s", entry="n")
    d.edge("w3", "fin", exit="s", entry="n")
    d.edge("pool", "fin", "surviving candidates", exit="e", entry="w")
    d.edge("fin", "order", exit="s", entry="n", waypoints=[(966, 600), (600, 600)])
    d.edge("fin", "txin", exit="s", entry="n", waypoints=[(966, 600), (1070, 600)])

    d.note(30, 722,
           "Every feature starts at UNKNOWN and is only overwritten by a real measurement. A model's UseIt toggle does not skip its\n"
           "analyzer — the gate sits inside, at the point the model's output would be consumed, so a closed gate simply leaves UNKNOWN.")
    return d


# ---------------------------------------------------------------------------
# 9 — Transform routing
# ---------------------------------------------------------------------------
def transform() -> Diagram:
    d = Diagram("transform-routing", 1240, 790,
                "PRISM — transform routing",
                "Transform detects nothing of its own. It consumes what Classify measured and picks a strategy from edge intersections.")

    d.band("pp", "ImagePreProcessor — runs immediately before routing", 30, 100, 1180, 148)
    d.node("pp1", "EXIF orient\n+ flatten to white", 56, 136, 250, 92, "core", font=11, bold=True,
           sub="flat JPEG, no alpha, sRGB")
    d.node("pp2", "Salient bbox", 330, 136, 250, 92, "core", font=11, bold=True,
           sub="Canny + local-contrast mask\nat ≤ 512 px analysis size")
    d.node("pp3", "FinalizeGeometry", 604, 136, 250, 92, "core", font=11, bold=True,
           sub="promote a confident SubjectDetection\nover the legacy salient box,\nthen shrink for a cast shadow")
    d.node("pp4", "Upscale decision", 878, 136, 306, 92, "core", font=11, bold=True,
           sub="target: final output ≥ 800 px longest side\nOFF → Lanczos4, cap 1.33×  ·  ON → Real-ESRGAN, cap 1.42×\n"
               "over the cap → KO PREPROCESS_UPSCALE_EXCEEDED")

    d.node("sel", "ImageTransformer.SelectTransformer()", 380, 288, 480, 52, "core", "round", bold=True,
           sub="first match wins — reads intersects-top/bottom/left/right and salient-bbox only")

    d.node("dc", "Tx_DetailCropper", 60, 384, 420, 60, "service", bold=True, font=12,
           sub="bbox present AND any edge intersects")
    d.node("cs", "Tx_CenterAndStretch", 520, 384, 300, 60, "service", bold=True, font=12,
           sub="bbox present, no edge intersects")
    d.node("pi", "Tx_ProblemImageProcessor", 860, 384, 320, 60, "service", bold=True, font=12,
           sub="no bbox at all — last resort")

    d.node("pat",
           "1 intersection — touched edge flush, WhiteSpaceMargin on the far edge\n"
           "2 opposing — pinned axis at full extent, other axis centres on the bbox\n"
           "2 adjacent — corner flush; each axis shrinks or extends away from the corner\n"
           "3 intersections — one axis pinned, the open axis anchors flush\n"
           "4 intersections — centred square crop at min(w, h), no extension",
           60, 470, 420, 118, "plain", font=10, align="left")
    d.node("csx", "centre on the bbox,\nmargin 0.042 all round\nTx_LowContrastEnhancement (CLAHE)\nfirst when low-contrast is set",
           520, 470, 300, 118, "plain", font=10)
    d.node("pix", "conservative proportional resize,\nexported with warnings\n\nKO only when the image is under\n570 px and cannot be upscaled",
           860, 470, 320, 118, "plain", font=10)

    d.node("shrink", "Shrink or extend, per axis", 60, 618, 420, 60, "core", "round", font=11, bold=True,
           sub="crop first if the whole bbox stays in frame;\notherwise extend, and fill the new pixels")

    d.node("fill", "Tx_util_BgStretch — fill tier by extension ratio",
           520, 610, 660, 116, "lib", font=11, bold=True,
           sub="≤ 125% → mirror or clamp the border pixels outward\n"
               "≤ 142% → content-aware, patch-based propagation\n"
               "> 142% → OpenCV INPAINT_TELEA          > 250% → solid white\n"
               "seam feathering after tiers 1 and 2; never a Gaussian blur")

    for a, b in (("pp1", "pp2"), ("pp2", "pp3"), ("pp3", "pp4")):
        d.edge(a, b)
    d.edge("pp4", "sel", exit="s", entry="n", waypoints=[(1031, 264), (620, 264)])
    d.edge("sel", "dc", exit="s", entry="n", waypoints=[(620, 362), (270, 362)])
    d.edge("sel", "cs", exit="s", entry="n", waypoints=[(620, 362), (670, 362)])
    d.edge("sel", "pi", exit="s", entry="n", waypoints=[(620, 362), (1020, 362)])
    d.edge("dc", "pat", exit="s", entry="n")
    d.edge("cs", "csx", exit="s", entry="n")
    d.edge("pi", "pix", exit="s", entry="n")
    d.edge("pat", "shrink", exit="s", entry="n")
    d.edge("shrink", "fill")
    d.edge("csx", "fill", exit="s", entry="n", waypoints=[(670, 598), (850, 598)])

    d.note(30, 752,
           "Tx_CropSquare still compiles but no route reaches it. Phenotype and det slot were removed from routing in the 2026-08-11 "
           "rework — they stay on the record for other stages.")
    return d


DIAGRAMS = {
    "system-context": system_context,
    "deployment-topologies": deployment,
    "assembly-map": assemblies,
    "pipeline-stages": pipeline,
    "record-lifecycle": records,
    "job-lifecycle": joblife,
    "matching-waterfall": waterfall,
    "classify-chain": classify,
    "transform-routing": transform,
}


def main() -> None:
    wanted = sys.argv[1:] or list(DIAGRAMS)
    for name in wanted:
        path = os.path.join(OUT, f"{name}.drawio.svg")
        DIAGRAMS[name]().write(path)
        print(f"wrote {path}")


if __name__ == "__main__":
    main()
