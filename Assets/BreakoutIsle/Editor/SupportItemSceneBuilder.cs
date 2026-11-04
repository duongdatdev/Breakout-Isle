using System.Collections.Generic;
using BreakoutIsle.Gameplay;
using UnityEditor;
using UnityEngine;

namespace BreakoutIsle.Editor
{
    public static class SupportItemSceneBuilder
    {
        private const string Root = "Assets/BreakoutIsle";
        private const string ProjectilePath = Root + "/Prefabs/Gameplay/SupportProjectile.prefab";
        private const string ArcMaterialPath = Root + "/Materials/ThrowArc.mat";
        private const string IconPath = Root + "/Art/UI/Icons/";

        public static SupportProjectile BuildProjectilePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
            if (existing != null)
            {
                var component = existing.GetComponent<SupportProjectile>();
                if (component == null) throw new System.InvalidOperationException("SupportProjectile prefab has no SupportProjectile component.");
                return component;
            }
            EnsureFolder(Root + "/Prefabs/Gameplay");
            var source = new GameObject("Support Projectile", typeof(SpriteRenderer), typeof(SupportProjectile));
            var renderer = source.GetComponent<SpriteRenderer>();
            renderer.sprite = SpriteAt(IconPath + "stone.png");
            renderer.sortingLayerName = "Actors";
            renderer.sortingOrder = 80;
            Set(source.GetComponent<SupportProjectile>(), "spriteRenderer", renderer);
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, ProjectilePath);
            Object.DestroyImmediate(source);
            var projectile = prefab != null ? prefab.GetComponent<SupportProjectile>() : null;
            if (projectile == null) throw new System.InvalidOperationException("Could not create SupportProjectile prefab.");
            return projectile;
        }

        public static SupportItemController ConfigurePlayer(GameObject player, LevelSession session, Camera camera,
            LayerMask obstacleMask, SupportProjectile projectile)
        {
            var controller = player.AddComponent<SupportItemController>();
            var arcObject = new GameObject("Support Item Aim Arc");
            arcObject.transform.SetParent(player.transform, false);
            var aimLine = arcObject.AddComponent<LineRenderer>();
            aimLine.useWorldSpace = true;
            aimLine.positionCount = 16;
            aimLine.startWidth = aimLine.endWidth = 0.055f;
            aimLine.startColor = aimLine.endColor = new Color(0.96f, 0.82f, 0.32f, 0.92f);
            aimLine.sortingLayerName = "Actors";
            aimLine.sortingOrder = 90;
            aimLine.sharedMaterial = GetArcMaterial();

            var landing = new GameObject("Support Landing Shadow");
            var ring = landing.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 17;
            ring.startWidth = ring.endWidth = 0.055f;
            ring.startColor = ring.endColor = new Color(0.95f, 0.77f, 0.28f, 0.9f);
            ring.sortingLayerName = "Actors";
            ring.sortingOrder = 89;
            ring.sharedMaterial = aimLine.sharedMaterial;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = Mathf.PI * 2f * i / (ring.positionCount - 1);
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.3f, Mathf.Sin(angle) * 0.3f, 0f));
            }
            landing.SetActive(false);

            Set(controller, "inputReader", player.GetComponent<PlayerInputReader>());
            Set(controller, "session", session);
            Set(controller, "motor", player.GetComponent<PlayerMotor>());
            Set(controller, "worldCamera", camera);
            Set(controller, "projectilePrefab", projectile);
            Set(controller, "aimLine", aimLine);
            Set(controller, "landingMarker", landing);
            Set(controller, "obstacleMask", obstacleMask.value);
            SetProfiles(controller, DefaultProfiles());
            return controller;
        }

        public static SupportItemProfile[] DefaultProfiles()
        {
            return new[]
            {
                Profile("stone", "stone", SupportNoisePolicy.NearestGuard, 6f),
                Profile("tin_can", "tin_can", SupportNoisePolicy.NearestGuard, 6f),
                Profile("glass_bottle", "glass_bottle", SupportNoisePolicy.AllGuards, 10f),
                Profile("firecracker", "firecracker", SupportNoisePolicy.AllGuards, 12f, 3f),
                Profile("meat", "meat", SupportNoisePolicy.None, 0f, 0f, true)
            };
        }

        private static SupportItemProfile Profile(string id, string icon, SupportNoisePolicy policy, float radius, float fuse = 0f, bool attractsDogs = false)
        {
            return new SupportItemProfile
            {
                itemId = id,
                projectileSprite = SpriteAt(IconPath + icon + ".png"),
                noisePolicy = policy,
                noiseRadius = radius,
                fuseSeconds = fuse,
                attractsDogs = attractsDogs
            };
        }

        private static void SetProfiles(SupportItemController controller, SupportItemProfile[] profiles)
        {
            var serialized = new SerializedObject(controller);
            var array = serialized.FindProperty("profiles");
            array.arraySize = profiles.Length;
            for (var i = 0; i < profiles.Length; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("itemId").stringValue = profiles[i].itemId;
                entry.FindPropertyRelative("projectileSprite").objectReferenceValue = profiles[i].projectileSprite;
                entry.FindPropertyRelative("noisePolicy").enumValueIndex = (int)profiles[i].noisePolicy;
                entry.FindPropertyRelative("noiseRadius").floatValue = profiles[i].noiseRadius;
                entry.FindPropertyRelative("fuseSeconds").floatValue = profiles[i].fuseSeconds;
                entry.FindPropertyRelative("attractsDogs").boolValue = profiles[i].attractsDogs;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material GetArcMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArcMaterialPath);
            if (material != null) return material;
            EnsureFolder(Root + "/Materials");
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new System.InvalidOperationException("Sprites/Default shader is unavailable for throw arc.");
            material = new Material(shader) { name = "Throw Arc" };
            AssetDatabase.CreateAsset(material, ArcMaterialPath);
            return material;
        }

        private static Sprite SpriteAt(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(path))
                if (item is Sprite loaded) return loaded;
            throw new System.InvalidOperationException("Missing support item icon sprite: " + path);
        }

        private static void Set(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new System.InvalidOperationException("Missing serialized field " + property + " on " + target.GetType().Name);
            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string property, int value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (field == null) throw new System.InvalidOperationException("Missing serialized field " + property + " on " + target.GetType().Name);
            field.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }

}
