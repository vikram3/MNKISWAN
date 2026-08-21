using System.Collections.Generic;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.VisualEffects;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.Actions
{
    [CreateAssetMenu(menuName = "BossRoom/Actions/Healing Feather Action")]
    public class HealingFeatherAction : Action
    {
        bool m_HealApplied;
        List<SpecialFXGraphic> m_SpawnedGraphics;

        public override bool OnStart(ServerCharacter serverCharacter)
        {
            var target = FindMissionHealthTarget();
            if (!target || target.LifeState != LifeState.Alive)
            {
                return ActionConclusion.Stop;
            }

            Data.TargetIds = new[] { target.NetworkObjectId };

            var lookAtPosition = target.physicsWrapper.Transform.position;
            lookAtPosition.y = serverCharacter.physicsWrapper.Transform.position.y;
            if ((lookAtPosition - serverCharacter.physicsWrapper.Transform.position).sqrMagnitude > 0.001f)
            {
                serverCharacter.physicsWrapper.Transform.LookAt(lookAtPosition);
            }

            if (!string.IsNullOrEmpty(Config.Anim))
            {
                serverCharacter.serverAnimationHandler.NetworkAnimator.SetTrigger(Config.Anim);
            }

            serverCharacter.clientCharacter.ClientPlayActionRpc(Data);
            return ActionConclusion.Continue;
        }

        public override bool OnUpdate(ServerCharacter serverCharacter)
        {
            if (!m_HealApplied && TimeRunning >= Config.ExecTimeSeconds)
            {
                m_HealApplied = true;
                var damageable = GetMissionHealthDamageable();
                if (damageable != null)
                {
                    damageable.ReceiveHitPoints(serverCharacter, Config.Amount);
                }
            }

            return ActionConclusion.Continue;
        }

        public override void Reset()
        {
            base.Reset();
            m_HealApplied = false;
            m_SpawnedGraphics = null;
        }

        ServerCharacter FindMissionHealthTarget()
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

        IDamageable GetMissionHealthDamageable()
        {
            if (Data.TargetIds == null || Data.TargetIds.Length == 0)
            {
                return null;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(Data.TargetIds[0], out var targetNetworkObj) ||
                targetNetworkObj == null)
            {
                return null;
            }

            if (PhysicsWrapper.TryGetPhysicsWrapper(Data.TargetIds[0], out var physicsWrapper))
            {
                return physicsWrapper.DamageCollider.GetComponent<IDamageable>();
            }

            return targetNetworkObj.GetComponent<IDamageable>();
        }

        public override bool OnStartClient(ClientCharacter clientCharacter)
        {
            base.OnStartClient(clientCharacter);

            if (Data.TargetIds != null &&
                Data.TargetIds.Length > 0 &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(Data.TargetIds[0], out var targetNetworkObj) &&
                targetNetworkObj != null)
            {
                if (PhysicsWrapper.TryGetPhysicsWrapper(Data.TargetIds[0], out var physicsWrapper))
                {
                    m_SpawnedGraphics = InstantiateSpecialFXGraphics(physicsWrapper.Transform, true);
                }
                else
                {
                    m_SpawnedGraphics = InstantiateSpecialFXGraphics(targetNetworkObj.transform, true);
                }
            }

            return ActionConclusion.Continue;
        }

        public override void CancelClient(ClientCharacter clientCharacter)
        {
            if (m_SpawnedGraphics == null)
            {
                return;
            }

            foreach (var spawnedGraphic in m_SpawnedGraphics)
            {
                if (spawnedGraphic)
                {
                    spawnedGraphic.Shutdown();
                }
            }
        }
    }
}
