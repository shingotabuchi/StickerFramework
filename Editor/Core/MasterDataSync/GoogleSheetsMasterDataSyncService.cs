using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using StickerFwk.Core.MasterData;
using MiniJSON;
using UnityEditor;
using UnityEngine;

public static class GoogleSheetsMasterDataSyncService
{
    private const string SheetsScope = "https://www.googleapis.com/auth/spreadsheets";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string SheetsApiBaseUrl = "https://sheets.googleapis.com/v4/spreadsheets";
    private static readonly HttpClient HttpClient = new();

    public static void DiscoverBindings(GoogleSheetsMasterDataSyncSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var searchRoot = string.IsNullOrWhiteSpace(settings.MasterAssetSearchRoot)
            ? "Assets"
            : settings.MasterAssetSearchRoot;

        var existingAssetPaths = new HashSet<string>(
            settings.Bindings
                .Where(binding => binding != null && binding.MasterAsset != null)
                .Select(binding => AssetDatabase.GetAssetPath(binding.MasterAsset)),
            StringComparer.OrdinalIgnoreCase);

        var assetGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { searchRoot });
        Undo.RecordObject(settings, "Discover Master Data Sheet Bindings");

        foreach (var guid in assetGuids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(assetPath) || existingAssetPaths.Contains(assetPath))
            {
                continue;
            }

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null || GetMasterRowType(asset.GetType()) == null)
            {
                continue;
            }

            settings.Bindings.Add(new GoogleSheetsMasterDataBinding
            {
                Enabled = true,
                MasterAsset = asset,
                SheetName = asset.name,
            });
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    public static async Task ExportBindingAsync(
        GoogleSheetsMasterDataSyncSettings settings,
        GoogleSheetsMasterDataBinding binding)
    {
        ValidateSettings(settings);
        ValidateBinding(binding);

        var asset = binding.MasterAsset;
        var rowType = GetMasterRowType(asset.GetType())
                      ?? throw new InvalidOperationException($"{asset.GetType().Name} is not a MasterAsset<T>.");
        var rows = BuildRowsForExport(asset, rowType);
        var accessToken = await GetAccessTokenAsync(settings);
        await EnsureSheetExistsAsync(settings.SpreadsheetId, binding.SheetName, accessToken, createIfMissing: true);
        await ClearSheetAsync(settings.SpreadsheetId, binding.SheetName, accessToken);
        await UpdateSheetAsync(settings.SpreadsheetId, binding.SheetName, rows, accessToken);
        Debug.Log($"Exported {rows.Count - 1} rows from {asset.name} to Google Sheet '{binding.SheetName}'.", asset);
    }

    public static async Task ImportBindingAsync(
        GoogleSheetsMasterDataSyncSettings settings,
        GoogleSheetsMasterDataBinding binding)
    {
        ValidateSettings(settings);
        ValidateBinding(binding);

        var asset = binding.MasterAsset;
        var rowType = GetMasterRowType(asset.GetType())
                      ?? throw new InvalidOperationException($"{asset.GetType().Name} is not a MasterAsset<T>.");
        var accessToken = await GetAccessTokenAsync(settings);
        await EnsureSheetExistsAsync(settings.SpreadsheetId, binding.SheetName, accessToken, createIfMissing: false);
        var rows = await ReadSheetAsync(settings.SpreadsheetId, binding.SheetName, accessToken);
        var importedItems = BuildObjectsForImport(rowType, rows);
        OverwriteMasterAsset(asset, rowType, importedItems);
        Debug.Log($"Imported {importedItems.Count} rows from Google Sheet '{binding.SheetName}' into {asset.name}.", asset);
    }

    public static string DescribeBinding(GoogleSheetsMasterDataBinding binding)
    {
        if (binding?.MasterAsset == null)
        {
            return "Missing asset";
        }

        var rowType = GetMasterRowType(binding.MasterAsset.GetType());
        var rowTypeName = rowType?.Name ?? "Unknown";
        return $"{binding.MasterAsset.name} ({rowTypeName}) -> {binding.SheetName}";
    }

    private static void ValidateSettings(GoogleSheetsMasterDataSyncSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(settings.SpreadsheetId))
        {
            throw new InvalidOperationException("Spreadsheet ID is required.");
        }

        if (settings.ServiceAccountCredentialsJsonAsset == null &&
            string.IsNullOrWhiteSpace(settings.ServiceAccountCredentialsJsonFilePath))
        {
            throw new InvalidOperationException("Assign a service account JSON asset or a service account JSON file path.");
        }

        if (settings.ServiceAccountCredentialsJsonAsset == null &&
            !File.Exists(ResolveCredentialsJsonFilePath(settings.ServiceAccountCredentialsJsonFilePath)))
        {
            throw new InvalidOperationException(
                $"The service account JSON file does not exist: {ResolveCredentialsJsonFilePath(settings.ServiceAccountCredentialsJsonFilePath)}");
        }
    }

    private static void ValidateBinding(GoogleSheetsMasterDataBinding binding)
    {
        if (binding == null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        if (binding.MasterAsset == null)
        {
            throw new InvalidOperationException("Binding is missing a master asset.");
        }

        if (string.IsNullOrWhiteSpace(binding.SheetName))
        {
            throw new InvalidOperationException($"Binding for {binding.MasterAsset.name} is missing a sheet name.");
        }
    }

    private static async Task<string> GetAccessTokenAsync(GoogleSheetsMasterDataSyncSettings settings)
    {
        var credentials = LoadCredentials(settings);
        var now = DateTimeOffset.UtcNow;
        var jwt = CreateSignedJwt(credentials, now);
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = jwt,
            }),
        };

        using var response = await HttpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to fetch Google access token: {response.StatusCode}\n{responseText}");
        }

        var responseData = Json.Deserialize(responseText) as Dictionary<string, object>;
        if (responseData == null || !responseData.TryGetValue("access_token", out var tokenValue))
        {
            throw new InvalidOperationException("Google token response did not contain an access_token.");
        }

        return tokenValue as string;
    }

    private static async Task<List<List<string>>> ReadSheetAsync(string spreadsheetId, string sheetName, string accessToken)
    {
        var url = $"{SheetsApiBaseUrl}/{spreadsheetId}/values/{Uri.EscapeDataString(BuildWholeSheetRange(sheetName))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await HttpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to read Google Sheet '{sheetName}': {response.StatusCode}\n{responseText}");
        }

        var rows = new List<List<string>>();
        var responseData = Json.Deserialize(responseText) as Dictionary<string, object>;
        if (responseData == null || !responseData.TryGetValue("values", out var valuesObject))
        {
            return rows;
        }

        if (valuesObject is not List<object> rawRows)
        {
            return rows;
        }

        foreach (var rowObject in rawRows)
        {
            var row = new List<string>();
            if (rowObject is List<object> rawCells)
            {
                foreach (var cellObject in rawCells)
                {
                    row.Add(cellObject?.ToString() ?? string.Empty);
                }
            }
            rows.Add(row);
        }

        return rows;
    }

    private static async Task ClearSheetAsync(string spreadsheetId, string sheetName, string accessToken)
    {
        var url = $"{SheetsApiBaseUrl}/{spreadsheetId}/values/{Uri.EscapeDataString(BuildWholeSheetRange(sheetName))}:clear";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await HttpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to clear Google Sheet '{sheetName}': {response.StatusCode}\n{responseText}");
        }
    }

    private static async Task EnsureSheetExistsAsync(string spreadsheetId, string sheetName, string accessToken, bool createIfMissing)
    {
        var sheetTitles = await GetSheetTitlesAsync(spreadsheetId, accessToken);
        if (sheetTitles.Contains(sheetName))
        {
            return;
        }

        if (!createIfMissing)
        {
            throw new InvalidOperationException(
                $"The spreadsheet does not contain a sheet tab named '{sheetName}'. Create the tab or update the binding.");
        }

        await CreateSheetAsync(spreadsheetId, sheetName, accessToken);
    }

    private static async Task<HashSet<string>> GetSheetTitlesAsync(string spreadsheetId, string accessToken)
    {
        var url = $"{SheetsApiBaseUrl}/{spreadsheetId}?fields=sheets.properties.title";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await HttpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to read spreadsheet metadata: {response.StatusCode}\n{responseText}");
        }

        var responseData = Json.Deserialize(responseText) as Dictionary<string, object>;
        if (responseData == null || !responseData.TryGetValue("sheets", out var sheetsObject) || sheetsObject is not List<object> sheets)
        {
            throw new InvalidOperationException("Spreadsheet metadata did not contain any sheet information.");
        }

        var sheetTitles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheetObject in sheets)
        {
            if (sheetObject is not Dictionary<string, object> sheetData ||
                !sheetData.TryGetValue("properties", out var propertiesObject) ||
                propertiesObject is not Dictionary<string, object> properties ||
                !properties.TryGetValue("title", out var titleObject))
            {
                continue;
            }

            sheetTitles.Add(titleObject?.ToString() ?? string.Empty);
        }

        return sheetTitles;
    }

    private static async Task CreateSheetAsync(string spreadsheetId, string sheetName, string accessToken)
    {
        var url = $"{SheetsApiBaseUrl}/{spreadsheetId}:batchUpdate";
        var payload = Json.Serialize(new Dictionary<string, object>
        {
            ["requests"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["addSheet"] = new Dictionary<string, object>
                    {
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["title"] = sheetName,
                        },
                    },
                },
            },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await HttpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to create Google Sheet '{sheetName}': {response.StatusCode}\n{responseText}");
        }
    }

    private static async Task UpdateSheetAsync(
        string spreadsheetId,
        string sheetName,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string accessToken)
    {
        var range = $"{BuildWholeSheetRange(sheetName)}!A1";
        var url = $"{SheetsApiBaseUrl}/{spreadsheetId}/values/{Uri.EscapeDataString(range)}?valueInputOption=RAW";
        var payload = Json.Serialize(new Dictionary<string, object>
        {
            ["range"] = range,
            ["majorDimension"] = "ROWS",
            ["values"] = rows.Select(row => row.Cast<object>().ToList()).Cast<object>().ToList(),
        });

        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await HttpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to update Google Sheet '{sheetName}': {response.StatusCode}\n{responseText}");
        }
    }

    private static List<IReadOnlyList<string>> BuildRowsForExport(ScriptableObject asset, Type rowType)
    {
        var fields = GetSerializableFields(rowType).ToArray();
        var dataField = GetDataField(asset.GetType());
        var items = (IList)dataField.GetValue(asset);

        var rows = new List<IReadOnlyList<string>>
        {
            fields.Select(GetColumnHeader).ToArray()
        };

        foreach (var item in items)
        {
            var row = new string[fields.Length];
            for (var i = 0; i < fields.Length; i++)
            {
                row[i] = ConvertFieldValueToCell(fields[i].GetValue(item), fields[i].FieldType);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static IList BuildObjectsForImport(Type rowType, List<List<string>> rows)
    {
        var result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType));
        var fields = GetSerializableFields(rowType).ToArray();
        if (rows.Count == 0)
        {
            return result;
        }

        var header = rows[0];
        var fieldMap = BuildFieldHeaderMap(fields);

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var rowValues = rows[rowIndex];
            if (rowValues.Count == 0 || rowValues.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var item = Activator.CreateInstance(rowType);
            for (var columnIndex = 0; columnIndex < header.Count; columnIndex++)
            {
                var columnName = header[columnIndex]?.Trim() ?? string.Empty;
                if (!fieldMap.TryGetValue(columnName, out var field))
                {
                    continue;
                }

                var rawValue = columnIndex < rowValues.Count ? rowValues[columnIndex] : string.Empty;
                var parsedValue = ParseCellValue(rawValue, field.FieldType);
                field.SetValue(item, parsedValue);
            }

            result.Add(item);
        }

        return result;
    }

    private static void OverwriteMasterAsset(ScriptableObject asset, Type rowType, IList importedItems)
    {
        var dataField = GetDataField(asset.GetType());
        var newList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rowType));
        foreach (var item in importedItems)
        {
            newList.Add(item);
        }

        Undo.RecordObject(asset, $"Import {asset.name} from Google Sheets");
        dataField.SetValue(asset, newList);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
    }

    private static GoogleServiceAccountCredentials LoadCredentials(GoogleSheetsMasterDataSyncSettings settings)
    {
        var json = settings.ServiceAccountCredentialsJsonAsset != null
            ? settings.ServiceAccountCredentialsJsonAsset.text
            : File.ReadAllText(ResolveCredentialsJsonFilePath(settings.ServiceAccountCredentialsJsonFilePath));

        var root = Json.Deserialize(json) as Dictionary<string, object>;
        if (root == null)
        {
            throw new InvalidOperationException("Failed to parse service account credentials JSON.");
        }

        return new GoogleServiceAccountCredentials
        {
            ClientEmail = root.TryGetValue("client_email", out var clientEmail) ? clientEmail as string : null,
            PrivateKey = root.TryGetValue("private_key", out var privateKey) ? privateKey as string : null,
            PrivateKeyId = root.TryGetValue("private_key_id", out var privateKeyId) ? privateKeyId as string : null,
        };
    }

    private static string CreateSignedJwt(GoogleServiceAccountCredentials credentials, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(credentials.ClientEmail))
        {
            throw new InvalidOperationException("The service account JSON is missing client_email.");
        }

        if (string.IsNullOrWhiteSpace(credentials.PrivateKey))
        {
            throw new InvalidOperationException("The service account JSON is missing private_key.");
        }

        var headerJson = Json.Serialize(new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = credentials.PrivateKeyId ?? string.Empty,
        });
        var claimJson = Json.Serialize(new Dictionary<string, object>
        {
            ["iss"] = credentials.ClientEmail,
            ["scope"] = SheetsScope,
            ["aud"] = TokenEndpoint,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(55).ToUnixTimeSeconds(),
        });

        var encodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var encodedClaim = Base64UrlEncode(Encoding.UTF8.GetBytes(claimJson));
        var payload = $"{encodedHeader}.{encodedClaim}";

        using var rsa = RSA.Create();
        var privateKeyBytes = DecodePemPrivateKey(credentials.PrivateKey);
        rsa.ImportParameters(ReadPkcs8PrivateKey(privateKeyBytes));
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(payload),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{payload}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] DecodePemPrivateKey(string pem)
    {
        var normalized = pem
            .Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
            .Replace("-----END PRIVATE KEY-----", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();
        return Convert.FromBase64String(normalized);
    }

    private static RSAParameters ReadPkcs8PrivateKey(byte[] pkcs8Bytes)
    {
        var reader = new DerReader(pkcs8Bytes);
        using var privateKeyInfo = reader.ReadSequence();
        privateKeyInfo.ReadInteger();
        using var algorithmIdentifier = privateKeyInfo.ReadSequence();
        algorithmIdentifier.ReadObjectIdentifier();
        if (algorithmIdentifier.HasData)
        {
            algorithmIdentifier.SkipValue();
        }

        var privateKeyBytes = privateKeyInfo.ReadOctetString();
        if (privateKeyInfo.HasData)
        {
            throw new InvalidOperationException("Unexpected data found after the PKCS#8 private key payload.");
        }

        return ReadPkcs1PrivateKey(privateKeyBytes);
    }

    private static RSAParameters ReadPkcs1PrivateKey(byte[] pkcs1Bytes)
    {
        var reader = new DerReader(pkcs1Bytes);
        using var privateKey = reader.ReadSequence();
        privateKey.ReadInteger();

        var parameters = new RSAParameters
        {
            Modulus = privateKey.ReadIntegerBytes(),
            Exponent = privateKey.ReadIntegerBytes(),
            D = privateKey.ReadIntegerBytes(),
            P = privateKey.ReadIntegerBytes(),
            Q = privateKey.ReadIntegerBytes(),
            DP = privateKey.ReadIntegerBytes(),
            DQ = privateKey.ReadIntegerBytes(),
            InverseQ = privateKey.ReadIntegerBytes(),
        };

        if (privateKey.HasData)
        {
            throw new InvalidOperationException("Unexpected data found after the PKCS#1 RSA private key.");
        }

        return parameters;
    }

    private static FieldInfo GetDataField(Type assetType)
    {
        var field = FindField(assetType, "_data");
        if (field == null)
        {
            throw new InvalidOperationException($"{assetType.Name} does not expose the expected _data field.");
        }

        return field;
    }

    private static Type GetMasterRowType(Type assetType)
    {
        while (assetType != null)
        {
            if (assetType.IsGenericType && assetType.GetGenericTypeDefinition() == typeof(MasterAsset<>))
            {
                return assetType.GetGenericArguments()[0];
            }

            assetType = assetType.BaseType;
        }

        return null;
    }

    private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
    {
        if (type == null || type == typeof(object))
        {
            yield break;
        }

        foreach (var baseField in GetSerializableFields(type.BaseType))
        {
            yield return baseField;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var fields = type
            .GetFields(flags)
            .Where(field => !field.IsStatic)
            .Where(field => !field.IsNotSerialized)
            .Where(field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
            .OrderBy(field => field.MetadataToken);

        foreach (var field in fields)
        {
            yield return field;
        }
    }

    private static FieldInfo FindField(Type type, string fieldName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        while (type != null)
        {
            var field = type.GetField(fieldName, flags);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    private static Dictionary<string, FieldInfo> BuildFieldHeaderMap(IEnumerable<FieldInfo> fields)
    {
        var fieldMap = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            RegisterFieldHeader(fieldMap, field.Name, field);
            RegisterFieldHeader(fieldMap, GetColumnHeader(field), field);
        }

        return fieldMap;
    }

    private static void RegisterFieldHeader(IDictionary<string, FieldInfo> fieldMap, string header, FieldInfo field)
    {
        if (string.IsNullOrWhiteSpace(header) || fieldMap.ContainsKey(header))
        {
            return;
        }

        fieldMap.Add(header, field);
    }

    private static string GetColumnHeader(FieldInfo field)
    {
        return field.Name.StartsWith("_", StringComparison.Ordinal) && field.Name.Length > 1
            ? field.Name.Substring(1)
            : field.Name;
    }

    private static string ConvertFieldValueToCell(object value, Type fieldType)
    {
        if (value == null)
        {
            return string.Empty;
        }

        var effectiveType = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
        if (effectiveType == typeof(string))
        {
            return (string)value;
        }

        if (effectiveType.IsEnum)
        {
            return value.ToString();
        }

        if (effectiveType == typeof(bool))
        {
            return ((bool)value) ? "true" : "false";
        }

        if (effectiveType.IsPrimitive || effectiveType == typeof(decimal))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return SerializeComplexValue(value, effectiveType);
    }

    private static object ParseCellValue(string rawValue, Type targetType)
    {
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType == typeof(string))
        {
            return rawValue ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return CreateDefaultValue(effectiveType);
        }

        if (effectiveType.IsEnum)
        {
            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var enumInt))
            {
                return Enum.ToObject(effectiveType, enumInt);
            }

            return Enum.Parse(effectiveType, rawValue, true);
        }

        if (effectiveType == typeof(bool))
        {
            if (rawValue == "1")
            {
                return true;
            }

            if (rawValue == "0")
            {
                return false;
            }

            return bool.Parse(rawValue);
        }

        if (effectiveType == typeof(int))
        {
            return int.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (effectiveType == typeof(long))
        {
            return long.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (effectiveType == typeof(float))
        {
            return float.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (effectiveType == typeof(double))
        {
            return double.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (effectiveType == typeof(decimal))
        {
            return decimal.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (effectiveType == typeof(short))
        {
            return short.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (effectiveType == typeof(byte))
        {
            return byte.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        return DeserializeComplexValue(rawValue, effectiveType);
    }

    private static object CreateDefaultValue(Type type)
    {
        if (type == typeof(string))
        {
            return string.Empty;
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType(), 0);
        }

        if (TryGetListElementType(type, out var elementType))
        {
            return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static string SerializeComplexValue(object value, Type type)
    {
        return Json.Serialize(ToJsonObject(value, type));
    }

    private static object ToJsonObject(object value, Type type)
    {
        if (value == null)
        {
            return null;
        }

        var effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (effectiveType == typeof(string))
        {
            return value;
        }

        if (effectiveType.IsEnum)
        {
            return value.ToString();
        }

        switch (Type.GetTypeCode(effectiveType))
        {
            case TypeCode.Boolean:
                return (bool)value;
            case TypeCode.Byte:
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            case TypeCode.Int16:
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            case TypeCode.Int32:
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            case TypeCode.Int64:
                return (long)value;
            case TypeCode.Single:
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            case TypeCode.Double:
                return (double)value;
            case TypeCode.Decimal:
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        if (effectiveType.IsArray)
        {
            var array = (Array)value;
            var result = new List<object>(array.Length);
            foreach (var item in array)
            {
                result.Add(ToJsonObject(item, effectiveType.GetElementType()));
            }
            return result;
        }

        if (TryGetListElementType(effectiveType, out var elementType))
        {
            var result = new List<object>();
            foreach (var item in (IEnumerable)value)
            {
                result.Add(ToJsonObject(item, elementType));
            }
            return result;
        }

        var objectResult = new Dictionary<string, object>();
        foreach (var field in GetSerializableFields(effectiveType))
        {
            objectResult[field.Name] = ToJsonObject(field.GetValue(value), field.FieldType);
        }
        return objectResult;
    }

    private static object DeserializeComplexValue(string json, Type targetType)
    {
        var jsonValue = Json.Deserialize(json);
        return FromJsonObject(jsonValue, targetType);
    }

    private static object FromJsonObject(object jsonValue, Type targetType)
    {
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (jsonValue == null)
        {
            return CreateDefaultValue(effectiveType);
        }

        if (effectiveType == typeof(string))
        {
            return jsonValue.ToString();
        }

        if (effectiveType.IsEnum)
        {
            if (jsonValue is string enumString)
            {
                return Enum.Parse(effectiveType, enumString, true);
            }

            return Enum.ToObject(effectiveType, Convert.ToInt32(jsonValue, CultureInfo.InvariantCulture));
        }

        switch (Type.GetTypeCode(effectiveType))
        {
            case TypeCode.Boolean:
                return Convert.ToBoolean(jsonValue, CultureInfo.InvariantCulture);
            case TypeCode.Byte:
                return Convert.ToByte(jsonValue, CultureInfo.InvariantCulture);
            case TypeCode.Int16:
                return Convert.ToInt16(jsonValue, CultureInfo.InvariantCulture);
            case TypeCode.Int32:
                return Convert.ToInt32(jsonValue, CultureInfo.InvariantCulture);
            case TypeCode.Int64:
                return Convert.ToInt64(jsonValue, CultureInfo.InvariantCulture);
            case TypeCode.Single:
                return Convert.ToSingle(jsonValue, CultureInfo.InvariantCulture);
            case TypeCode.Double:
                return Convert.ToDouble(jsonValue, CultureInfo.InvariantCulture);
            case TypeCode.Decimal:
                return Convert.ToDecimal(jsonValue, CultureInfo.InvariantCulture);
        }

        if (effectiveType.IsArray)
        {
            var elementType = effectiveType.GetElementType();
            var sourceList = jsonValue as List<object> ?? new List<object>();
            var items = sourceList
                .Select(child => FromJsonObject(child, elementType))
                .ToArray();
            var array = Array.CreateInstance(elementType, items.Length);
            for (var i = 0; i < items.Length; i++)
            {
                array.SetValue(items[i], i);
            }
            return array;
        }

        if (TryGetListElementType(effectiveType, out var listElementType))
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(listElementType));
            var sourceList = jsonValue as List<object> ?? new List<object>();
            foreach (var child in sourceList)
            {
                list.Add(FromJsonObject(child, listElementType));
            }
            return list;
        }

        var instance = Activator.CreateInstance(effectiveType);
        var sourceObject = jsonValue as Dictionary<string, object>;
        foreach (var field in GetSerializableFields(effectiveType))
        {
            if (sourceObject == null || !sourceObject.TryGetValue(field.Name, out var propertyValue))
            {
                continue;
            }

            var fieldValue = FromJsonObject(propertyValue, field.FieldType);
            field.SetValue(instance, fieldValue);
        }
        return instance;
    }

    private static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    private static string BuildWholeSheetRange(string sheetName)
    {
        var escapedSheetName = sheetName.Replace("'", "''");
        return $"'{escapedSheetName}'";
    }

    private static string ResolveCredentialsJsonFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    private sealed class GoogleServiceAccountCredentials
    {
        public string ClientEmail;
        public string PrivateKey;
        public string PrivateKeyId;
    }

    private sealed class DerReader : IDisposable
    {
        private readonly byte[] _data;
        private int _offset;

        public DerReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public bool HasData => _offset < _data.Length;

        public DerReader ReadSequence()
        {
            return new DerReader(ReadValue(0x30));
        }

        public void ReadInteger()
        {
            ReadValue(0x02);
        }

        public byte[] ReadIntegerBytes()
        {
            var value = ReadValue(0x02);
            var firstNonZeroIndex = 0;
            while (firstNonZeroIndex < value.Length - 1 && value[firstNonZeroIndex] == 0)
            {
                firstNonZeroIndex++;
            }

            var trimmedLength = value.Length - firstNonZeroIndex;
            var trimmed = new byte[trimmedLength];
            Buffer.BlockCopy(value, firstNonZeroIndex, trimmed, 0, trimmedLength);
            return trimmed;
        }

        public void ReadObjectIdentifier()
        {
            ReadValue(0x06);
        }

        public byte[] ReadOctetString()
        {
            return ReadValue(0x04);
        }

        public void SkipValue()
        {
            ReadTag();
            var length = ReadLength();
            EnsureAvailable(length);
            _offset += length;
        }

        public void Dispose()
        {
        }

        private byte[] ReadValue(byte expectedTag)
        {
            var actualTag = ReadTag();
            if (actualTag != expectedTag)
            {
                throw new InvalidOperationException($"Unexpected ASN.1 tag. Expected 0x{expectedTag:X2}, got 0x{actualTag:X2}.");
            }

            var length = ReadLength();
            EnsureAvailable(length);
            var value = new byte[length];
            Buffer.BlockCopy(_data, _offset, value, 0, length);
            _offset += length;
            return value;
        }

        private byte ReadTag()
        {
            EnsureAvailable(1);
            return _data[_offset++];
        }

        private int ReadLength()
        {
            EnsureAvailable(1);
            var firstByte = _data[_offset++];
            if ((firstByte & 0x80) == 0)
            {
                return firstByte;
            }

            var byteCount = firstByte & 0x7F;
            if (byteCount == 0 || byteCount > 4)
            {
                throw new InvalidOperationException("Unsupported ASN.1 length encoding.");
            }

            EnsureAvailable(byteCount);
            var length = 0;
            for (var i = 0; i < byteCount; i++)
            {
                length = (length << 8) | _data[_offset++];
            }

            return length;
        }

        private void EnsureAvailable(int count)
        {
            if (_offset + count > _data.Length)
            {
                throw new InvalidOperationException("Unexpected end of ASN.1 data.");
            }
        }
    }
}
