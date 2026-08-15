using System.Collections.Generic;
using FourCorners.Scripts.Components.Combat;
using FourCorners.Scripts.Components.Minion;
using FourCorners.Scripts.Components.Spawner;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace FourCorners.Scripts.Controller
{
    /// <summary>
    /// Draws health bars over bases and wounded minions, and flashes a bar when it loses health.
    ///
    /// Combat was invisible without this: a base losing two points out of three hundred looks
    /// exactly like a base at full health, so there was no way to tell whether any of the
    /// simulation was working. This is the cheapest thing that makes it legible.
    ///
    /// Deliberately immediate-mode and asset-free — no bar prefab, no material, no shader
    /// variant, nothing to bake. That keeps it out of the entity scene entirely, which matters
    /// because every baker change costs a blocking re-bake. The proper presentation pass
    /// (billboarded meshes, hit VFX, damage numbers) is still Tier 3.3; this is the stand-in that
    /// lets the gameplay tiers be verified in the meantime.
    ///
    /// Reads <see cref="ClientServerBootstrap.ClientWorld"/> specifically. Health is a GhostField,
    /// so on a client these values are the replicated ones — the same numbers the server is
    /// acting on.
    /// </summary>
    public class CombatFeedbackOverlay : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float minionBarWidth = 30f;
        [SerializeField] private float minionBarHeight = 4f;
        [SerializeField] private float minionHeightOffset = 1.6f;

        [SerializeField] private float baseBarWidth = 90f;
        [SerializeField] private float baseBarHeight = 9f;
        [SerializeField] private float baseHeightOffset = 5f;

        [Header("Behaviour")]
        [Tooltip("Bars stop being drawn past this many, so a pile-up cannot tank the frame rate.")]
        [SerializeField] private int maxBars = 400;

        [Tooltip("Seconds a bar stays lit white after losing health.")]
        [SerializeField] private float flashDuration = 0.15f;

        private struct Bar
        {
            public Vector3 WorldPosition;
            public float Fraction;
            public float Width;
            public float Height;
            public float HeightOffset;
            public bool Flashing;
        }

        /// <summary>Last seen health per entity, so a drop can be detected without server help.</summary>
        private struct HealthMemory
        {
            public int LastHealth;
            public float FlashUntil;
            public int LastSeenFrame;
        }

        private const int PruneIntervalFrames = 300;

        private readonly List<Bar> _bars = new List<Bar>(512);
        private readonly Dictionary<Entity, HealthMemory> _memory = new Dictionary<Entity, HealthMemory>(512);
        private readonly List<Entity> _stale = new List<Entity>(64);

        private World _cachedWorld;
        private EntityQuery _baseQuery;
        private EntityQuery _minionQuery;
        private Camera _camera;

        private void Update()
        {
            _bars.Clear();

            if (!TryResolveWorld()) return;

            _camera = _camera != null ? _camera : Camera.main;
            if (_camera == null) return;

            CollectBases();
            CollectMinions();

            if (Time.frameCount % PruneIntervalFrames == 0) PruneMemory();
        }

        /// <summary>
        /// Rebuilds the queries whenever the client world changes. Queries belong to a world, so a
        /// cached one from a disposed world is a crash waiting to happen.
        /// </summary>
        private bool TryResolveWorld()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world is not { IsCreated: true }) return false;

            if (_cachedWorld == world) return true;

            _cachedWorld = world;
            _baseQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerBase>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalToWorld>());
            _minionQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<MinionData>(),
                ComponentType.ReadOnly<Health>(),
                ComponentType.ReadOnly<LocalToWorld>());

            _memory.Clear();
            return true;
        }

        private void CollectBases()
        {
            using var entities = _baseQuery.ToEntityArray(Allocator.Temp);
            using var health = _baseQuery.ToComponentDataArray<Health>(Allocator.Temp);
            using var transforms = _baseQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            using var bases = _baseQuery.ToComponentDataArray<PlayerBase>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                // An unclaimed corner has nothing to report. A destroyed one keeps its empty bar,
                // which is how you read "that player is out" at a glance.
                bool worthShowing = bases[i].IsActive || health[i].Current < health[i].Max;
                if (!worthShowing) continue;

                AddBar(entities[i], health[i], transforms[i].Position,
                    baseBarWidth, baseBarHeight, baseHeightOffset);
            }
        }

        private void CollectMinions()
        {
            using var entities = _minionQuery.ToEntityArray(Allocator.Temp);
            using var health = _minionQuery.ToComponentDataArray<Health>(Allocator.Temp);
            using var transforms = _minionQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                // Only the wounded. A bar over every healthy minion is noise, and the ones that
                // matter are exactly the ones in a fight.
                if (health[i].Current >= health[i].Max) continue;

                AddBar(entities[i], health[i], transforms[i].Position,
                    minionBarWidth, minionBarHeight, minionHeightOffset);
            }
        }

        private void AddBar(Entity entity, Health health, Vector3 position,
            float width, float height, float heightOffset)
        {
            if (_bars.Count >= maxBars) return;
            if (health.Max <= 0) return;

            bool flashing = RegisterHealth(entity, health.Current);

            _bars.Add(new Bar
            {
                WorldPosition = position,
                Fraction = Mathf.Clamp01(health.Current / (float)health.Max),
                Width = width,
                Height = height,
                HeightOffset = heightOffset,
                Flashing = flashing
            });
        }

        /// <summary>Returns true while this entity should be drawn as freshly hit.</summary>
        private bool RegisterHealth(Entity entity, int current)
        {
            float now = Time.time;

            if (_memory.TryGetValue(entity, out var memory))
            {
                if (current < memory.LastHealth) memory.FlashUntil = now + flashDuration;
                memory.LastHealth = current;
            }
            else
            {
                memory = new HealthMemory { LastHealth = current, FlashUntil = 0f };
            }

            memory.LastSeenFrame = Time.frameCount;
            _memory[entity] = memory;

            return now < memory.FlashUntil;
        }

        /// <summary>Entities die constantly, so the memory has to be swept or it grows forever.</summary>
        private void PruneMemory()
        {
            _stale.Clear();
            int cutoff = Time.frameCount - PruneIntervalFrames;

            foreach (var pair in _memory)
            {
                if (pair.Value.LastSeenFrame < cutoff) _stale.Add(pair.Key);
            }

            foreach (var entity in _stale) _memory.Remove(entity);
        }

        private void OnGUI()
        {
            // OnGUI runs several times a frame; only the repaint pass draws anything.
            if (Event.current.type != EventType.Repaint) return;
            if (_camera == null || _bars.Count == 0) return;

            var previousColor = GUI.color;

            foreach (var bar in _bars)
            {
                var screen = _camera.WorldToScreenPoint(bar.WorldPosition + Vector3.up * bar.HeightOffset);

                // Behind the camera projects to a mirrored on-screen point — draw it and bars
                // appear over things that are not there.
                if (screen.z <= 0f) continue;
                if (screen.x < -bar.Width || screen.x > Screen.width + bar.Width) continue;
                if (screen.y < -bar.Height || screen.y > Screen.height + bar.Height) continue;

                float x = screen.x - bar.Width * 0.5f;
                float y = Screen.height - screen.y;

                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x, y, bar.Width, bar.Height), Texture2D.whiteTexture);

                GUI.color = bar.Flashing ? Color.white : FillColour(bar.Fraction);
                GUI.DrawTexture(new Rect(x, y, bar.Width * bar.Fraction, bar.Height), Texture2D.whiteTexture);
            }

            GUI.color = previousColor;
        }

        private static Color FillColour(float fraction)
        {
            // Green through yellow to red, so a corner in trouble reads at a glance.
            return fraction > 0.5f
                ? Color.Lerp(Color.yellow, Color.green, (fraction - 0.5f) * 2f)
                : Color.Lerp(Color.red, Color.yellow, fraction * 2f);
        }
    }
}
