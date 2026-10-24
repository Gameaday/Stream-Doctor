# Stream Doctor

> **Pre-flight checks for streamers.** A Windows diagnostic & repair app that catches stream-killing problems *before* you go live — and fixes them in one click.

Every streamer knows the ritual: chat freezes, mic cuts out, the encoder stutters mid-raid, and you only find out from viewers. Stream Doctor flips that — it runs a **pre-flight checklist** against your streaming PC before you hit *Start Streaming*, and a **post-flight debrief** afterwards, so problems get found when they are cheap to fix.

## What it does

- **Pre-flight checks** — one button scans the things that actually kill streams: USB device drops, GPU/driver state, encoder health, OBS connectivity, audio devices, network stability.
- **Post-flight debrief** — after the stream, get a plain-English report of what degraded and when, so fixes compound instead of being re-discovered every stream.
- **USB Sentinel** — monitors audio interfaces, capture cards, and webcams for the invisible disconnect/reconnect events that cause silent failures.
- **Frame Pacing Doctor** — detects stutter, dropped frames, and pacing problems that average-FPS tools miss.
- **OBS Link** — talks to your OBS instance to pull scene/encoder/output diagnostics.
- **Snapshot & Restore** — capture a known-good configuration of devices, drivers, and settings so you can roll back after a bad Windows update.

## Status

Pre-development. The full product vision, architecture, and feature design live in the [product design document](./Stream-Doctor-Product-Design-Document.md). Built as a C# / .NET / WinUI 3 Windows app.

## Who it is for

Solo Twitch streamers and small streaming teams who do not have an IT department — anyone who has lost a stream to a mystery USB dropout and never wants to again.

## License

[MIT](./LICENSE)
