using System;
using TMPro;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using BossAction = Unity.BossRoom.Gameplay.Actions.Action;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Minimal prototype readout layered into the existing Boss Room HUD.
    /// </summary>
    public class PrototypeMissionHUD : MonoBehaviour
    {
        const float k_UpdateInterval = 0.25f;
        const float k_DangerHealthPercent = 0.35f;

        TextMeshProUGUI m_MissionText;
        TextMeshProUGUI m_MonkeyText;
        TextMeshProUGUI m_BossText;
        TextMeshProUGUI m_EnemyText;
        TextMeshProUGUI m_SwanText;

        ServerCharacter m_LocalCharacter;
        ClientInputSender m_LocalInputSender;
        float m_NextUpdateTime;

        void Awake()
        {
            CreateHudPanel();
            ClientPlayerAvatar.LocalClientSpawned += OnLocalClientSpawned;
            ClientPlayerAvatar.LocalClientDespawned += OnLocalClientDespawned;
        }

        void OnDestroy()
        {
            ClientPlayerAvatar.LocalClientSpawned -= OnLocalClientSpawned;
            ClientPlayerAvatar.LocalClientDespawned -= OnLocalClientDespawned;
        }

        void OnLocalClientSpawned(ClientPlayerAvatar avatar)
        {
            m_LocalCharacter = avatar.GetComponent<ServerCharacter>();
            m_LocalInputSender = avatar.GetComponent<ClientInputSender>();
        }

        void OnLocalClientDespawned()
        {
            m_LocalCharacter = null;
            m_LocalInputSender = null;
        }

        void Update()
        {
            if (Time.unscaledTime < m_NextUpdateTime)
            {
                return;
            }

            m_NextUpdateTime = Time.unscaledTime + k_UpdateInterval;
            Refresh();
        }

        void CreateHudPanel()
        {
            var panel = new GameObject("Prototype Mission HUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(transform, false);
            panel.layer = gameObject.layer;

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(430f, 230f);

            var image = panel.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.52f);
            image.raycastTarget = false;

            m_MissionText = CreateLine(panel.transform, "Mission", 0, 18, FontStyles.Bold);
            m_MonkeyText = CreateLine(panel.transform, "Monkey Health", 1, 16, FontStyles.Normal);
            m_BossText = CreateLine(panel.transform, "Commander Health", 2, 16, FontStyles.Normal);
            m_EnemyText = CreateLine(panel.transform, "Enemy Info", 3, 16, FontStyles.Normal);
            m_SwanText = CreateLine(panel.transform, "Swan Tactical", 4, 15, FontStyles.Normal);
        }

        static TextMeshProUGUI CreateLine(Transform parent, string name, int line, int fontSize, FontStyles fontStyle)
        {
            var lineObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lineObject.transform.SetParent(parent, false);
            lineObject.layer = parent.gameObject.layer;

            var rect = lineObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f - (line * 36f));
            rect.sizeDelta = new Vector2(-24f, line == 4 ? 58f : 30f);

            var text = lineObject.GetComponent<TextMeshProUGUI>();
            text.color = Color.white;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        void Refresh()
        {
            var monkey = FindMissionHealthCharacter();
            var boss = FindBoss();
            var priorityTarget = FindPriorityTarget(boss);

            SetMissionText(boss);
            SetHealthText(m_MonkeyText, "Monkey", monkey, true);
            SetHealthText(m_BossText, "Commander", boss, false);
            SetEnemyText(boss, priorityTarget);
            SetSwanText(priorityTarget);
        }

        void SetMissionText(ServerCharacter boss)
        {
            var bossAlive = boss && boss.LifeState == LifeState.Alive;
            m_MissionText.text = bossAlive
                ? "OBJECTIVE: Defeat the enemy commander"
                : "OBJECTIVE: Kingdom reclaimed";
        }

        void SetHealthText(TextMeshProUGUI label, string displayName, ServerCharacter character, bool showDanger)
        {
            if (!character)
            {
                label.text = $"{displayName}: locating...";
                label.color = Color.white;
                return;
            }

            var maxHp = Mathf.Max(1, character.CharacterClass.BaseHP.Value);
            var hp = Mathf.Max(0, character.HitPoints);
            var percent = hp / (float)maxHp;
            var danger = showDanger && percent <= k_DangerHealthPercent && character.LifeState == LifeState.Alive;
            label.text = danger
                ? $"{displayName}: {hp}/{maxHp} - DANGER"
                : $"{displayName}: {hp}/{maxHp}";
            label.color = danger ? new Color(1f, 0.45f, 0.35f) : Color.white;
        }

        void SetEnemyText(ServerCharacter boss, ServerCharacter priorityTarget)
        {
            var liveEnemies = CountLiveEnemies();
            var bossState = boss && boss.LifeState == LifeState.Alive ? "Commander active" : "Commander defeated";
            var priorityName = priorityTarget ? GetEnemyDisplayName(priorityTarget) : "none";
            m_EnemyText.text = $"Enemies: {liveEnemies} | {bossState} | Priority: {priorityName}";
        }

        void SetSwanText(ServerCharacter priorityTarget)
        {
            if (!m_LocalCharacter || m_LocalCharacter.CharacterClass.DisplayedName != "SWAN PRINCESS")
            {
                m_SwanText.text = "Tactical: Swan command HUD appears for Player 2";
                return;
            }

            var selected = "none";
            if (m_LocalInputSender)
            {
                if (m_LocalInputSender.IsSwanArmyGroupSelected)
                {
                    selected = "group";
                }
                else if (m_LocalInputSender.SelectedSwanArmyUnitId != 0 &&
                    NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(m_LocalInputSender.SelectedSwanArmyUnitId, out var selectedObject))
                {
                    selected = selectedObject.name.Replace("(Clone)", string.Empty);
                }
            }

            var command = m_LocalInputSender ? m_LocalInputSender.CurrentSwanArmyCommand : "None";
            var priorityName = priorityTarget ? GetEnemyDisplayName(priorityTarget) : "none";
            m_SwanText.text = $"Swan: selected {selected} | command {command}\nCDs: {GetAbilityCooldowns(m_LocalCharacter)} | priority {priorityName}";
        }

        ServerCharacter FindMissionHealthCharacter()
        {
            foreach (var player in PlayerServerCharacter.GetPlayerServerCharacters())
            {
                if (player && player.CharacterClass.IsMissionHealth)
                {
                    return player;
                }
            }

            return null;
        }

        ServerCharacter FindBoss()
        {
            if (NetworkManager.Singleton == null)
            {
                return null;
            }

            foreach (var spawnedObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (spawnedObject &&
                    spawnedObject.TryGetComponent(out ServerCharacter character) &&
                    character.CharacterType == CharacterTypeEnum.ImpBoss)
                {
                    return character;
                }
            }

            return null;
        }

        ServerCharacter FindPriorityTarget(ServerCharacter boss)
        {
            if (m_LocalCharacter &&
                m_LocalCharacter.TargetId.Value != 0 &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(m_LocalCharacter.TargetId.Value, out var targetObject) &&
                targetObject.TryGetComponent(out ServerCharacter targetCharacter) &&
                targetCharacter.IsNpc &&
                targetCharacter.LifeState == LifeState.Alive)
            {
                return targetCharacter;
            }

            if (boss && boss.LifeState == LifeState.Alive)
            {
                return boss;
            }

            return FindClosestLiveEnemy();
        }

        ServerCharacter FindClosestLiveEnemy()
        {
            if (NetworkManager.Singleton == null)
            {
                return null;
            }

            var origin = m_LocalCharacter ? m_LocalCharacter.physicsWrapper.Transform.position : Vector3.zero;
            var closestDistance = float.MaxValue;
            ServerCharacter closestEnemy = null;

            foreach (var spawnedObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (!spawnedObject ||
                    !spawnedObject.TryGetComponent(out ServerCharacter character) ||
                    !character.IsNpc ||
                    character.LifeState != LifeState.Alive)
                {
                    continue;
                }

                var distance = (character.physicsWrapper.Transform.position - origin).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = character;
                }
            }

            return closestEnemy;
        }

        int CountLiveEnemies()
        {
            if (NetworkManager.Singleton == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var spawnedObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (spawnedObject &&
                    spawnedObject.TryGetComponent(out ServerCharacter character) &&
                    character.IsNpc &&
                    character.LifeState == LifeState.Alive)
                {
                    count++;
                }
            }

            return count;
        }

        static string GetEnemyDisplayName(ServerCharacter character)
        {
            return character.CharacterType == CharacterTypeEnum.ImpBoss ? "Commander" : character.name.Replace("(Clone)", string.Empty);
        }

        static string GetAbilityCooldowns(ServerCharacter character)
        {
            if (!character)
            {
                return "action bar";
            }

            return $"{GetCooldown(character.CharacterClass.Skill1)}/{GetCooldown(character.CharacterClass.Skill2)}/{GetCooldown(character.CharacterClass.Skill3)}";
        }

        static string GetCooldown(BossAction action)
        {
            if (!action)
            {
                return "-";
            }

            return $"{Mathf.RoundToInt(action.Config.ReuseTimeSeconds)}s";
        }
    }
}
