# ROADMAP — Stream-Doctor

> **Unscheduled.** Not part of the current four-phase year (Nov 2026 – Oct 2027).
> Master plan: [side-hustle-master-plan](https://github.com/Gameaday/home/blob/main/side-hustle-master-plan.md) · Full design: [product design document](./Stream-Doctor-Product-Design-Document.md)

**One-liner:** pre-flight checks for streamers — catch stream-killing problems *before* you go live.

## Status

Pre-development. This repo holds the product design document; build work starts only after the current year plan finishes or a phase gate frees capacity.

## Pricing model (decided 2026-10-24, Carl)

Three tiers. The split principle: **you own what is permanent; you rent only what needs constant feeding.**

| Tier | Model | What lives here |
|---|---|---|
| **Free** | $0 forever | Static local checks: pre-flight scan, post-flight debrief, device inventory |
| **Pro** | one-time purchase | Permanent local features with no ongoing data dependency: USB Sentinel, Frame Pacing Doctor, OBS Link, Snapshot & Restore |
| **Live-Data** | subscription | Only the features that require constant updates: known-issue checks against the current driver/device landscape, capture-card compatibility lookups, vendor driver advisories |

The subscription exists because stream hardware and drivers keep shifting — checking against a live, maintained database is a genuinely per-user ongoing cost, which is the only justification rule 2 allows. Everything that can be one-time, is one-time.

### Future: secure cloud backups

Eventually: opt-in encrypted cloud backup of stream configurations and snapshots. This has real per-user storage cost, so it will sit in the subscription tier only — never bundled silently, never a surprise charge.

### Design-doc note

The [product design document](./Stream-Doctor-Product-Design-Document.md) currently describes a flat Free + ~$8/mo model. The three-tier structure above supersedes that sketch; the design doc should be revised to match when the project enters development.

**License:** [MIT](./LICENSE)
