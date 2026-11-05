using System.Collections.Generic;
using System.IO;
using AThousandDawns.Data;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 구글 시트에서 내려받은 CSV(Assets/_Game/Data/Csv)를 ScriptableObject(Assets/_Game/Data/Generated)로 바꿔요.
    public static class SheetImporter
    {
        public const string CsvFolder = "Assets/_Game/Data/Csv";
        public const string GeneratedFolder = "Assets/_Game/Data/Generated";
        public const string BalanceCsvPath = CsvFolder + "/data_balance.csv";
        public const string BalanceAssetPath = GeneratedFolder + "/BalanceConfig.asset";

        [MenuItem("AThousandDawns/시트 가져오기")]
        public static void ImportAll()
        {
            bool ok = ImportBalance();
            AssetDatabase.SaveAssets();
            if (ok) Debug.Log("[시트 가져오기] 끝났어요.");
        }

        // 배치 모드용: -executeMethod AThousandDawns.EditorTools.SheetImporter.ImportAllBatch
        public static void ImportAllBatch()
        {
            AssetDatabase.Refresh();
            bool ok = ImportBalance();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool ImportBalance()
        {
            if (!File.Exists(BalanceCsvPath))
            {
                Debug.LogWarning($"[시트 가져오기] {BalanceCsvPath} 파일이 없어요. 시트의 data_balance 탭을 CSV로 내려받아 넣어 주세요.");
                return false;
            }

            var errors = new List<string>();
            var entries = BalanceTable.Read(File.ReadAllText(BalanceCsvPath), errors);
            foreach (var e in errors) Debug.LogError($"[시트 가져오기] {e}");
            if (errors.Count > 0) return false;

            EnsureFolder(GeneratedFolder);
            var asset = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalanceAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BalanceConfig>();
                AssetDatabase.CreateAsset(asset, BalanceAssetPath);
            }
            asset.SetEntries(entries);
            EditorUtility.SetDirty(asset);
            Debug.Log($"[시트 가져오기] data_balance: 상수 {entries.Count}개");
            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    // data_balance.csv를 덮어쓰면 (시트에서 내려받아 넣거나 git으로 받아도) 메뉴를 누르지 않아도 다시 가져와요.
    sealed class BalanceCsvWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Application.isBatchMode) return; // 배치 모드는 ImportAllBatch·씬 만들기가 직접 가져와요
            foreach (var path in importedAssets)
            {
                if (path != SheetImporter.BalanceCsvPath) continue;
                EditorApplication.delayCall += SheetImporter.ImportAll; // 가져오기 도중에 에셋을 만들지 않게 한 박자 늦춰요
                return;
            }
        }
    }
}
