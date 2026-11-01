# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Desktop app for Windows 11. The interface is web (HTML, CSS, TypeScript with React, bundled by Vite) hosted inside the app through WebView2. The engine is C# (.NET 10): disk scanning, hardware specs, live sensors, FPS, settings. The two sides talk through a JSON message bridge. The in-game overlay stays a native, click-through WPF window because it must be light and never take focus. 3D uses Three.js. Choices made by the user.

## Users

One person: the owner of the PC, using the app on their own gaming machine (Windows 11, Intel i5-10400F, NVIDIA RTX 3060, 16 Go of RAM, a 1 To NVMe drive that is almost full). No other audience is planned and nothing is distributed. They are comfortable with web development but are not a Windows internals specialist: they want to understand why the disk is full, see what the machine contains, and get the most out of it when playing demanding games such as Microsoft Flight Simulator.

## Product Purpose

A personal control room for one PC. It explains where disk space goes and lets the owner reclaim it safely, shows what the machine is made of and how it is behaving right now, overlays live figures (FPS, load, temperatures) on top of games, and prepares the machine when a game starts. Success: the owner understands their PC, frees real space without fear of breaking anything, and plays with measurably better conditions.

## Positioning

A tool that tells the truth with measurements and never acts without a preview and a way back. Neighbouring tools (cleaners, boosters, benchmark suites) either claim gains they do not measure or delete without explanation. This one shows before and after figures from the machine itself, explains each space hog in plain French, sends deletions to the Recycle Bin by default, and injects nothing into games.

## Operating Context

- Windows 11 desktop, used at the PC, often with a game running or about to run. The overlay is read at a glance in the corner of a game.
- Real material: a full-drive scan takes seconds once warm; the drive holds large game libraries (Steam, Microsoft Flight Simulator add-ons and caches), a Recycle Bin, caches, and virtual disks.
- FPS needs Windows event tracing, which requires administrator rights, so the app asks for elevation only when the owner chooses to.
- Games run windowed or borderless windowed for the overlay; exclusive full screen hides any overlay.

## Capabilities and Constraints

- Built and working: hardware specs screen, storage scan with folder tree, treemap and known space hogs, live monitoring (CPU, RAM, GPU, VRAM, disk, network), in-game overlay with global hotkey `Ctrl+Alt+F10`, FPS from ETW (DirectX 9 to 12, administrator rights, live capture still to be validated).
- Decided next: 3D exploded view of the PC where each component is clickable for details; storage management with deletion (Recycle Bin by default, system folders protected, preview and confirmation, guided uninstall through game launchers); a game booster that closes heavy background apps, raises game priority and sets the power plan on game launch, restores everything afterwards, and reports a measured before/after.
- CPU temperature is not available without a kernel driver and is deliberately not enabled.
- Vulkan and OpenGL frame rates are not covered.
- Interface language: French only.
- The app must stay light: it measures the machine and must not weigh on it.

## Brand Commitments

- Name: treemap (lowercase).
- Palette imposed by the owner: Mandarin Orange `#FD550A`, Forest Green `#015D47`, Crest Yellow `#E0F354` as accent, and a cream white for the global ground.
- Binding expectations from the owner: a crafted, distinctive design that avoids generic AI-looking interfaces, with refined transitions and animations, and CSS-level care in the web interface.

## Evidence on Hand

- Real data from the owner's machine through the existing engine: specs, scans, live sensors. No testimonials, customers or benchmarks exist and none may be invented.
- A first measured finding to preserve as a fact: Microsoft Flight Simulator add-ons live in `Community`, the game content in `Official`, and `ROLLINGCACHE.CCC` is a regenerable cache.

## Product Principles

1. Understand before acting: every number is explained in plain language before any action is offered.
2. Every destructive action is previewed and reversible: Recycle Bin by default, protected system folders, explicit confirmation.
3. Honest figures: measured on the machine, with before and after, never an invented gain.
4. Nothing is injected into games: observation uses what Windows provides, so anti-cheat systems have nothing to object to.
5. The app stays light: it must not weigh on the machine it measures.
