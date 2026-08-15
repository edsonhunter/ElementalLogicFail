# FourCorners — Agent Guide

4-player FFA lane-MOBA. Each player owns a corner base with 3 spawners; minions walk a lane
visiting the 3 enemy bases, then loop home and repeat. Server-authoritative Netcode for Entities.

> Local rule files live in `.agents/rules/{architect,executor,work}.md`. That directory is
> **gitignored**, so this file is the tracked, authoritative copy of the rules that matter.

---

## Stack

| | |
|---|---|
| Unity | 6000.3.8f1 (Unity 6.3) |
| Entities | 1.4.5 (+ Entities Graphics 1.4.18) |
| Netcode for Entities | 1.10.0 |
| Unity Physics | 1.4.5 |
| Burst | 1.8.27 · Collections 2.6.5 |
| Render pipeline | URP 17.3.0 · Addressables 2.8.1 · Input System 1.18.0 |

Entities/Physics/Burst/Collections are **transitive** via `com.unity.feature.ecs` — they are not
listed in `Packages/manifest.json`. Read `Packages/packages-lock.json` for real versions.

---

## Non-negotiable rules

**Simulation layer (`Scripts/Systems`, `Scripts/Components`)**
- `ISystem` + `[BurstCompile]` + `IJobEntity`. `SystemBase` only where a managed reference is
  genuinely unavoidable, and then it must be a leaf that does nothing but fan out to C# events.
- No managed objects in the simulation loop. No `class` components, no `foreach` over managed
  collections, no `string` — use `FixedString*`.
- **Never** call `GetExistingSystemManaged` from an `ISystem`. It kills Burst and it is how the
  world-discrimination bug got in. Discriminate with an entity tag instead.
- Every gameplay system carries `[WorldSystemFilter(...)]`. A system with no filter runs in the
  server, client, thin-client **and** default worlds.
- All structural changes go through `EntityCommandBuffer`, normally
  `EndSimulationEntityCommandBufferSystem.Singleton`. Do not mix in ad-hoc
  `new EntityCommandBuffer(Allocator.Temp)` — pick the singleton unless you can state why.
- **Writing a `ComponentLookup`/`BufferLookup` from the main thread requires
  `state.CompleteDependency()` first**, or the safety system throws once per frame as soon as
  any job reads that type. Put the call as late as possible — inside the branch that actually
  writes — so a system that ticks every frame does not stall the jobs every frame.
  `BaseAllocationSystem` and `ServerDisconnectSystem` are the two that need it.
- `BlobAsset` for shared immutable data; `DynamicBuffer` for per-entity lists.
- **Never put an `Entity` inside a `BlobAsset`.** Entity remapping on entity-scene load walks
  components and buffers only, so a baked `Entity` in a blob is a meaningless index at runtime.
  Prefab references belong in an `IBufferElementData` (see `RaceBaseVisual`).
- **A throw during ECB playback aborts the whole flush**, silently dropping the commands of every
  system that recorded into the same `EndSimulationEntityCommandBufferSystem` that frame. One
  bad `Instantiate` can look like a completely unrelated system failing — validate entities
  before recording rather than letting playback throw.

**Netcode**
- `GhostAuthoringComponent` + `[GhostField]` for replicated state.
- **`IRpcCommand` structs must never carry `[GhostComponent]`.** Codegen emits a ghost serializer
  that competes with the RPC serializer and *silently drops the payload on non-IPC transports* —
  it works over local IPC and fails over Relay, which makes it near-impossible to diagnose.
- Components on **child** entities of a ghost need
  `[GhostComponent(SendDataForChildEntity = true)]` or they do not replicate at all.

**Baking**
- Modern `Baker<T>` only. No `ConvertToEntity`.
- **Bakers must be deterministic.** No `UnityEngine.Random`, no time, no frame state — server and
  client entity scenes must be bit-identical.
- A baker that depends on hierarchy shape (e.g. reading `transform.parent`) must validate it and
  raise a baker-time error. Silent `Entity.Null` produces a system that fails forever with no log.

