# Runtime lifecycle (EventBus / Services / Bootstrap / Region activation)

Date : 2026-11-15 (Europe/Paris). Tests : `Assets/_Project/Tests/PlayMode/RuntimeLifecycleTests.cs` (25 real Unity Play Mode tests).

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

## Honest limits
* All content stays resident in memory; this only saves rendering/physics/Update cost.
* `ValVert.unity` as committed does not yet carry the bootstrap (it is added by `ValVertEnvWire`); the tests build synthetic scenes and check the authored config against the real scene's object names, they do not play the full 1 km scene.

## Run
```
Unity -batchmode -nographics -projectPath <proj> -runTests -testPlatform PlayMode -testResults out.xml
```
