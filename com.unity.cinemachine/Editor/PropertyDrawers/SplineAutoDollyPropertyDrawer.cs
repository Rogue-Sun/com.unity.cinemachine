using System;
using System.Collections.Generic;
using Cinemachine.Utility;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Reflection;

namespace Cinemachine.Editor
{
    [CustomPropertyDrawer(typeof(SplineAutoDolly))]
    class SplineAutoDollyPropertyDrawer : PropertyDrawer
    {
        SerializedProperty GetImplementation(SerializedProperty p) => p.FindPropertyRelative("Implementation");

        int GetImplementationIndex(SerializedProperty p)
        {
            var value = GetImplementation(p).managedReferenceValue;
            return AutoDollyMenuItems.GetTypeIndex(value == null ? null : value.GetType());
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var ux = new VisualElement();

            var dropdown = ux.AddChild(new DropdownField
            {
                label = property.name,
                tooltip = property.tooltip,
                choices = AutoDollyMenuItems.s_ItemNames,
                style = { flexGrow = 1 }
            });
            dropdown.AddToClassList(InspectorUtility.kAlignFieldClass);
            dropdown.RegisterValueChangedCallback((evt) => 
            {
                var index = AutoDollyMenuItems.GetTypeIndex(evt.newValue);
                if (index != GetImplementationIndex(property))
                {
                    var p = GetImplementation(property);
                    var targets = p.serializedObject.targetObjects;
                    foreach (var t in targets)
                    {
                        var o = new SerializedObject(t);
                        var p2 = o.FindProperty(p.propertyPath);
                        p2.managedReferenceValue = (index == 0) 
                            ? null : Activator.CreateInstance(AutoDollyMenuItems.s_AllItems[index]);
                        o.ApplyModifiedProperties();
                    }
                }
            });
            
            Update();
            ux.TrackPropertyValue(property, (p) => 
            {
                Update();
                ux.Bind(p.serializedObject); // Bind is not automatic after the the initial creation
            });

            void Update()
            {
                dropdown.index = GetImplementationIndex(property);

                // First delete the existing element
                const string kElementName = "ActiveAutoDollyContainer";
                var old = ux.Q(kElementName);
                if (old != null)
                    old.RemoveFromHierarchy();

                // Create a new one
                var type = AutoDollyMenuItems.s_AllItems[dropdown.index];
                if (type != null)
                {
                    var foldout = ux.AddChild(new Foldout() { name = kElementName, value = true });
                    foldout.Q<Toggle>(className: "unity-foldout__toggle").RemoveFromHierarchy(); // remove the header
                    var childProperty = GetImplementation(property);
                    var endProperty = childProperty.GetEndProperty();
                    childProperty.NextVisible(true);
                    while (!SerializedProperty.EqualContents(childProperty, endProperty))
                    {
                        foldout.Add(new PropertyField(childProperty));
                        childProperty.NextVisible(false);
                    }
                }
            }

            return ux;
        }

        [InitializeOnLoad]
        static class AutoDollyMenuItems
        {
            public static int GetTypeIndex(Type type)
            {
                for (int i = 0; i < s_AllItems.Count; ++i)
                    if (s_AllItems[i] == type)
                        return i;
                return 0;
            }
            public static int GetTypeIndex(string typeName)
            {
                for (int i = 0; i < s_ItemNames.Count; ++i)
                    if (s_ItemNames[i] == typeName)
                        return i;
                return 0;
            }
        
            // These lists are synchronized
            public static List<Type> s_AllItems = new ();
            public static List<string> s_ItemNames = new ();

            // This code dynamically discovers eligible classes and builds the menu data 
            static AutoDollyMenuItems()
            {
                // Get all eligible types
                var allTypes
                    = ReflectionHelpers.GetTypesInAllDependentAssemblies(
                        (Type t) => typeof(SplineAutoDolly.ISplineAutoDolly).IsAssignableFrom(t) 
                            && !t.IsAbstract && t.GetCustomAttribute<ObsoleteAttribute>() == null);

                s_AllItems.Clear();
                s_AllItems.Add(null);
                s_AllItems.AddRange(allTypes);

                s_ItemNames.Clear();
                s_ItemNames.Add("None");
                for (int i = 1; i < s_AllItems.Count; ++i)
                    s_ItemNames.Add(InspectorUtility.NicifyClassName(s_AllItems[i]));
            }
        }
    }
}

