using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace Documentator.Plugins
{
    [Serializable]
    public class KindAttributeReplacement
    {
        public string OldKind;
        public string AttributeName;
        public string NewKind;
    }

    public class KindAttributePlugin : IDocumentationPlugin
    {
        private List<KindAttributeReplacement> _replacements = new List<KindAttributeReplacement>();
        private bool _isDirty = false;

        public string GetSerializedSettings()
        {
            return JsonUtility.ToJson(new SerializableList<KindAttributeReplacement> { Items = _replacements });
        }

        public void LoadSettings(string settingsJson)
        {
            var serializableList = JsonUtility.FromJson<SerializableList<KindAttributeReplacement>>(settingsJson);
            _replacements = serializableList?.Items ?? new List<KindAttributeReplacement>();
        }

        public void Generate(DocumentationProject project)
        {
            foreach (var ns in project.Namespaces)
            {
                ProcessItems(ns.Items);
            }
        }

        private void ProcessItems(List<DocumentationItem> items)
        {
            foreach (var item in items)
            {
                foreach (var replacement in _replacements)
                {
                    if (item.Kind == replacement.OldKind && item.Attributes.Exists(a => a.Name == replacement.AttributeName))
                    {
                        item.Kind = replacement.NewKind;
                        break; // Stop after first match
                    }
                }

                // Recursively process nested items
                ProcessItems(item.Members);
            }
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Replacements", EditorStyles.boldLabel);

            for (int i = 0; i < _replacements.Count; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUI.BeginChangeCheck();
                _replacements[i].OldKind = EditorGUILayout.TextField("Old Kind", _replacements[i].OldKind);
                _replacements[i].AttributeName = EditorGUILayout.TextField("Attribute Name", _replacements[i].AttributeName);
                _replacements[i].NewKind = EditorGUILayout.TextField("New Kind", _replacements[i].NewKind);
                if (EditorGUI.EndChangeCheck())
                {
                    _isDirty = true;
                }

                if (GUILayout.Button("Remove"))
                {
                    _replacements.RemoveAt(i);
                    i--;
                    _isDirty = true;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            if (GUILayout.Button("Add Replacement"))
            {
                _replacements.Add(new KindAttributeReplacement());
                _isDirty = true;
            }
        }

        public bool IsDirty()
        {
            return _isDirty;
        }

        public void ResetDirtyFlag()
        {
            _isDirty = false;
        }
    }

    [Serializable]
    public class SerializableList<T>
    {
        public List<T> Items;
    }
}