**Managed layer (`Scripts/Services`, `Manager`, `Scenes`, `View`, `Controller`)**
- Target a specific `World`. `foreach (var world in World.All)` is a bug in a project that runs
  multiple client worlds in one process (Multiplayer Play Mode). Use
  `ClientServerBootstrap.ClientWorld` / `.ServerWorld`.
- Every `+=` on a bridge event needs its `-=` in the matching `Unload()`.

---

## Project invariants

1. **`PlayerBase` is the team authority.** `SpawnerData.IsActive` / `.NetworkId` are derived
   mirrors for client replication — never gate simulation on them. `SpawnerSystem` reads
   `PlayerBase.IsActive` through `SpawnerData.PlayerBaseEntity`.
2. **A spawner's owning base comes from the GameObject parent** at bake time
   (`SpawnerAuthoring` → `GetEntity(transform.parent)`). Spawner prefabs must stay direct
   children of a `PlayerBaseAuthoring` object.
3. **A lane is a `DynamicBuffer<PathWaypoint>` on the spawner**, copied onto each minion at spawn.
   "Loop back home" is an index wrap in `PathFollowSystem`, not separate logic. The last waypoint
   of every lane is the spawner's own base. Lanes are **generated** by `LaneBakingSystem` from
   the base positions plus `SpawnerData.LaneIndex` — do not reintroduce hand-authored
   `List<Transform>` waypoints as the primary path; that data is invisible in the Inspector and
   is silently destroyed by ordinary prefab work.
6. **A race's base visuals must be referenced as a prefab asset**, never as an object placed in
   the subscene. A scene reference bakes a live entity, so every race renders on every corner.
4. **Team count is `TeamNumber`'s member count.** Never write a literal `4`. Note `TeamColor` in
   the same file has six members — the two enums are *not* interchangeable.
5. **A minion's only cross-frame state is `PathFollower`, `MinionData`, `Health`, `AttackCooldown`
   and `Engagement`.** Anything the client needs must be a `[GhostField]`; anything the server-only
   simulation needs must be gated to `ServerSimulation` or it will diverge on clients.
7. **Fighting is the presence of `Engagement`, not a state machine.** `PathFollowSystem` excludes
   it, so a minion stops walking the instant it is engaged and resumes from the same waypoint
   index when released — there is no "combat mode" to enter or leave. `CollisionSystem` is the
   only producer (physics contact), `EngagementSystem` the only releaser.
8. **`Engagement.Target` is always stale.** It is added and removed through the end-of-frame ECB,
   so every reader must re-check that the target still exists, still has `Health`, and is still
   in range. Do not try to fix this with `[UpdateBefore]`/`[UpdateAfter]` — the removal lands
   after every system in the frame regardless of ordering.
9. **`Health` alone does not kill.** Only entities tagged `DestroyOnDeath` are destroyed by
   `DeathSystem`. A corner base takes damage but must survive its own destruction as a
   deactivated ghost, so it deliberately carries `Health` without the tag.

---

## World & scene topology

Build scenes, in order: `Bootstrapper` → `LoaderScene` → `MainMenuScene` → `GameplayScene` →
`GameplaySubscene` → `ConfigScene` → `LobbyScene`.

`Bootstrapper.unity` is the entry point: `Bootstrapper` → `ApplicationManager` (DI container) →
services (`SystemBridge`, `Addressables`, `Multiplayer`) + managers (`Scene`, `Camera`) →
`BaseScene` controllers → `View/` UI.

`GameplayScene/GameplaySubscene.unity` is the ECS SubScene holding the 4 corner bases (each with
3 spawner children), the minion prefab map, and the `WanderArea` bounds.

**Worlds in play:** Server, the presentation Client (`ClientServerBootstrap.ClientWorld`), plus
in Multiplayer Play Mode additional full-client and thin-client worlds in the same process.
Anything that assumes "one client world" is wrong.

---

