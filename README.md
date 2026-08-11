# Four Corners

A 4-player free-for-all lane MOBA built on Unity DOTS (Entities + Burst + Job System) with
server-authoritative Netcode for Entities.

Each player claims one of four corner bases. Every base has three spawners that emit waves of
minions on a timer. Each spawner's lane visits the three enemy bases in a fixed order, then
returns home and repeats. Minions of different teams destroy each other on contact.

**Stack:** Unity 6000.3.8f1 · Entities 1.4.5 · Netcode for Entities 1.10.0 · Unity Physics 1.4.5 ·
Burst 1.8.27 · URP 17.3.0 · Addressables 2.8.1

> Working on this codebase with an AI agent? Read [CLAUDE.md](CLAUDE.md) first — it carries the
> architectural rules and the invariants this code depends on.

---

## Running it

1. Open `Assets/FourCorners/Scenes/Bootstrapper.unity`.
2. Press Play → **Play Game**.
3. Host via **Relay** (share the join code) or **Direct** (`127.0.0.1:7777`).
4. Once at least two players are in the lobby, the host's **Start** button appears.

For multiplayer testing use the Multiplayer Play Mode config at
`Assets/Settings/PlayMode/ServerAndHost.asset` with **two or more virtual players**. A single
client cannot exercise the world-discrimination logic in the connection handshake.

---

## Architecture

Two halves joined by one narrow seam.

### Managed layer
`Bootstrapper` → `ApplicationManager` (a `DIContainer` registry keyed by interface) → services
(`SystemBridgeService`, `AddressablesService`, `MultiplayerService`) and managers (`SceneManager`,
`CameraManager`) → `BaseScene` controllers → `View/` UI. One assembly per layer, with matching
`*.Interface` assemblies to keep the dependency graph acyclic.

### Simulation layer
Unmanaged `ISystem` structs and `IJobEntity` jobs, split by `[WorldSystemFilter]` into
server-authoritative simulation (spawning, lane movement, wander, collision, team allocation) and
client presentation (camera focus, base visibility). All structural changes go through
`EndSimulationEntityCommandBufferSystem`.

### The seam
`ISystemBridgeService` in one direction, `BridgeNotificationSystem` in the other. That system is
the only `SystemBase` in the connection pipeline and exists in exactly one world —
`ClientServerBootstrap.ClientWorld` — where it creates `PresentationClientTag`. Unmanaged systems
discriminate on that tag rather than probing for an attached managed service, which keeps them
Burst-compilable and, more importantly, makes "which world drives the UI" a single well-defined
answer rather than a per-world guess.

---

## Connection handshake

```
MatchStateBootstrapSystem  server, OnCreate   MatchState + TeamStatusElement[Teams.Count]
ClientRequestGameSystem    client            → GoInGameRequest{team}
ServerAcceptGameSystem     server            grants a team slot | TeamRejectedRpc
                                             elects host, WaitingForPlayers → Lobby
                                             → LobbyStateUpdateRpc (broadcast)
HostStartGameSystem        server            StartGameRequest → Lobby → Active
                                             → MatchStartedRpc (broadcast)
ClientMatchStartedSystem   client            → MatchStartedTag
ClientSceneReadySystem     client            → ClientSceneReady
ClientStreamReadySystem    client            NetworkStreamInGame + ReadyForGhostsRequest
ServerStreamReadySystem    server            NetworkStreamInGame + PendingBaseAllocation
BaseAllocationSystem       server            activates PlayerBase + its three spawners
```

Ghost streaming is deliberately deferred until the client reports its SubScenes are baked —
`NetworkStreamInGame` is never added at accept time.

`ServerDisconnectSystem` reverses all of it: frees the team slot, drops the roster entry,
deactivates the corner and its minions, and re-elects a host.

---

## Subsystems

**Spawning.** `SpawnerSystem` ticks each active spawner's wave timer and appends
`MinionSpawnRequest` entries; `MinionSpawningSystem` drains them into entities, copying the
spawner's lane onto each minion. Both gate on `MatchPhase.Active`. They are *not* ordered relative
to one another — requests travel through the EndSimulation ECB, so the one-frame lag is inherent.

**Lanes.** A lane is a `DynamicBuffer<PathWaypoint>` authored as a list of Transforms and baked
onto the spawner. `PathFollowSystem` advances an index and wraps to 0 at the end; since every
lane's final waypoint is its own base, that wrap *is* the loop-back-home behaviour. Movement adds
a Perlin-noise perpendicular sway so waves do not walk in a perfect line.

**Collision.** `CollisionSystem` runs after `PhysicsSimulationGroup` and consumes collision
events in a Bursted `ICollisionEventsJob`, using a `NativeHashSet<Entity>` to dedupe and
`BodyIndexA/B` as ECB sort keys to avoid data races. Different teams touching means both die.
There is no health or damage model yet.

**Camera.** `LocalPlayerCameraSystem` finds the base whose `NetworkId` matches the local player
and raises an event; `CameraController` snaps to it, then handles pan/zoom with desktop
edge-panning and mobile drag-panning separated by preprocessor directives.

**Addressables.** `AddressablesService` preloads the `Characters` and `Buildings` label groups
during `LoaderScene` before the main menu appears.

---

## Race and team selection

Team is which corner you occupy — exclusive, arbitrated by the server. Race is what you look like
and what you spawn — not exclusive, always honoured. They are independent.

The full path is implemented: the connection screen's optional race/team dropdowns write
`LocalPlayerSelection`, `ClientRequestGameSystem` sends it in `GoInGameRequest`,
`ServerAcceptGameSystem` records it, and `BaseAllocationSystem` writes it onto the replicated
`PlayerBase.Race`.

**Race visuals and rosters still need authoring.** `RaceCatalog` is an optional BlobAsset baked
from a `RaceCatalogAuthoring` component. Until one exists in the gameplay subscene, spawners fall
back to their baked `SpawnerPrefab` buffer and bases keep their prefab's own visuals — i.e. race
remains a property of the corner. See `SETUP-RACES.md` for the Editor steps.

## Known gaps

- **No combat model.** No health, damage, or targeting; minions annihilate on contact and never
  attack bases.
- **`Prefabs/Characters/Scientist Variant`** exists but has no entry in the subscene's model map,
  so requests for it are silently dropped.
- Prefabs and scenes still carry pre-rename `m_EditorClassIdentifier` hints
  (`ElementLogicFail.*`, `Corner.*`). These are cosmetic — every GUID/fileID reference resolves.
