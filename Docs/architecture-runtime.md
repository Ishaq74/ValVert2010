# Runtime lifecycle (EventBus / Services / Bootstrap / Region activation)

Date : 2026-11-15 (Europe/Paris). Tests : `Assets/Tests/PlayMode/RuntimeLifecycleTests.cs` (25 synthetic-scene tests) + `FullSceneSmokeTests.cs` (the committed `ValVert.unity`, full scene).

## Rules
* **Nobody clears the bus or the registry globally.** Only `ResetForDomain()` (domain/subsystem registration hook, tests) does.
  Bootstrap enable/disable used to call `ClearAll()`/`Clear()` and wiped other systems' subscribers (StreetLights, GrassSeasonal, QA) — fixed.
* **Subscribers own their subscriptions** (subscribe in `OnEnable`, dispose in `OnDisable`). Destroyed subscriber objects are skipped and purged.
* **Sticky last value** per event type: late systems use `TryGetLast<T>` or `Subscribe(..., replayLast:true)`.
* A throwing handler is logged and does not stop delivery to others. Delivery iterates a snapshot (re-entrant unsubscribe safe).
* **Services are owner-safe**: `Register` never silently replaces a live different instance; `Unregister(instance)` only removes the caller's own entry; destroyed instances count as absent.
* **Bootstrap**: exactly one primary; others inert until the primary goes away, then the oldest enabled candidate is promoted (additive scenes / duplicates / reload). The primary only unregisters what it registered, and unhooks the clock on shutdown (no stacked handlers over enable/disable cycles).

## Region activation (`ValVertWorldStreaming`)
Not LOD, not async asset streaming, not Addressables: `SetActive` on authored roots (or each direct child) by **horizontal distance to the region's XZ bounds**, with hysteresis (`minHysteresis` enforced), `safetyMargin`, and rules:
never deactivate while the viewer is inside/over the region or is a child of its root; `AlwaysActive` is never deactivated; roots authored inactive are left alone; disabling the component re-activates what it deactivated; destroyed roots are dropped.
Regions are authored in `Settings/World/ValVertWorldConfig.asset` (`RegionSpec` list; validated by `ValVertWorldConfig.Validate()` and by a test against the real scene's root names). **No config → no regions** (nothing is guessed).

## In the committed scene
`Assets/Scenes/ValVert.unity` carries ONE host `ValVertEnvironment` = `ValVertClock` + `ValVertSeasons` (no foliage lists) + `ValVertRuntimeBootstrap` (config `Settings/World/ValVertWorldConfig.asset`). Added by `ValVertSceneWire.WireIntoScene` (idempotent; scene diff = host GameObject + 3 components + SceneRoots entry, nothing else). `ValVertEnvWire.Wire()` (full environment incl. foliage/weather lists) reuses the same host and the same single bootstrap.
Caveat inherited from the clock (`[ExecuteAlways]`): once the host is active, opening/saving the scene in the Editor lets `ValVertClock.Apply` bake fog/ambient/sun/`sky.mat` exposure into the scene; the wire step avoids that by creating the host inactive and flipping `m_IsActive` in the YAML.
Smoke result (full scene, batchmode): 768 regions (762 grass chunks, 4 east children, 2 terrains), far teleport → 0 chunks active, map corner → 654, back home → 762, one `Tick` ≈ 1.6 ms; shutdown re-activates everything; bootstrap re-enable re-registers; no unexpected errors.

## Honest limits
* All content stays resident in memory; this only saves rendering/physics/Update cost.
* The viewer resolves to `Main Camera` in the committed scene (no active `SimpleFPS` found); in Play the player controller would be preferred when present.
* Full foliage/weather environment is not persisted in the scene (it references private/ignored vendor assets); it is still wired in-editor by `ValVertEnvWire.Wire()` for reviews.

## Run
```
Unity -batchmode -nographics -projectPath <proj> -runTests -testPlatform PlayMode -testResults out.xml
```
