# Unity Gameplay Code Samples

[한국어 README](README.md)

> **Status:** Active development · Partial public source snapshot · Source review only

This repository contains selected Unity C# source files from an in-development **3D open-world action RPG**. It is structured for portfolio and technical review rather than distribution of the complete game.

Scenes, prefabs, art and audio, ScriptableObject assets, project settings, licensed third-party packages, and selected project-specific integration code are intentionally excluded. The snapshot is therefore **not a standalone Unity project or drop-in package**. After cloning, however, reviewers can inspect the architecture, algorithms, responsibility boundaries, and performance-oriented implementation without additional setup.

## Featured systems

| System | Main problem | Selected approach | Entry points |
|---|---|---|---|
| Runtime Building System | Runtime placement, snapping, removal, and structural stability | State-based interaction, automatic/manual/free snapping, preview/commit separation, parent-child graph, BFS support propagation, localized removal recomputation | [`BuildingSystem.cs`](Samples/RuntimeBuildingSystem/Source/Core/BuildingSystem.cs), [`StructuralIntegritySolver.cs`](Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs) |
| Custom Behavior Tree + Utility AI | Combining ordered behavior logic with context-sensitive action selection | Explicit node lifecycle and aborts, stateful/reactive composites, ScriptableObject-authored trees, periodic utility reevaluation with inertia | [`Node.cs`](Samples/BehaviorTreeUtilityAI/Source/Core/Node.cs), [`UtilitySelectorNode.cs`](Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs) |
| Boss Combat Framework | Preventing hit misses during fast skeletal motion and coordinating multi-stage skills | Temporal sphere sweeps, spatial capsule filling, one-hit-per-window tracking, animation-event-driven skills, pooled tentacle lifecycle | [`BossSweepDamager.cs`](Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs), [`TenTacleChild.cs`](Samples/BossCombatFramework/Source/Tentacle/TenTacleChild.cs) |

## Recommended review path

Start with [`docs/REVIEW_GUIDE.md`](docs/REVIEW_GUIDE.md), then read [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). The most representative implementation files are:

1. [`StructuralIntegritySolver.cs`](Samples/RuntimeBuildingSystem/Source/StructuralIntegrity/StructuralIntegritySolver.cs)
2. [`UtilitySelectorNode.cs`](Samples/BehaviorTreeUtilityAI/Source/Composite/UtilitySelectorNode.cs)
3. [`BossSweepDamager.cs`](Samples/BossCombatFramework/Source/HitDetection/BossSweepDamager.cs)

## Validate the cloned repository

Clone the public repository and run the validation tool. It uses only the Python standard library.

```bash
git clone <repository-url>
cd Unity-Gameplay-Code-Samples
python tools/verify_repository.py
```

It checks required documentation, UTF-8 encoding, corrupted characters, duplicate C# contents, generated-directory leakage, and local Markdown links.

## Public snapshot boundary

This repository does not claim standalone compilation. See [`docs/PUBLIC_SNAPSHOT.md`](docs/PUBLIC_SNAPSHOT.md) for the disclosure policy and [`docs/DEPENDENCIES.md`](docs/DEPENDENCIES.md) for omitted project and package dependencies.

## License

Cloning and viewing are permitted for portfolio, recruitment, and technical evaluation. Copying, modifying, redistributing, commercial use, or incorporation into another project requires prior permission. See [`LICENSE`](LICENSE).