## Connection handshake (server-authoritative, order matters)

```
MatchStateBootstrapSystem  server, OnCreate   MatchState + TeamStatusElement[Teams.Count]
ClientRequestGameSystem    client            reads LocalPlayerSelection
                                             → GoInGameRequest{team, race}
ServerAcceptGameSystem     server            grants team slot | TeamRejectedRpc
                                             records race, elects host, → Lobby
                                             → LobbyStateUpdateRpc (broadcast)
HostStartGameSystem        server            StartGameRequest → Lobby → Active
                                             → MatchStartedRpc (broadcast)
ClientMatchStartedSystem   client            → MatchStartedTag
ClientSceneReadySystem     client            → ClientSceneReady
ClientStreamReadySystem    client            NetworkStreamInGame + ReadyForGhostsRequest
ServerStreamReadySystem    server            NetworkStreamInGame + PendingBaseAllocation
BaseAllocationSystem       server            activates PlayerBase + its 3 spawners
SpawnerSystem              server            ticks waves → MinionSpawnRequest
MinionSpawningSystem       server            instantiates minions with lane + team
```

And the way out (server-authoritative in the same way):

```
BaseAttackSystem           server            unengaged minions damage an enemy base in range
BaseDestructionSystem      server            Health 0 → PlayerBase.IsActive=false,
                                             TeamStatusElement.IsEliminated, minions cleared
MatchClockSystem           server            ticks ElapsedSeconds; past the threshold every
                                             live base decays until someone dies
MatchOutcomeSystem         server            ≤1 uneliminated slot → Ended + WinnerNetworkId
                                             → MatchEndedRpc (to every roster entry)
ClientMatchEndedSystem     client            → MatchEndedTag{Winner, LocalPlayerWon}
BridgeNotificationSystem   client            → ISystemBridgeService.OnMatchEnded(won)
```

**Survivors are counted from `TeamStatusElement`, never from `PlayerBase.IsActive`.** Bases are
activated several frames after the phase goes Active — the client has to report its SubScene ready
first — so a base-driven count reads zero survivors at kickoff and ends the match before it starts.

**`ClientSceneReady` has exactly one producer** (`ClientSceneReadySystem`). If you find yourself
adding a second path to signal client readiness, that is the bug — not the fix.

`ServerDisconnectSystem` runs before the accept system and reverses all of it — team slot, roster
entry, corner deactivation, minion cleanup, host re-election.

**Race vs team.** Team is which corner you occupy (exclusive, server-arbitrated). Race is what you
look like and what you spawn (not exclusive, always honoured). They are independent. `RaceCatalog`
is an *optional* baked BlobAsset: without it, spawners fall back to their baked `SpawnerPrefab`
buffer and bases keep their authored visuals — the old race-is-the-corner behaviour. Keep that
fallback intact when editing `SpawnerSystem` or `BaseRaceVisualSystem`.

`PresentationClientTag` marks the single client world that owns the managed scene loader and UI.
It is created by `SystemBridgeService` into `ClientServerBootstrap.ClientWorld` only. Systems
discriminate on this tag, never on "does this world have a managed service attached".

---

## Working on this repo

- ECS structural changes land at **end of frame** via the ECB singleton, so a producer and its
  consumer are always one frame apart regardless of `[UpdateBefore]`/`[UpdateAfter]`. Adding
  ordering attributes to "fix" a one-frame lag does nothing — check whether the constraint is
  load-bearing before adding it.
- Tests are EditMode-only (`FourCorners.Tests.asmdef` restricts platforms to Editor).
  Run via Test Runner; `Tests/EntityTest.cs` holds the archetype factories — if a system's query
  changes, that file must change with it.
- Verify multiplayer changes with `Assets/Settings/PlayMode/ServerAndHost.asset` and **2+ virtual
  players**. A single client masks the entire class of world-discrimination bugs.
- Relay-only failures are usually `[GhostComponent]`-on-RPC or child-entity ghost config — both
  are invisible over local IPC.
