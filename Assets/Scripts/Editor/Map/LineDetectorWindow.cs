/**
 * Copyright (c) 2020 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

namespace Simulator.Editor
{
    using Simulator.Editor.MapLineDetection;
    using Simulator.Editor.MapMeshes;
    using UnityEditor;
    using UnityEngine;

    public class LineDetectorWindow : EditorWindow
    {
        private static class Styles
        {
            public static readonly GUIStyle TitleLabelStyle = new GUIStyle(GUI.skin.label)
                {alignment = TextAnchor.MiddleCenter, fontSize = 14};
        }

        private const string LineDetectionSettingsKey = "Simulator/LineDetectionSettings";
        private const string MeshBuilderSettingsKey = "Simulator/MapMeshSettings";
        private const string ParentObjectName = "LineData_generated";

        private LineDetectionSettings lineDetectionSettings;
        private SerializedObject serializedLineDetectionSettings;
        
        private MapMeshSettings meshBuilderSettings;
        private SerializedObject serializedMeshBuilderSettings;

        private SerializedProperty lineSource;
        private SerializedProperty lineDistanceThreshold;
        private SerializedProperty lineAngleThreshold;
        private SerializedProperty maxLineSegmentLength;
        private SerializedProperty worstFitThreshold;
        private SerializedProperty jointLineThreshold;
        private SerializedProperty minWidthThreshold;
        private SerializedProperty worldSpaceSnapDistance;
        private SerializedProperty worldSpaceSnapAngle;
        private SerializedProperty snapLaneEnds;
        private SerializedProperty snapThreshold;
        private SerializedProperty lineUvUnit;
        private SerializedProperty lineBump;
        private SerializedProperty lineWidth;

        private SerializedObject SerializedLineDetectionSettings
        {
            get
            {
                if (serializedLineDetectionSettings == null || lineDetectionSettings == null)
                    LoadSettings();
                return serializedLineDetectionSettings;
            }
        }
        
        private SerializedObject SerializedMeshBuilderSettings
        {
            get
            {
                if (serializedLineDetectionSettings == null || lineDetectionSettings == null)
                    LoadSettings();
                return serializedLineDetectionSettings;
            }
        }

        [MenuItem("Simulator/Detect Lane Lines", false, 128)]
        private static void ShowWindow()
        {
            var window = GetWindow<LineDetectorWindow>();
            window.titleContent = new GUIContent("Lane Line Detector");
            window.Show();
        }

        public void OnEnable()
        {
            LoadSettings();
            Undo.undoRedoPerformed += UndoRedoPerformed;
        }

        private void OnDisable()
        {
            SaveSettings();

            // ReSharper disable once DelegateSubtraction
            Undo.undoRedoPerformed -= UndoRedoPerformed;
        }

        private void UndoRedoPerformed()
        {
            if (serializedLineDetectionSettings != null)
                SerializedLineDetectionSettings.Update();
            
            if (serializedMeshBuilderSettings != null)
                SerializedMeshBuilderSettings.Update();

            Repaint();
        }

        private void LoadSettings()
        {
            var data = EditorPrefs.GetString(LineDetectionSettingsKey, null);
            lineDetectionSettings = CreateInstance<LineDetectionSettings>();

            try
            {
                JsonUtility.FromJsonOverwrite(data, lineDetectionSettings);
            }
            catch
            {
                // Deserialization failed, but default values are already present
            }

            serializedLineDetectionSettings = new SerializedObject(lineDetectionSettings);
            
            data = EditorPrefs.GetString(MeshBuilderSettingsKey, null);
            meshBuilderSettings = CreateInstance<MapMeshSettings>();

            try
            {
                JsonUtility.FromJsonOverwrite(data, meshBuilderSettings);
            }
            catch
            {
                // Deserialization failed, but default values are already present
            }

            serializedMeshBuilderSettings = new SerializedObject(meshBuilderSettings);
            
            FindProperties();
        }

        private void LoadDefaultSettings()
        {
            lineDetectionSettings = CreateInstance<LineDetectionSettings>();
            serializedLineDetectionSettings = new SerializedObject(lineDetectionSettings);
            meshBuilderSettings = CreateInstance<MapMeshSettings>();
            serializedMeshBuilderSettings = new SerializedObject(lineDetectionSettings);
            FindProperties();
            SaveSettings();
        }

        private void FindProperties()
        {
            lineSource = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.lineSource));
            lineDistanceThreshold = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.lineDistanceThreshold));
            lineAngleThreshold = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.lineAngleThreshold));
            maxLineSegmentLength = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.maxLineSegmentLength));
            worstFitThreshold = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.worstFitThreshold));
            jointLineThreshold = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.jointLineThreshold));
            minWidthThreshold = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.minWidthThreshold));
            worldSpaceSnapDistance = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.worldSpaceSnapDistance));
            worldSpaceSnapAngle = serializedLineDetectionSettings.FindProperty(nameof(LineDetectionSettings.worldSpaceSnapAngle));
            snapLaneEnds = serializedMeshBuilderSettings.FindProperty(nameof(MapMeshSettings.snapLaneEnds));
            snapThreshold = serializedMeshBuilderSettings.FindProperty(nameof(MapMeshSettings.snapThreshold));
            lineUvUnit = serializedMeshBuilderSettings.FindProperty(nameof(MapMeshSettings.lineUvUnit));
            lineBump = serializedMeshBuilderSettings.FindProperty(nameof(MapMeshSettings.lineBump));
            lineWidth = serializedMeshBuilderSettings.FindProperty(nameof(MapMeshSettings.lineWidth));
        }

        private void SaveSettings()
        {
            if (serializedLineDetectionSettings != null)
            {
                SerializedLineDetectionSettings.ApplyModifiedProperties();
                var data = JsonUtility.ToJson(lineDetectionSettings);
                EditorPrefs.SetString(LineDetectionSettingsKey, data);
            }
            
            if (serializedMeshBuilderSettings != null)
            {
                SerializedMeshBuilderSettings.ApplyModifiedProperties();
                var data = JsonUtility.ToJson(meshBuilderSettings);
                EditorPrefs.SetString(MeshBuilderSettingsKey, data);
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawSettingsSection();
            EditorGUILayout.Space();
            DrawDefaultSettingsButton();
            EditorGUILayout.Space();
            DrawImportButton();
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Lane Line Detector", Styles.TitleLabelStyle);
            GUILayout.Space(5);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            GUILayout.Space(10);
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.PropertyField(lineSource);
            if (lineSource.enumValueIndex == (int) LineDetectionSettings.LineSource.IntensityMap)
            {
                EditorGUILayout.PropertyField(lineDistanceThreshold);
                EditorGUILayout.PropertyField(lineAngleThreshold);
                EditorGUILayout.PropertyField(maxLineSegmentLength);
                EditorGUILayout.PropertyField(worstFitThreshold);
                EditorGUILayout.PropertyField(minWidthThreshold);
                EditorGUILayout.PropertyField(jointLineThreshold);
                EditorGUILayout.PropertyField(worldSpaceSnapDistance);
                EditorGUILayout.PropertyField(worldSpaceSnapAngle);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(!snapLaneEnds.boolValue);
                EditorGUILayout.PropertyField(snapThreshold);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.PropertyField(lineUvUnit);
                EditorGUILayout.PropertyField(lineWidth);
                EditorGUILayout.PropertyField(lineBump);
            }
        }

        private void DrawDefaultSettingsButton()
        {
            if (GUILayout.Button("Reset Settings"))
                LoadDefaultSettings();
        }

        private void DrawImportButton()
        {
            SerializedLineDetectionSettings.ApplyModifiedProperties();
            SerializedMeshBuilderSettings.ApplyModifiedProperties();

            if (lineSource.enumValueIndex == (int) LineDetectionSettings.LineSource.IntensityMap)
            {
                if (GUILayout.Button("Detect Lines"))
                {
                    var parent = GameObject.Find(ParentObjectName);
                    if (parent != null)
                        DestroyImmediate(parent);

                    parent = new GameObject(ParentObjectName);

                    LineDetector.Execute(parent.transform, lineDetectionSettings);
                }
            }
            else
            {
                if (GUILayout.Button("Generate Lines"))
                {
                    var parent = GameObject.Find(ParentObjectName);
                    if (parent != null)
                        DestroyImmediate(parent);

                    parent = new GameObject(ParentObjectName);

                    var materials = AssetDatabase.LoadAssetAtPath<MapMeshMaterials>("Assets/Resources/Editor/HDMapMaterials.asset");
                    materials = Instantiate(materials);
                    materials.OverrideShader(Shader.Find("Simulator/SegmentationLine"));
                    
                    var builder = new MapMeshBuilder(meshBuilderSettings);
                    
                    builder.BuildLinesMesh(parent, materials);
                }
            }
        }
    }
}