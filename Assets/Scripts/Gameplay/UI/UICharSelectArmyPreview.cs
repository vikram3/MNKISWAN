using UnityEngine;
using UnityEngine.UI;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// Decorative-only character select army lineup for the two prototype kingdoms.
    /// </summary>
    public class UICharSelectArmyPreview : MonoBehaviour
    {
        [SerializeField]
        GameObject[] m_LeftArmyGraphics;

        [SerializeField]
        GameObject[] m_RightArmyGraphics;

        [SerializeField]
        Vector3 m_LeftOrigin = new Vector3(-5.4f, -1.85f, -41.25f);

        [SerializeField]
        Vector3 m_RightOrigin = new Vector3(5.4f, -1.85f, -41.25f);

        [SerializeField]
        Vector3 m_Spacing = new Vector3(1.15f, 0f, 0f);

        [SerializeField]
        Vector3 m_LeftRotationEuler = new Vector3(0f, 160f, 0f);

        [SerializeField]
        Vector3 m_RightRotationEuler = new Vector3(0f, 200f, 0f);

        [SerializeField]
        float m_Scale = 0.65f;

        void Start()
        {
            SpawnLineup(m_LeftArmyGraphics, m_LeftOrigin, m_LeftRotationEuler);
            SpawnLineup(m_RightArmyGraphics, m_RightOrigin, m_RightRotationEuler);
        }

        void SpawnLineup(GameObject[] graphicsPrefabs, Vector3 origin, Vector3 rotationEuler)
        {
            if (graphicsPrefabs == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(rotationEuler);
            for (int i = 0; i < graphicsPrefabs.Length; i++)
            {
                var prefab = graphicsPrefabs[i];
                if (!prefab)
                {
                    continue;
                }

                var instance = Instantiate(prefab, origin + (m_Spacing * i), rotation, transform);
                instance.name = prefab.name + "_ArmyPreview";
                instance.transform.localScale = Vector3.one * m_Scale;
                MakeDecorativeOnly(instance);
            }
        }

        static void MakeDecorativeOnly(GameObject root)
        {
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                selectable.interactable = false;
            }

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }
    }
}
