using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GoogleSheetsMasterDataSyncSettings",
    menuName = "Tools/Master Data/Google Sheets Sync Settings")]
public sealed class GoogleSheetsMasterDataSyncSettings : ScriptableObject
{
    [SerializeField] private string _spreadsheetId;
    [SerializeField] private TextAsset _serviceAccountCredentialsJsonAsset;
    [SerializeField] private string _serviceAccountCredentialsJsonFilePath;
    [SerializeField] private string _masterAssetSearchRoot = "Assets/AddressableResources/Master";
    [SerializeField] private List<GoogleSheetsMasterDataBinding> _bindings = new();

    public string SpreadsheetId => _spreadsheetId;
    public TextAsset ServiceAccountCredentialsJsonAsset => _serviceAccountCredentialsJsonAsset;
    public string ServiceAccountCredentialsJsonFilePath => _serviceAccountCredentialsJsonFilePath;
    public string MasterAssetSearchRoot => _masterAssetSearchRoot;
    public List<GoogleSheetsMasterDataBinding> Bindings => _bindings;
}

[Serializable]
public sealed class GoogleSheetsMasterDataBinding
{
    [SerializeField] private bool _enabled = true;
    [SerializeField] private ScriptableObject _masterAsset;
    [SerializeField] private string _sheetName;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public ScriptableObject MasterAsset
    {
        get => _masterAsset;
        set => _masterAsset = value;
    }

    public string SheetName
    {
        get => _sheetName;
        set => _sheetName = value;
    }
}
