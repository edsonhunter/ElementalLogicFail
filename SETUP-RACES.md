# Editor setup: runtime race selection

The C# side is done. These are the Editor steps I could not do headlessly.

**Nothing here is required to play.** Without a `RaceCatalog` the game behaves exactly as before:
spawners use their baked `SpawnerPrefab` buffer, bases keep their own visuals, and race stays a
property of the corner. Do this when you want race to actually be a choice.

---

## 1. Fix the swapped Ghost/Zombie prefabs (do this regardless)

In `Assets/FourCorners/Scenes/GameplayScene/GameplaySubscene.unity`, select the **Pool** GameObject
(`MinionPrefabAuthoring`). Two entries are crossed:

| Slot | Currently assigned | Should be |
|---|---|---|
| `ModelType: Ghost` | `Zombie Variant` | `Ghost Variant` |
| `ModelType: Zombie` | `Ghost Variant` | `Zombie Variant` |

Both wrong models spawn today.

While you are there, add the missing **`Scientist Variant`** entry with `ModelType: Scientist` —
without it, any spawn request for that model is silently dropped.

## 2. Re-serialise `HumanBase.prefab`'s team

`Assets/FourCorners/Prefabs/Buildings/HumanBase.prefab` still stores the pre-rename field name
(`Team: 0`) rather than `teamNumber`. Open the prefab, set **Team Number** on `PlayerBaseAuthoring`
to `Team1` explicitly, and save. It currently bakes correctly only by accident, because
`Team1 == 0`.

## 3. Author the race catalog

1. In `GameplaySubscene`, create an empty GameObject named `RaceCatalog`.
2. Add the **Race Catalog Authoring** component.
3. Set **Races** size to 4 and fill in:

| race | baseVisualPrefab | roster |
|---|---|---|
| `Human` | visuals from `HumanBase` | Warrior, Mage |
| `Orc` | visuals from `OrcBase` | Orc |
| `Soldier` | visuals from `SoldierBase` | Soldier |
| `Monster` | visuals from `MonsterBase` | Skeleton, Ghost, Zombie |

The rosters above reproduce what each base prefab spawns today, so this step alone changes nothing
visible — it just moves the data somewhere race can select from.

**A roster must not be empty.** The baker logs an error, and an empty roster means those spawners
produce nothing.

## 4. Split visuals out of the base prefabs

`baseVisualPrefab` must be a **visuals-only** prefab: meshes and renderers, no
`PlayerBaseAuthoring`, no `GhostAuthoringComponent`, no spawner children. `BaseRaceVisualSystem`
instantiates it and parents it under the claimed corner at runtime.

For each of the four base prefabs: duplicate it, strip everything except the visual hierarchy, and
name it e.g. `HumanBaseVisual`. Then remove those same meshes from the original base prefab, so the
corner renders nothing until a player claims it and a race is chosen.

Keep on the original base prefab: `PlayerBaseAuthoring`, `GhostAuthoringComponent`,
`LinkedEntityGroupAuthoring`, and the three `SpawnerA/B/C` children.

> ### ⚠ The visual prefabs must stay ASSETS — never drag them into the subscene
>
> `RaceCatalogAuthoring.baseVisualPrefab` must reference the prefab **in the Project window**.
> Drag from `Assets/FourCorners/Prefabs/Buildings/`, not from the Hierarchy.
>
> A correct reference serialises as `{fileID: 100100000, guid: …, type: 3}`. A reference to a
> scene object serialises as a bare `{fileID: 8201202360746502142}` — and because that object
> is a live scene entity rather than a prefab, **every race's visuals render permanently on
> every corner**. `BaseRaceVisualSystem` instantiates the prefab at runtime; it must not
> already be in the scene.
>
> If you already dragged them in: delete the four `*BaseVisual` objects from the subscene
> Hierarchy, then re-assign the four `baseVisualPrefab` fields from the Project window.

> Spawner children must stay **direct children** of the `PlayerBaseAuthoring` object.
> `SpawnerAuthoring` resolves its owning base from `transform.parent` and now logs a bake-time
> error if that is not the case — previously it baked `Entity.Null` and the spawner silently
> never fired.

Once visuals are stripped, the four base prefabs become interchangeable; only the `Team Number`
differs, so you can collapse them into one prefab placed at four corners.

