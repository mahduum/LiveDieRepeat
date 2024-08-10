using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DreamTeckExtensions.Editor
{
    [CustomEditor(typeof(TestCustomEditorForMonoProperty))]
    public class TestCustomEditorForMonoProperty_Editor : UnityEditor.Editor
    {
        [SerializeField]
        public StyleSheet _styleSheet;
        
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var box = new Box();
            box.Add(new PropertyField(serializedObject.FindProperty("_name"), "Test Name"));
            box.Add(new PropertyField(serializedObject.FindProperty("_id"), "Test Id"));
            
            var descriptionsProp = serializedObject.FindProperty("_descriptions");
            var descriptionsField = new PropertyField(descriptionsProp, "Test Descriptions");
            descriptionsField.RegisterCallback<AttachToPanelEvent>(e =>
            {
                if (_styleSheet)
                {
                    descriptionsField.styleSheets.Add(_styleSheet);
                }
            });

            var descriptionsListProp = serializedObject.FindProperty("_descriptionsList");
            var descriptionsListField = new PropertyField(descriptionsListProp, "Test Descriptions List");
            
            var numbersProp = serializedObject.FindProperty("_numbers");
            var nums = new PropertyField(numbersProp, "Test Numbers");
            //new ListView(descriptionsProp, 1)
            root.Add(nums);
            root.Add(descriptionsField);
            root.Add(descriptionsListField);
            root.Add(box);
            
            InspectorElement.FillDefaultInspector(descriptionsField, serializedObject, this);//additional default draw for debug

            return root;
        }
    }
}