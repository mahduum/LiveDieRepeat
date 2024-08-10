using System.Reflection;
using Dreamteck.Splines;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DreamTeckExtensions.Editor
{
    [CustomPropertyDrawer(typeof(TestMonoPropertyComponent))]
    public class TestPropertyDrawerForMono : PropertyDrawer
    {
        private SerializedObject _splineMeshObject;
        private SerializedProperty _splineMeshChannelsProperty;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            var laneDescField = new ObjectField("Debug Test Description")
            {
                objectType = typeof(TestMonoPropertyComponent),
                allowSceneObjects = true
            };

            laneDescField.BindProperty(property);
            
            root.Add(laneDescField);

            var box = new Box();
            box.AddToClassList("indent-box");

            root.Add(box);

            //return root;
            
            laneDescField.RegisterCallback<ChangeEvent<Object>, VisualElement>((e, parent) =>
            {
                parent.Clear();
                
                var obj = e.newValue as TestMonoPropertyComponent;
                if (obj != null)
                {
                    
                    // Create a SerializedObject for the selected SplineMeshChannelLaneDesc
                    var serializedObject = new SerializedObject(obj);//todo only drawn

                    // Display the internal properties
                    //var channelColorField = new PropertyField(serializedObject.FindProperty("_channelColor"), "Channel Color");
                    var channelColorField = new ColorField("Test Channel Color");
                    channelColorField.BindProperty(serializedObject.FindProperty("_channelColor"));
                    var startingHealthField = new FloatField("Test Health Channel");
                    startingHealthField.BindProperty(serializedObject.FindProperty("_startingHealth"));

                    var transform = serializedObject.FindProperty("_laneStartPosition").objectReferenceValue;

                    //o.Add(box);
                    parent.Add(channelColorField);
                    parent.Add(startingHealthField);
                    parent.Add(new InspectorElement(transform));

                    // Optionally, you can call serializedObject.Update() to refresh the properties
                    serializedObject.Update();
                    
                    //o.Add(new InspectorElement(obj));//not inspector, but box
                    //o.Add(new InspectorElement(serializedObject));//not inspector, but box
                }
                
            }, box);
            
            //startPos.RegisterCallback<ChangeEvent<Object>, VisualElement>(SpawnChanged, spawnInspector);

            return root;
        }

        private void SpawnChanged(ChangeEvent<Object> e, VisualElement spawnInspector)
        {
            spawnInspector.Clear();

            var t = e.newValue;
            
            Debug.Log("Value changed.");
            if (t == null) return;
            
            spawnInspector.Add(new InspectorElement(t));//looks up a custom editor and if not found creates a default one
        }

        private PropertyField FindPropertyField(System.Type editorType, UnityEditor.Editor editorInstance,
            string propertyName)
        {
            var fields = editorType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);
            foreach (var fieldInfo in fields)
            {
                if (fieldInfo.FieldType == typeof(PropertyField))
                {
                    var propertyField = fieldInfo.GetValue(editorInstance) as PropertyField;
                    Debug.Log($"Property field binding path: {propertyField?.bindingPath}, name: {propertyField?.name}");
                    if (propertyField != null && propertyField.bindingPath == propertyName)
                    {
                        return propertyField;
                    }
                }
            }

            return null;
        }
    }
}