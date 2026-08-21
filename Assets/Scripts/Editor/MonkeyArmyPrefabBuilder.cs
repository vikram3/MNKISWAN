using System.IO;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

public static class MonkeyArmyPrefabBuilder
{
    const string k_OutputRoot = "Assets/Prefabs/Character/MonkeyArmy";
    const string k_ClassOutputRoot = "Assets/GameData/Character/MonkeyArmy";
    const string k_EnemyPrefabPath = "Assets/Prefabs/Character/Enemy.prefab";
    const string k_BossRoomStatePrefabPath = "Assets/Prefabs/State/BossRoomState.prefab";
    const string k_NetworkPrefabsPath = "Assets/GameData/NetworkPrefabs.asset";
    const string k_VisualizationConfigurationGuid = "9504973cdecd65749889771972fa0117";
    const string k_AnimatorControllerPath = "Assets/Models/Animation Controllers/CharacterSetController.controller";
    const string k_CharacterSetAvatarGuid = "2115c4661f55eff45a5a0f91fc0a12f0";

    static readonly UnitDefinition[] k_Units =
    {
        new UnitDefinition("Tank", "Assets/GameData/Character/Tank/Tank.asset",
            "Assets/Prefabs/CharGFX/CharacterGraphics/PlayerGraphics_Tank_Boy.prefab", new Vector3(-2f, 0f, -2.5f)),
        new UnitDefinition("Archer", "Assets/GameData/Character/Archer/Archer.asset",
            "Assets/Prefabs/CharGFX/CharacterGraphics/PlayerGraphics_Archer_Boy.prefab", new Vector3(2f, 0f, -2.5f)),
        new UnitDefinition("Mage", "Assets/GameData/Character/Mage/Mage.asset",
            "Assets/Prefabs/CharGFX/CharacterGraphics/PlayerGraphics_Mage_Boy.prefab", new Vector3(-3f, 0f, -4.25f)),
        new UnitDefinition("Rogue", "Assets/GameData/Character/Rogue/Rogue.asset",
            "Assets/Prefabs/CharGFX/CharacterGraphics/PlayerGraphics_Rogue_Boy.prefab", new Vector3(3f, 0f, -4.25f)),
    };

    [MenuItem("Boss Room/Monkey Army/Rebuild Monkey Followers")]
    public static void RebuildMonkeyFollowers()
    {
        Directory.CreateDirectory(k_OutputRoot);
        Directory.CreateDirectory(k_ClassOutputRoot);

        var createdPrefabs = new NetworkObject[k_Units.Length];
        for (int i = 0; i < k_Units.Length; i++)
        {
            createdPrefabs[i] = BuildUnit(k_Units[i]);
        }

        RegisterNetworkPrefabs(createdPrefabs);
        AssignBossRoomStateFollowers(createdPrefabs);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static NetworkObject BuildUnit(UnitDefinition unit)
    {
        var classAsset = BuildClassAsset(unit);
        var prefabPath = $"{k_OutputRoot}/MonkeyArmy_{unit.Name}.prefab";

        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_EnemyPrefabPath);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
        root.name = $"MonkeyArmy_{unit.Name}";

        var graphicsRoot = new GameObject("PlayerGraphics");
        graphicsRoot.transform.SetParent(root.transform, false);
        SetLayerRecursively(graphicsRoot, root.layer);

        var animator = graphicsRoot.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(k_AnimatorControllerPath);
        animator.avatar = AssetDatabase.LoadAssetAtPath<UnityEngine.Avatar>(AssetDatabase.GUIDToAssetPath(k_CharacterSetAvatarGuid));

        var clientCharacter = graphicsRoot.AddComponent<ClientCharacter>();
        SetObjectReference(clientCharacter, "m_ClientVisualsAnimator", animator);
        SetObjectReference(clientCharacter, "m_VisualizationConfiguration",
            AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(k_VisualizationConfigurationGuid)));

        var networkAnimator = graphicsRoot.AddComponent<NetworkAnimator>();
        SetObjectReference(networkAnimator, "m_Animator", animator);

        var graphicsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(unit.GraphicsPath);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(graphicsPrefab, graphicsRoot.transform);
        model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;
        SetLayerRecursively(model, root.layer);
        AssignNestedAnimatorReferences(model, animator);

        var serverCharacter = root.GetComponent<ServerCharacter>();
        SetObjectReference(serverCharacter, "m_ClientCharacter", clientCharacter);
        SetObjectReference(serverCharacter, "m_CharacterClass", classAsset);
        SetBool(serverCharacter, "m_BrainEnabled", false);

