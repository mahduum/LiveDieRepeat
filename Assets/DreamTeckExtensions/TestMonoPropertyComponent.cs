using UnityEngine;

namespace DreamTeckExtensions
{
    public class TestMonoPropertyComponent : MonoBehaviour
    {
        [SerializeField] private Color _channelColor;
        [SerializeField] private float _startingHealth;
        [SerializeField] private Transform _laneStartPosition;
    }
}