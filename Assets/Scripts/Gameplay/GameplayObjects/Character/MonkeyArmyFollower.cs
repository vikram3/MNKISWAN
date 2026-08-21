using Unity.Netcode;
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

        float m_NextUpdateTime;

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
        }

        void Update()
        {
            if (Time.time < m_NextUpdateTime || !m_ServerCharacter || m_ServerCharacter.LifeState != LifeState.Alive)
            {
                return;
            }

            m_NextUpdateTime = Time.time + m_UpdateIntervalSeconds;

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
