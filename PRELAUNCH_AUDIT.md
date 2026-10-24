# Pre-Launch Audit — Stream-Doctor (2026-10-24)

> Companion to [ROADMAP](./ROADMAP.md). Unscheduled product; this audit exists so the eventual phase starts from a decision record, not ambition.

## Verdict summary

| Area | Status | One-liner |
|---|---|---|
| Core focus | ⚠️ drifted in design doc | Pre-flight is the product; four subsystems is a suite |
| Scope sprawl | ❌ highest risk of all five | 6 features × 1 person × ≤ 2 hrs/week |
| Tier design | ✅ decided | Free / one-time Pro / Live-Data subscription |
| Feasibility | ❌ unproven | USB-drop and frame-pacing detection: no spike exists |

## Core promise & scope check

The promise: *pre-flight checks for streamers.* The design doc grows it into USB Sentinel + Frame Pacing Doctor + OBS Link + Snapshot/Restore — four substantial subsystems. For a side hustle this is the portfolio's biggest sprawl risk. **Recommended v1.0:**

| v1.0 (the product) | Post-1.0 (only if v1 earns it) |
|---|---|
| Pre-flight checklist + result screen | Post-flight debrief |
| USB drop detection (audio/capture/webcam) | Frame Pacing Doctor |
| Plain-English fix suggestions | OBS Link |
| | Snapshot & Restore |
| | Live-Data tier, cloud backups |

One core mechanic — *run the checklist, know before you go live* — shipped excellently. The rest are sequenced ambitions, and the ROADMAP should treat them that way.

## Tier design review

The three-tier model (Free / one-time Pro / Live-Data subscription) is decided and right. One warning to record now: **the Live-Data tier is a content business.** Maintaining current driver/device knowledge is the only part of this product with recurring labor — before committing, decide the data source (automated vendor/driver feeds vs. manual curation) or the subscription becomes a treadmill. Cloud backups stay subscription-only and opt-in (real per-user cost), per the master plan.

## Professionalism & polish bar

| Item | Bar |
|---|---|
| Feasibility spike | detect one real USB drop on one real machine — before any UI code |
| Demo | GIF/video: streamer runs pre-flight, finds a faulted capture device, fixes it |
| Naming | "Stream Doctor" is friendly and searchable — keep; check trademark conflicts before selling |
| Pricing page | the own-vs-rent split explained in one table (Free forever / Pro once / Live-Data monthly) |
| Design doc | revise flat ~$8/mo model → three tiers (flagged in ROADMAP, still pending) |

## "As-is" vs 1.0

- **As-is:** a design document and a decision record. Correct for an unscheduled repo.
- **Stream-Doctor 1.0 (whenever it starts):** pre-flight + USB Sentinel + fix suggestions, Free + one-time Pro tiers live, Live-Data tier deferred until the data question is answered.

## Ordered steps if/when this project starts

1. One-week feasibility spike (USB drop detection on Windows).
2. Pre-flight checklist v0 — the core loop, nothing else.
3. Revise the design doc's monetization section to the three-tier model.
4. Cut v1.0 scope to the table above; everything else becomes a backlog list.
