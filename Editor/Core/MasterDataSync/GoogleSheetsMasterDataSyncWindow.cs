using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class GoogleSheetsMasterDataSyncWindow : EditorWindow
{
    private const string LastSettingsGuidKey = "GoogleSheetsMasterDataSyncWindow.LastSettingsGuid";

    private GoogleSheetsMasterDataSyncSettings _settings;
    private SerializedObject _serializedSettings;
    private Vector2 _scrollPosition;

    [MenuItem("Tools/Master Data/Google Sheets Sync")]
    private static void Open()
    {
        GetWindow<GoogleSheetsMasterDataSyncWindow>("Master Data Sync");
    }

    private void OnEnable()
    {
        LoadLastSettings();
    }

    private void OnGUI()
    {
        DrawSettingsPicker();
        if (_settings == null)
        {
            EditorGUILayout.HelpBox("Create or assign a GoogleSheetsMasterDataSyncSettings asset to continue.", MessageType.Info);
            if (GUILayout.Button("Create Settings Asset"))
            {
                CreateSettingsAsset();
            }

            return;
        }

        _serializedSettings ??= new SerializedObject(_settings);
        _serializedSettings.Update();

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_spreadsheetId"));
        EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_serviceAccountCredentialsJsonAsset"));
        EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_serviceAccountCredentialsJsonFilePath"));
        EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_masterAssetSearchRoot"));

        EditorGUILayout.Space(8);
        DrawToolbar();
        EditorGUILayout.Space(8);
        DrawBindings();
        EditorGUILayout.EndScrollView();

        _serializedSettings.ApplyModifiedProperties();
    }

    private void DrawSettingsPicker()
    {
        EditorGUI.BeginChangeCheck();
        _settings = (GoogleSheetsMasterDataSyncSettings)EditorGUILayout.ObjectField(
            "Settings",
            _settings,
            typeof(GoogleSheetsMasterDataSyncSettings),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            _serializedSettings = _settings != null ? new SerializedObject(_settings) : null;
            SaveLastSettings();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Discover Master Assets"))
            {
                GoogleSheetsMasterDataSyncService.DiscoverBindings(_settings);
                _serializedSettings = new SerializedObject(_settings);
            }

            if (GUILayout.Button("Export All"))
            {
                _ = RunBatchAsync(export: true);
            }

            if (GUILayout.Button("Import All"))
            {
                if (EditorUtility.DisplayDialog(
                        "Import All Master Data",
                        "This will overwrite the local master-data assets listed below. Continue?",
                        "Import",
                        "Cancel"))
                {
                    _ = RunBatchAsync(export: false);
                }
            }
        }

        EditorGUILayout.HelpBox(
            "Primitive fields sync to individual columns. Arrays, lists, and nested objects are stored as JSON in a single cell.",
            MessageType.None);
    }

    private void DrawBindings()
    {
        var bindingsProperty = _serializedSettings.FindProperty("_bindings");
        EditorGUILayout.LabelField("Bindings", EditorStyles.boldLabel);

        if (bindingsProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No bindings configured. Use Discover Master Assets or add them manually.", MessageType.None);
            return;
        }

        for (var i = 0; i < bindingsProperty.arraySize; i++)
        {
            var bindingProperty = bindingsProperty.GetArrayElementAtIndex(i);
            var enabledProperty = bindingProperty.FindPropertyRelative("_enabled");
            var assetProperty = bindingProperty.FindPropertyRelative("_masterAsset");
            var sheetNameProperty = bindingProperty.FindPropertyRelative("_sheetName");

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.PropertyField(enabledProperty, new GUIContent("Enabled"));
                EditorGUILayout.PropertyField(assetProperty, new GUIContent("Master Asset"));
                EditorGUILayout.PropertyField(sheetNameProperty, new GUIContent("Sheet Name"));

                var binding = _settings.Bindings[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!enabledProperty.boolValue || binding.MasterAsset == null || string.IsNullOrWhiteSpace(binding.SheetName)))
                    {
                        if (GUILayout.Button("Export"))
                        {
                            _ = RunBindingAsync(binding, export: true);
                        }

                        if (GUILayout.Button("Import"))
                        {
                            if (EditorUtility.DisplayDialog(
                                    "Import Master Data",
                                    $"Overwrite local asset '{binding.MasterAsset.name}' from Google Sheet '{binding.SheetName}'?",
                                    "Import",
                                    "Cancel"))
                            {
                                _ = RunBindingAsync(binding, export: false);
                            }
                        }
                    }

                    if (GUILayout.Button("Remove"))
                    {
                        bindingsProperty.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                EditorGUILayout.LabelField(GoogleSheetsMasterDataSyncService.DescribeBinding(binding), EditorStyles.miniLabel);
            }
        }
    }

    private async Task RunBatchAsync(bool export)
    {
        var title = export ? "Exporting master data" : "Importing master data";

        try
        {
            var enabledBindings = _settings.Bindings.FindAll(binding => binding is { Enabled: true, MasterAsset: not null } && !string.IsNullOrWhiteSpace(binding.SheetName));
            if (enabledBindings.Count == 0)
            {
                Debug.LogWarning("No enabled master-data bindings are configured for batch sync.");
                return;
            }

            var failures = new List<string>();
            for (var i = 0; i < enabledBindings.Count; i++)
            {
                var binding = enabledBindings[i];
                EditorUtility.DisplayProgressBar(title, GoogleSheetsMasterDataSyncService.DescribeBinding(binding), (i + 1f) / enabledBindings.Count);

                try
                {
                    if (export)
                    {
                        await GoogleSheetsMasterDataSyncService.ExportBindingAsync(_settings, binding);
                    }
                    else
                    {
                        await GoogleSheetsMasterDataSyncService.ImportBindingAsync(_settings, binding);
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{GoogleSheetsMasterDataSyncService.DescribeBinding(binding)}: {exception.Message}");
                    Debug.LogException(exception);
                }
            }

            if (failures.Count > 0)
            {
                var summary = string.Join("\n", failures);
                Debug.LogError($"Completed batch sync with {failures.Count} failure(s).\n{summary}");
                EditorUtility.DisplayDialog(
                    "Master Data Sync Completed with Errors",
                    $"Finished processing {enabledBindings.Count} binding(s), but {failures.Count} failed.\n\n{summary}",
                    "OK");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    private async Task RunBindingAsync(GoogleSheetsMasterDataBinding binding, bool export)
    {
        try
        {
            var title = export ? "Exporting master data" : "Importing master data";
            EditorUtility.DisplayProgressBar(title, GoogleSheetsMasterDataSyncService.DescribeBinding(binding), 1f);

            if (export)
            {
                await GoogleSheetsMasterDataSyncService.ExportBindingAsync(_settings, binding);
            }
            else
            {
                await GoogleSheetsMasterDataSyncService.ImportBindingAsync(_settings, binding);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    private void CreateSettingsAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Google Sheets Sync Settings",
            "GoogleSheetsMasterDataSyncSettings",
            "asset",
            "Choose where to save the settings asset.");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var asset = CreateInstance<GoogleSheetsMasterDataSyncSettings>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        _settings = asset;
        _serializedSettings = new SerializedObject(_settings);
        SaveLastSettings();
        Selection.activeObject = asset;
    }

    private void LoadLastSettings()
    {
        var guid = EditorPrefs.GetString(LastSettingsGuidKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(guid))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrWhiteSpace(path))
            {
                _settings = AssetDatabase.LoadAssetAtPath<GoogleSheetsMasterDataSyncSettings>(path);
            }
        }

        if (_settings == null)
        {
            var settingsGuids = AssetDatabase.FindAssets("t:GoogleSheetsMasterDataSyncSettings");
            if (settingsGuids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(settingsGuids[0]);
                _settings = AssetDatabase.LoadAssetAtPath<GoogleSheetsMasterDataSyncSettings>(path);
            }
        }

        if (_settings != null)
        {
            _serializedSettings = new SerializedObject(_settings);
        }
    }

    private void SaveLastSettings()
    {
        if (_settings == null)
        {
            EditorPrefs.DeleteKey(LastSettingsGuidKey);
            return;
        }

        var path = AssetDatabase.GetAssetPath(_settings);
        var guid = AssetDatabase.AssetPathToGUID(path);
        EditorPrefs.SetString(LastSettingsGuidKey, guid);
    }
}