> ### Lanes are now generated, not authored
>
> Earlier revisions of this document suggested collapsing the prefabs without warning that lane
> data lived in per-instance prefab overrides — 48 `Transform` references that a prefab swap
> silently destroys, leaving spawners with empty lanes and no error anywhere.
>
> `LaneBakingSystem` now derives every lane from the four base positions at bake time. Per
> spawner you set one integer, **Lane Index** (`0`, `1` or `2`), so the base's three spawners
> take three different routes. Each route visits all three enemy corners and ends at home,
> which is what the old overrides encoded.
>
> Leave **Waypoints** empty. It still works as a manual override if you ever want a custom
> route, but any entry there disables generation for that spawner.

## 5. Build the race / team selection UI

### The constraint that decides where this UI can live

`ClientRequestGameSystem` sends `GoInGameRequest` the instant the client is assigned a
`NetworkId` — which happens moments after the connection is established. So:

> **The player's choice must be recorded BEFORE the Host/Join button is pressed.**

Anything after that is too late; the server has already granted a slot. That rules out a
selection screen shown in the lobby, and it's why the dropdowns live on the connection screen.

If you want a *dedicated* selection scene, it has to sit **before** the connection screen —
MainMenu → Selection → Connection → Lobby — and call
`GetService<ISystemBridgeService>().SetLocalPlayerSelection(teamIndex, race)` before advancing.
The wiring below is the same either way; only the host GameObject differs.

### Steps (dropdowns on the existing connection screen)

1. Open `Assets/FourCorners/Scenes/MainMenuScene.unity`.
2. Find the panel carrying the **`ConnectionScreenUI`** component — the same object whose
   `Host Relay Btn` / `Join Relay Btn` fields are already assigned.
3. Add two dropdowns under it: **GameObject → UI → Dropdown - TextMeshPro**. Name them
   `RaceDropdown` and `TeamDropdown`.
4. On `RaceDropdown`, clear **Options** and add these four, **in this exact order**:

   | Index | Label   | Maps to |
   |---|---|---|
   | 0 | Human   | `RaceType.Human` |
   | 1 | Orc     | `RaceType.Orc` |
   | 2 | Soldier | `RaceType.Soldier` |
   | 3 | Monster | `RaceType.Monster` |

5. On `TeamDropdown`, add five options, **in this exact order**:

   | Index | Label      | Maps to |
   |---|---|---|
   | 0 | Any corner | `-1` — server picks the first free slot |
   | 1 | Corner 1   | `TeamNumber.Team1` |
   | 2 | Corner 2   | `TeamNumber.Team2` |
   | 3 | Corner 3   | `TeamNumber.Team3` |
   | 4 | Corner 4   | `TeamNumber.Team4` |

6. Select the `ConnectionScreenUI` object. Under **Lobby Selection (optional)**, drag
   `RaceDropdown` into **Race Dropdown** and `TeamDropdown` into **Team Dropdown**.

**Order is load-bearing.** The code casts the dropdown index directly:
`(RaceType)raceDropdown.value`, and `teamDropdown.value - 1` for the team. Reordering the
options in the Inspector silently changes what the player picks. No labels are matched by text.

Leave either field unassigned and that dimension falls back to its default — `Human`, and
"any corner". Nothing breaks.

### What works right now vs. later

- **Team selection works immediately.** The server honours `RequestedTeamIndex`, falling back to
  the first free corner if the requested one is taken.
- **Race is recorded and replicated** onto `PlayerBase.Race`, but has no visible effect until the
  `RaceCatalog` from steps 3–4 exists. Until then every corner keeps its authored visuals and
  roster.

### Verifying

Pick "Corner 3" on the first client and "Corner 1" on the second. The server log should read:

```
[ServerAcceptGameSystem] Granted Team 2 (race Human) to NetworkId=1 ...
[ServerAcceptGameSystem] Granted Team 0 (race Orc)   to NetworkId=2 ...
```

Note the log prints the zero-based team index, so "Corner 3" appears as `Team 2`. Then have both
clients request the *same* corner — the second should be granted a different one, while still
getting the race it asked for.

---

## Verifying

1. Two clients pick **different races and different corners**. Each base should render its chosen
   race's visuals and spawn that race's roster, regardless of which corner it got.
2. Two clients pick the **same corner**. The second is granted a different one, and its race is
   still honoured — races are not exclusive.
3. Pick a race, then disconnect and rejoin with a different one. The corner's visuals should be
   torn down and rebuilt (`BaseRaceVisualSystem` tracks the spawned race and rebuilds on change).
