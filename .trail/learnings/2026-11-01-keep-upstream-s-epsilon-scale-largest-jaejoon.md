---
title: "Keep upstream's epsilon scale (largest absolute coordinate) and document it instead of changing it"
author: Jaejoon
date: 2026-11-01
branch: HanJaeJoon/dogfood-r1
cost: { minutes: 11, tokens: 162196 }
id: l-70377c13
scope: repo
status: active
src: agent
touches:
  - tests/QuickHullSharp.Tests/DoublePrecisionTests.cs
  - src/QuickHullSharp/QuickHullBuilder.cs
  - docs/dogfooding/round1.md
---

## Decided
The epsilon passed to QuickHull is multiplied by the point cloud's largest absolute coordinate, as upstream akuukka/quickhull does, not by its extent. Round 1 dogfooding showed this coarsens hulls of clouds far from the origin (unit ball at offset 1e3: 199 -> 39 hull vertices on the Vector3 path), but we kept upstream semantics for port fidelity and documented it (README, XML) with the advice to translate the cloud near the origin first.

Epsilon validation (finite, >= 0, ArgumentOutOfRangeException) is an addition over upstream: NaN crashed with IndexOutOfRangeException and negative/infinite values silently returned hulls that do not contain the points.

## Watch out
Changing the scale to the extent would change every hull for off-origin input; treat it as a behavior change needing a CHANGELOG entry and the user's approval, not a fix.

In the code: QuickHullBuilder constructor (GetScale, epsilon check), docs/dogfooding/round1.md
