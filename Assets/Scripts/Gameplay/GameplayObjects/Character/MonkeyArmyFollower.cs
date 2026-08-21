using Unity.Netcode;
using Unity.BossRoom.Gameplay.Actions;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// Server-side follower movement for the first Monkey army prototype.
    /// </summary>
    public class MonkeyArmyFollower : NetworkBehaviour
    {
        [SerializeField]
        ServerCharacter m_ServerCharacter;

        [SerializeField]
        Vector3 m_FollowOffset = new Vector3(0, 0, -2.5f);

        [SerializeField]
        float m_UpdateIntervalSeconds = 0.35f;

        [SerializeField]
        float m_RepathDistance = 0.75f;

        [SerializeField]
        bool m_StartFollowingOnSpawn = true;

        float m_NextUpdateTime;
        bool m_IsFollowing;
        ulong m_AttackTargetId;

        public Vector3 FollowOffset
        {
            get => m_FollowOffset;
            set => m_FollowOffset = value;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                return;
            }

            if (!m_ServerCharacter)
            {
                m_ServerCharacter = GetComponent<ServerCharacter>();
            }

            m_IsFollowing = m_StartFollowingOnSpawn;
        }

        public void FollowMonkeyKing()
        {
            if (!IsServer)
            {
                return;
            }

            m_IsFollowing = true;
            m_AttackTargetId = 0;
            m_NextUpdateTime = 0;
        }

        public void HoldPosition()
        {
            if (!IsServer)
            {
                return;
            }

            m_IsFollowing = false;
            m_AttackTargetId = 0;
            m_ServerCharacter.ActionPlayer.ClearActions(true);
            m_ServerCharacter.Movement.CancelMove();
        }

        public void AttackTarget(ulong targetId)
        {
            if (!IsServer)
            {
                return;
            }

            m_IsFollowing = false;
            m_AttackTargetId = targetId;
            m_NextUpdateTime = 0;
        }

        void Update()
        {
            if (Time.time < m_NextUpdateTime ||
                !m_ServerCharacter ||
                m_ServerCharacter.LifeState != LifeState.Alive)
            {
                return;
            }

            m_NextUpdateTime = Time.time + m_UpdateIntervalSeconds;

            if (m_AttackTargetId != 0)
            {
                UpdateAttackTarget();
                return;
            }

            if (!m_IsFollowing)
            {
                return;
            }

            var monkey = FindMonkeyKing();
            if (!monkey || monkey.LifeState != LifeState.Alive)
            {
                return;
            }

            var monkeyTransform = monkey.physicsWrapper.Transform;
            var desiredPosition = monkeyTransform.position + (monkeyTransform.rotation * m_FollowOffset);

            if (NavMesh.SamplePosition(desiredPosition, out var navMeshHit, 2f, NavMesh.AllAreas))
            {
                desiredPosition = navMeshHit.position;
            }

            var currentPosition = m_ServerCharacter.physicsWrapper.Transform.position;
            if ((currentPosition - desiredPosition).sqrMagnitude > m_RepathDistance * m_RepathDistance)
            {
                m_ServerCharacter.Movement.SetMovementTarget(desiredPosition);
            }
        }

        void UpdateAttackTarget()
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(m_AttackTargetId, out var targetObject) ||
                !targetObject.TryGetComponent(out ServerCharacter targetCharacter) ||
                targetCharacter.LifeState != LifeState.Alive)
            {
                m_AttackTargetId = 0;
                return;
            }

            var attack = m_ServerCharacter.CharacterClass.Skill1;
            if (!attack || !m_ServerCharacter.ActionPlayer.IsReuseTimeElapsed(attack.ActionID))
            {
                return;
            }

            if (m_ServerCharacter.ActionPlayer.GetActiveActionInfo(out var activeAction) &&
                activeAction.TargetIds != null &&
                activeAction.TargetIds.Length > 0 &&
                activeAction.TargetIds[0] == m_AttackTargetId)
            {
                return;
            }

            var attackData = new ActionRequestData
            {
                ActionID = attack.ActionID,
                TargetIds = new[] { m_AttackTargetId },
                ShouldClose = true,
                Direction = m_ServerCharacter.physicsWrapper.Transform.forward
            };

            m_ServerCharacter.ActionPlayer.PlayAction(ref attackData);
        }

        static ServerCharacter FindMonkeyKing()
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
    }
}
