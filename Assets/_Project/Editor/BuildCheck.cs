using System;
using System.IO;
using EdgeChronicles.Tools.Editor.Import;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批处理自证入口。由 Tools/ci/check-*.sh 调用。
/// <para>
/// 命名空间保持全局（不带 EdgeChronicles 前缀）：CI 脚本按
/// <c>-executeMethod BuildCheck.CompileCheck</c> 调用，改名会打断现有脚本契约。
/// </para>
/// </summary>
public static class BuildCheck
{
    /// <summary>源数据目录（入库的是源数据）。</summary>
    private const string SourceDir = "Assets/_Project/Data";

    /// <summary>生成物目录（已入 .gitignore）。</summary>
    private const string GeneratedDir = "Assets/Generated";

    /// <summary>编译自证：Unity 编译失败时会在日志中留下 error CS，脚本据此判失败。</summary>
    public static void CompileCheck()
    {
        Debug.Log("[CHECK] compile-ok");
        EditorApplication.Exit(0);
    }

    /// <summary>
    /// 配表自证：检查<b>生成物</b>目录下的 json 是否存在且结构完整。
    /// <para>
    /// 源数据目录（<c>Assets/_Project/Data</c>）的完整性由导表器负责（缺源数据即 error），
    /// 这里的检查对象是<b>导表产物</b>（<c>Assets/Generated</c>）。
    /// </para>
    /// </summary>
    public static void DataCheck()
    {
        if (!Directory.Exists(GeneratedDir))
        {
            Debug.LogWarning("[CHECK] data-skip: " + GeneratedDir + " 尚未生成（导表器未产出）");
            EditorApplication.Exit(0);
            return;
        }

        int bad = 0;
        int count = 0;
        foreach (string f in Directory.GetFiles(GeneratedDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            count++;
            string text = File.ReadAllText(f);
            string trimmed = text.Trim();
            bool ok = trimmed.StartsWith("{") && trimmed.EndsWith("}") && BracesBalanced(text);
            if (!ok)
            {
                Debug.LogError("[CHECK] data-bad: " + f);
                bad++;
            }
        }

        if (count == 0)
        {
            Debug.LogError("[CHECK] data-fail: 生成物目录里没有任何 json");
            EditorApplication.Exit(1);
            return;
        }

        if (bad > 0)
        {
            Debug.LogError("[CHECK] data-fail: " + bad + " 个文件结构非法");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[CHECK] data-ok: " + count + " 个生成文件");
        EditorApplication.Exit(0);
    }

    private static bool BracesBalanced(string s)
    {
        int depth = 0;
        bool inString = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"' && (i == 0 || s[i - 1] != '\\')) inString = !inString;
            else if (!inString)
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
            }
        }
        return depth == 0;
    }
}
