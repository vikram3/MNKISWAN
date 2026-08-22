using System;
using System.Collections;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.BossRoom.VisualEffects;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Minimal prototype-only visual feedback that reuses existing Boss Room VFX prefabs.
    /// </summary>
    public class PrototypeVFXFeedback : MonoBehaviour
    {
        const float k_SelectionPulseInterval = 1.0f;
        const float k_PriorityPulseInterval = 1.2f;
        const float k_CriticalPulseInterval = 2.5f;
        const float k_CommandPulseLifetime = 1.5f;
        const float k_OneShotLifetime = 2.0f;
        const float k_CriticalHealthPercent = 0.35f;

        [SerializeField]
        GameObject m_MonkeyAttackFX;

        [SerializeField]
        GameObject m_SwanSupportFX;

        [SerializeField]
        GameObject m_ArmyCommandFX;

        [SerializeField]
        GameObject m_SelectedArmyUnitFX;

        [SerializeField]
        GameObject m_EnemyPriorityTargetFX;

        [SerializeField]
        GameObject m_MonkeyCriticalHealthFX;

        [SerializeField]
        GameObject m_CommanderDefeatFX;

        ServerCharacter m_LocalCharacter;
        ClientInputSender m_LocalInputSender;
        string m_LastSwanCommand = "None";
        bool m_HasSeenCommander;
        bool m_CommanderWasAlive;
        float m_NextSelectionPulseTime;
        float m_NextPriorityPulseTime;
        float m_NextCriticalPulseTime;

        void Awake()
        {
            ClientPlayerAvatar.LocalClientSpawned += OnLocalClientSpawned;
            ClientPlayerAvatar.LocalClientDespawned += OnLocalClientDespawned;
        }

        void OnDestroy()
        {
            ClientPlayerAvatar.LocalClientSpawned -= OnLocalClientSpawned;
            ClientPlayerAvatar.LocalClientDespawned -= OnLocalClientDespawned;
            UnregisterInputSender();
        }

        void OnLocalClientSpawned(ClientPlayerAvatar avatar)
        {
            UnregisterInputSender();
            m_LocalCharacter = avatar.GetComponent<ServerCharacter>();
            m_LocalInputSender = avatar.GetComponent<ClientInputSender>();

            if (m_LocalInputSender)
            {
                m_LocalInputSender.ActionInputEvent += OnLocalActionInput;
                m_LastSwanCommand = m_LocalInputSender.CurrentSwanArmyCommand;
            }
        }

        void OnLocalClientDespawned()
        {
            UnregisterInputSender();
            m_LocalCharacter = null;
        }

        void UnregisterInputSender()
        {
            if (m_LocalInputSender)
            {
                m_LocalInputSender.ActionInputEvent -= OnLocalActionInput;
                m_LocalInputSender = null;
            }
        }

        void Update()
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            {
                return;
            }

            UpdateSwanCommandFX();
            PulseSwanSelectionFX();
            PulsePriorityTargetFX();
            PulseMonkeyCriticalHealthFX();
            PlayCommanderDefeatFX();
        }

        void OnLocalActionInput(ActionRequestData action)
        {
            if (!m_LocalCharacter)
            {
                return;
            }

            var displayedName = m_LocalCharacter.CharacterClass.DisplayedName;
            if (displayedName == "MONKEY KING" &&
                m_LocalCharacter.CharacterClass.Skill1 &&
                action.ActionID == m_LocalCharacter.CharacterClass.Skill1.ActionID)
            {
                SpawnFX(m_MonkeyAttackFX, GetVisualAnchor(m_LocalCharacter), Vector3.forward * 0.8f, k_OneShotLifetime, false);
            }
            else if (displayedName == "SWAN PRINCESS" &&
                m_LocalCharacter.CharacterClass.Skill2 &&
                action.ActionID == m_LocalCharacter.CharacterClass.Skill2.ActionID)
            {
                SpawnFX(m_SwanSupportFX, GetVisualAnchor(FindMissionHealthCharacter()), Vector3.up * 0.5f, k_OneShotLifetime, true);
            }
        }

        void UpdateSwanCommandFX()
        {
            if (!m_LocalInputSender ||
                !m_LocalCharacter ||
                m_LocalCharacter.CharacterClass.DisplayedName != "SWAN PRINCESS")
            {
                return;
            }

            var command = m_LocalInputSender.CurrentSwanArmyCommand;
            if (string.Equals(command, m_LastSwanCommand, StringComparison.Ordinal))
            {
                return;
            }

            m_LastSwanCommand = command;
            if (string.IsNullOrEmpty(command) || command == "None")
            {
                return;
            }

            foreach (var unit in GetSelectedSwanUnits())
            {
                SpawnFX(m_ArmyCommandFX, GetVisualAnchor(unit), Vector3.up * 0.25f, k_CommandPulseLifetime, true);
            }
        }

        void PulseSwanSelectionFX()
        {
            if (Time.unscaledTime < m_NextSelectionPulseTime ||
                !m_LocalInputSender ||
                !m_LocalCharacter ||
                m_LocalCharacter.CharacterClass.DisplayedName != "SWAN PRINCESS")
            {
                return;
            }

            m_NextSelectionPulseTime = Time.unscaledTime + k_SelectionPulseInterval;
            foreach (var unit in GetSelectedSwanUnits())
            {
                SpawnFX(m_SelectedArmyUnitFX, GetVisualAnchor(unit), Vector3.up * 0.1f, k_SelectionPulseInterval, true);
            }
        }

        void PulsePriorityTargetFX()
        {
            if (Time.unscaledTime < m_NextPriorityPulseTime)
            {
                return;
            }

            m_NextPriorityPulseTime = Time.unscaledTime + k_PriorityPulseInterval;
            var target = FindPriorityTarget(FindBoss());
            if (target)
            {
                SpawnFX(m_EnemyPriorityTargetFX, GetVisualAnchor(target), Vector3.up * 0.2f, k_PriorityPulseInterval, true);
            }
        }

        void PulseMonkeyCriticalHealthFX()
        {
            if (Time.unscaledTime < m_NextCriticalPulseTime)
            {
                return;
            }

            var monkey = FindMissionHealthCharacter();
            if (!monkey || monkey.LifeState != LifeState.Alive)
            {
                return;
            }

            var maxHp = Mathf.Max(1, monkey.CharacterClass.BaseHP.Value);
            if (monkey.HitPoints / (float)maxHp > k_CriticalHealthPercent)
            {
                return;
            }

            m_NextCriticalPulseTime = Time.unscaledTime + k_CriticalPulseInterval;
            SpawnFX(m_MonkeyCriticalHealthFX, GetVisualAnchor(monkey), Vector3.up * 1.0f, k_OneShotLifetime, true);
        }

        void PlayCommanderDefeatFX()
        {
            var commander = FindBoss();
            if (!commander)
            {
                return;
            }

            var commanderAlive = commander.LifeState == LifeState.Alive;
            if (!m_HasSeenCommander)
            {
                m_HasSeenCommander = true;
                m_CommanderWasAlive = commanderAlive;
                return;
            }

            if (m_CommanderWasAlive && !commanderAlive)
            {
                SpawnFX(m_CommanderDefeatFX, GetVisualAnchor(commander), Vector3.up * 0.5f, k_OneShotLifetime, true);
            }

            m_CommanderWasAlive = commanderAlive;
        }

        IEnumerable<ServerCharacter> GetSelectedSwanUnits()
        {
            if (!m_LocalInputSender)
            {
                yield break;
            }

            if (m_LocalInputSender.IsSwanArmyGroupSelected)
            {
                foreach (var unit in GetAllLiveSwanUnits())
                {
                    yield return unit;
                }

                yield break;
            }

            if (m_LocalInputSender.SelectedSwanArmyUnitId != 0 &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(m_LocalInputSender.SelectedSwanArmyUnitId, out var selectedObject) &&
                selectedObject.TryGetComponent(out ServerCharacter selectedCharacter) &&
                IsLiveSwanTacticalUnit(selectedCharacter))
            {
                yield return selectedCharacter;
            }
        }

        IEnumerable<ServerCharacter> GetAllLiveSwanUnits()
        {
            foreach (var spawnedObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
            {
                if (spawnedObject &&
                    spawnedObject.TryGetComponent(out ServerCharacter character) &&
                    IsLiveSwanTacticalUnit(character))
                {
                    yield return character;
                }
            }
        }

        static bool IsLiveSwanTacticalUnit(ServerCharacter character)
        {
            return character &&
                character.LifeState == LifeState.Alive &&
                character.TryGetComponent<SwanTacticalUnit>(out _);
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

        ServerCharacter FindClosestLiveEnemy()
        {
            var origin = m_LocalCharacter ? GetVisualAnchor(m_LocalCharacter).position : Vector3.zero;
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

                var anchor = GetVisualAnchor(character);
                var distance = (anchor.position - origin).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = character;
                }
            }

            return closestEnemy;
        }

        static Transform GetVisualAnchor(ServerCharacter character)
        {
            if (!character)
            {
                return null;
            }

            if (character.clientCharacter)
            {
                return character.clientCharacter.transform;
            }

            return character.physicsWrapper ? character.physicsWrapper.Transform : character.transform;
        }

        void SpawnFX(GameObject prefab, Transform anchor, Vector3 localOffset, float lifetime, bool followAnchor)
        {
            if (!prefab || !anchor)
            {
                return;
            }

            var fx = Instantiate(prefab, anchor.position, anchor.rotation, followAnchor ? anchor : null);
            fx.transform.localPosition = followAnchor ? localOffset : fx.transform.localPosition + localOffset;

            if (fx.TryGetComponent(out SpecialFXGraphic specialFX))
            {
                StartCoroutine(ShutdownFXAfterDelay(specialFX, lifetime));
            }
            else
            {
                Destroy(fx, lifetime);
            }
        }

        static IEnumerator ShutdownFXAfterDelay(SpecialFXGraphic specialFX, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (specialFX)
            {
                specialFX.Shutdown();
                Destroy(specialFX.gameObject, 1.0f);
            }
        }
    }
}
