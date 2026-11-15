using EdgeChronicles.Tools.Editor.Import;
using UnityEditor;
using UnityEngine;

namespace EdgeChronicles.Tools.Editor.Import
{
    /// <summary>
    /// 导表菜单入口（Editor 内手动跑；CI 走 <c>EdgeChronicles.Tools.Editor.Ci.*</c>）。
    /// <para>
    /// 为什么需要菜单入口：Unity Editor 打开工程时 batchmode 会因工程被占用而失败，
    /// 因此「开着 Editor 时自证」必须有一条不依赖 batchmode 的路径。
    /// </para>
    /// </summary>
    public static class DataMenu
    {
        /// <summary>导表 + 全断言（L1 + L2）。</summary>
        [MenuItem("EdgeChronicles/导表与校验（全断言）", false, 100)]
        public static void ImportAndValidate()
        {
            var outcome = TableImportPipeline.Run(TableImportPipeline.SourceDir, TableImportPipeline.OutputDir, true);
            Ci.ReportWriter.Print("import", outcome);

            if (outcome.Succeeded)
            {
                Debug.Log($"[CHECK] 导表通过：{outcome.Report.AssertionSummaryLine()}");
            }
            else
            {
                Debug.LogError($"[CHECK] 导表失败：{outcome.Report.SummaryLine()}（未生成产物）");
            }
        }

        /// <summary>只跑断言，不写生成物。</summary>
        [MenuItem("EdgeChronicles/只跑断言（不生成产物）", false, 101)]
        public static void AuditOnly()
        {
            var outcome = TableImportPipeline.Run(TableImportPipeline.SourceDir, null, false);
            Ci.ReportWriter.Print("audit", outcome);
        }

        /// <summary>打开源数据目录。</summary>
        [MenuItem("EdgeChronicles/打开源数据目录", false, 200)]
        public static void OpenSourceDir()
        {
            EditorUtility.OpenWithDefaultApp(TableImportPipeline.SourceDir);
        }
    }
}
