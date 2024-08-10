using System.Collections.Generic;
using UnityEngine;

namespace DreamTeckExtensions
{
    public class TestCustomEditorForMonoProperty : MonoBehaviour
    {
        [SerializeField] private TestMonoPropertyComponent _descriptions;
        [SerializeField] private List<TestMonoPropertyComponent> _descriptionsList;
        [SerializeField] private List<int> _numbers;
        [SerializeField] private int _id;
        [SerializeField] private string _name;

        private void OnValidate()
        {
            //_descriptions.Clear();
            //_descriptions.Add(GetComponent<SplineMeshChannelLaneDesc>());
        }
    }
}