        var animationHandler = root.GetComponent<ServerAnimationHandler>();
        SetObjectReference(animationHandler, "m_NetworkAnimator", networkAnimator);

        var follower = root.GetComponent<MonkeyArmyFollower>();
        if (!follower)
        {
            follower = root.AddComponent<MonkeyArmyFollower>();
        }

        SetObjectReference(follower, "m_ServerCharacter", serverCharacter);
        SetVector3(follower, "m_FollowOffset", unit.FollowOffset);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<NetworkObject>();
    }

    static CharacterClass BuildClassAsset(UnitDefinition unit)
    {
        var outputPath = $"{k_ClassOutputRoot}/MonkeyArmy_{unit.Name}.asset";
        if (!File.Exists(outputPath))
        {
            AssetDatabase.CopyAsset(unit.ClassPath, outputPath);
        }

        var classAsset = AssetDatabase.LoadAssetAtPath<CharacterClass>(outputPath);
        classAsset.IsNpc = true;
        classAsset.DetectRange = 0;
        classAsset.DisplayedName = $"MONKEY ARMY {unit.Name.ToUpperInvariant()}";
        EditorUtility.SetDirty(classAsset);
        return classAsset;
    }

    static void RegisterNetworkPrefabs(NetworkObject[] prefabs)
    {
        var networkPrefabs = AssetDatabase.LoadAssetAtPath<Object>(k_NetworkPrefabsPath);
        var serializedObject = new SerializedObject(networkPrefabs);
        var list = serializedObject.FindProperty("List");
        RemoveEmptyNetworkPrefabEntries(list);

        foreach (var prefab in prefabs)
        {
            if (!ContainsPrefab(list, prefab))
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("Override").boolValue = false;
                entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab.gameObject;
                entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
                entry.FindPropertyRelative("SourceHashToOverride").ulongValue = 0;
                entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
            }
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(networkPrefabs);
    }

    static void RemoveEmptyNetworkPrefabEntries(SerializedProperty list)
    {
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == null)
            {
                list.DeleteArrayElementAtIndex(i);
            }
        }
    }

    static void AssignBossRoomStateFollowers(NetworkObject[] prefabs)
    {
        var root = PrefabUtility.LoadPrefabContents(k_BossRoomStatePrefabPath);
        var state = FindComponent(root, "Unity.BossRoom.Gameplay.GameState.ServerBossRoomState");
        var serializedObject = new SerializedObject(state);
        var followerPrefabs = serializedObject.FindProperty("m_MonkeyFollowerPrefabs");
        followerPrefabs.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
        {
            followerPrefabs.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        }

        serializedObject.ApplyModifiedProperties();
        PrefabUtility.SaveAsPrefabAsset(root, k_BossRoomStatePrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static Component FindComponent(GameObject root, string fullTypeName)
    {
        foreach (var component in root.GetComponents<Component>())
        {
            if (component && component.GetType().FullName == fullTypeName)
            {
                return component;
            }
        }

        throw new MissingComponentException($"Could not find component {fullTypeName} on {root.name}.");
    }

    static bool ContainsPrefab(SerializedProperty list, NetworkObject prefab)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab.gameObject)
            {
                return true;
            }
        }

        return false;
    }

    static void SetObjectReference(Object target, string propertyName, Object value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
    }

    static void SetBool(Object target, string propertyName, bool value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).boolValue = value;
        serializedObject.ApplyModifiedProperties();
    }

    static void SetVector3(Object target, string propertyName, Vector3 value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).vector3Value = value;
        serializedObject.ApplyModifiedProperties();
    }

    static void AssignNestedAnimatorReferences(GameObject root, Animator animator)
    {
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!behaviour)
            {
                continue;
            }

            var serializedObject = new SerializedObject(behaviour);
            var animatorProperty = serializedObject.FindProperty("m_Animator");
            if (animatorProperty != null)
            {
                animatorProperty.objectReferenceValue = animator;
                serializedObject.ApplyModifiedProperties();
            }
        }
    }

    static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    readonly struct UnitDefinition
    {
        public UnitDefinition(string name, string classPath, string graphicsPath, Vector3 followOffset)
        {
            Name = name;
            ClassPath = classPath;
            GraphicsPath = graphicsPath;
            FollowOffset = followOffset;
        }

        public readonly string Name;
        public readonly string ClassPath;
        public readonly string GraphicsPath;
        public readonly Vector3 FollowOffset;
    }
}
