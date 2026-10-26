using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hidano.TransformSync.Runtime;

namespace Hidano.TransformSync.Editor
{
    /// <summary>
    /// Publisher Label を、シーン内に存在する <see cref="TransformPublisher"/> の
    /// ラベル名から選べるドロップダウンとして描画する。
    /// </summary>
    [CustomEditor(typeof(TransformSubscriber))]
    [CanEditMultipleObjects]
    public sealed class TransformSubscriberEditor : UnityEditor.Editor
    {
        private const string ManualInputDisplay = "<Manual Input>";

        private SerializedProperty _targetProperty;
        private SerializedProperty _publisherLabelProperty;
        private SerializedProperty _boneIdProperty;

        private TransformPublisher[] _publishers = Array.Empty<TransformPublisher>();
        private string[] _labels = Array.Empty<string>();
        private bool _manualInput;

        private void OnEnable()
        {
            _targetProperty = serializedObject.FindProperty("target");
            _publisherLabelProperty = serializedObject.FindProperty("publisherLabel");
            _boneIdProperty = serializedObject.FindProperty("boneId");

            RefreshPublishers();
            EditorApplication.hierarchyChanged += RefreshPublishers;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= RefreshPublishers;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetProperty);
            if (!_targetProperty.hasMultipleDifferentValues && _targetProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Target が未設定のため、この GameObject 自身の Transform を追従させます。", MessageType.Info);
            }

            DrawPublisherLabelField();
            EditorGUILayout.PropertyField(_boneIdProperty);
            DrawPublisherStatus();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPublisherLabelField()
        {
            var current = _publisherLabelProperty.stringValue;

            var values = new List<string>(_labels);
            var displays = _labels.Select(label => new GUIContent(label)).ToList();

            // シーンに存在しないラベルが設定されている場合も選択状態を失わせない。
            if (!_publisherLabelProperty.hasMultipleDifferentValues &&
                !string.IsNullOrEmpty(current) && !values.Contains(current))
            {
                values.Insert(0, current);
                displays.Insert(0, new GUIContent($"{current}  (シーンに存在しません)"));
            }

            var manualIndex = values.Count;
            values.Add(null);
            displays.Add(new GUIContent(ManualInputDisplay));

            int index;
            if (_manualInput) index = manualIndex;
            else if (string.IsNullOrEmpty(current)) index = -1;
            else index = values.IndexOf(current);

            using (var changeScope = new EditorGUI.ChangeCheckScope())
            {
                EditorGUI.showMixedValue = _publisherLabelProperty.hasMultipleDifferentValues;
                var selected = EditorGUILayout.Popup(
                    new GUIContent(_publisherLabelProperty.displayName, "シーン内の TransformPublisher のラベルから選択する。"),
                    index, displays.ToArray());
                EditorGUI.showMixedValue = false;

                if (changeScope.changed)
                {
                    _manualInput = selected == manualIndex;
                    if (!_manualInput) _publisherLabelProperty.stringValue = values[selected];
                }
            }

            if (_manualInput)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_publisherLabelProperty, new GUIContent("Label"));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Publisher 一覧を更新", EditorStyles.miniButton, GUILayout.Width(160f)))
                {
                    RefreshPublishers();
                }
            }
        }

        private void DrawPublisherStatus()
        {
            if (_publisherLabelProperty.hasMultipleDifferentValues || _boneIdProperty.hasMultipleDifferentValues) return;

            var label = _publisherLabelProperty.stringValue;
            if (string.IsNullOrEmpty(label))
            {
                EditorGUILayout.HelpBox("Publisher Label が未設定です。", MessageType.Warning);
                return;
            }

            var publisher = FindPublisher(label);
            if (publisher == null)
            {
                EditorGUILayout.HelpBox(
                    $"ラベル '{label}' の {nameof(TransformPublisher)} がシーン内に見つかりません。", MessageType.Warning);
                return;
            }

            var boneId = (HumanoidBoneId)_boneIdProperty.enumValueIndex;
            var animator = publisher.Animator;
            if (animator == null || !animator.isHuman)
            {
                if (boneId != HumanoidBoneId.Root)
                {
                    EditorGUILayout.HelpBox(
                        $"'{publisher.name}' は Animator/HumanoidAvatar を持たないため、" +
                        $"どの骨を指定しても '{publisher.name}' 自身の Transform に追従します。", MessageType.Info);
                }
                return;
            }

            if (!publisher.HasBone(boneId))
            {
                EditorGUILayout.HelpBox(
                    $"'{publisher.name}' のアバターに {boneId} がありません。追従はスキップされます。", MessageType.Warning);
            }
        }

        private TransformPublisher FindPublisher(string label)
        {
            return _publishers.FirstOrDefault(publisher => publisher != null && publisher.Label == label);
        }

        private void RefreshPublishers()
        {
            var found = new List<TransformPublisher>(
                FindObjectsByType<TransformPublisher>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                found.AddRange(prefabStage.prefabContentsRoot.GetComponentsInChildren<TransformPublisher>(true));
            }

            _publishers = found.Where(publisher => publisher != null).Distinct().ToArray();
            _labels = _publishers
                .Select(publisher => publisher.Label)
                .Where(label => !string.IsNullOrEmpty(label))
                .Distinct()
                .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Repaint();
        }
    }
}
