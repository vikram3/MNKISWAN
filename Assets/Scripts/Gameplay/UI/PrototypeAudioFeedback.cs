using Unity.BossRoom.Audio;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Small local-only prototype audio layer using existing Boss Room clips and music flow.
    /// </summary>
    public class PrototypeAudioFeedback : MonoBehaviour
    {
        const float k_CriticalHealthPercent = 0.35f;
        const float k_CriticalRepeatSeconds = 8f;

        [SerializeField]
        AudioClip m_MonkeyAttackClip;

        [SerializeField]
        AudioClip m_SwanAbilityClip;

        [SerializeField]
        AudioClip m_CommandConfirmClip;

        [SerializeField]
        AudioClip m_MonkeyCriticalClip;

        [SerializeField]
        AudioClip m_CommanderPresenceClip;

        AudioSource m_Source;
        ServerCharacter m_LocalCharacter;
        ClientInputSender m_LocalInputSender;
        string m_LastSwanCommand = "None";
        bool m_CommanderPresencePlayed;
        bool m_CommanderWasAlive;
        bool m_VictoryPlayed;
        float m_NextCriticalTime;

        void Awake()
        {
            m_Source = gameObject.AddComponent<AudioSource>();
            m_Source.playOnAwake = false;
            m_Source.spatialBlend = 0f;

            ClientPlayerAvatar.LocalClientSpawned += OnLocalClientSpawned;
            ClientPlayerAvatar.LocalClientDespawned += OnLocalClientDespawned;
        }

        void OnDestroy()
        {
            ClientPlayerAvatar.LocalClientSpawned -= OnLocalClientSpawned;
            ClientPlayerAvatar.LocalClientDespawned -= OnLocalClientDespawned;
            ClearLocalInputSender();
        }

        void OnLocalClientSpawned(ClientPlayerAvatar avatar)
        {
            ClearLocalInputSender();
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
            ClearLocalInputSender();
            m_LocalCharacter = null;
        }

        void ClearLocalInputSender()
        {
            if (m_LocalInputSender)
            {
                m_LocalInputSender.ActionInputEvent -= OnLocalActionInput;
            }

            m_LocalInputSender = null;
        }

        void Update()
        {
            PlaySwanCommandConfirmation();
            PlayMonkeyCriticalHealthWarning();
            UpdateCommanderMusicAndPresence();
        }

        void OnLocalActionInput(ActionRequestData data)
        {
            if (!m_LocalCharacter)
            {
                return;
            }

            if (m_LocalCharacter.CharacterClass.DisplayedName == "MONKEY KING" &&
                data.ActionID == m_LocalCharacter.CharacterClass.Skill1.ActionID)
            {
                PlayOneShot(m_MonkeyAttackClip, 0.7f);
            }
            else if (m_LocalCharacter.CharacterClass.DisplayedName == "SWAN PRINCESS" &&
                data.ActionID == m_LocalCharacter.CharacterClass.Skill2.ActionID)
            {
                PlayOneShot(m_SwanAbilityClip, 0.75f);
            }
        }

        void PlaySwanCommandConfirmation()
        {
            if (!m_LocalInputSender ||
                !m_LocalCharacter ||
                m_LocalCharacter.CharacterClass.DisplayedName != "SWAN PRINCESS")
            {
                return;
            }

            var command = m_LocalInputSender.CurrentSwanArmyCommand;
            if (command != m_LastSwanCommand)
            {
                m_LastSwanCommand = command;
                if (command != "None")
                {
                    PlayOneShot(m_CommandConfirmClip, 0.65f);
                }
            }
        }

        void PlayMonkeyCriticalHealthWarning()
        {
            var monkey = FindMonkey();
            if (!monkey || monkey.LifeState != LifeState.Alive)
            {
                return;
            }

            var maxHp = Mathf.Max(1, monkey.CharacterClass.BaseHP.Value);
            if (monkey.HitPoints / (float)maxHp > k_CriticalHealthPercent ||
                Time.unscaledTime < m_NextCriticalTime)
            {
                return;
            }

            m_NextCriticalTime = Time.unscaledTime + k_CriticalRepeatSeconds;
            PlayOneShot(m_MonkeyCriticalClip, 0.85f);
        }

        void UpdateCommanderMusicAndPresence()
        {
            var commander = FindCommander();
            var commanderAlive = commander && commander.LifeState == LifeState.Alive;

            if (commanderAlive && !m_CommanderPresencePlayed)
            {
                m_CommanderPresencePlayed = true;
                m_CommanderWasAlive = true;
                PlayOneShot(m_CommanderPresenceClip, 0.8f);
                if (ClientMusicPlayer.Instance)
                {
                    ClientMusicPlayer.Instance.PlayBossMusic();
                }
            }

            if (m_CommanderWasAlive && !commanderAlive && !m_VictoryPlayed)
            {
                m_VictoryPlayed = true;
                if (ClientMusicPlayer.Instance)
                {
                    ClientMusicPlayer.Instance.PlayVictoryMusic();
                }
            }
        }

        void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip)
            {
                m_Source.PlayOneShot(clip, volume);
            }
        }

        static ServerCharacter FindMonkey()
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

        static ServerCharacter FindCommander()
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
    }
}
