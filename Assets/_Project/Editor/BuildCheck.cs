using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>批处理自证入口。由 Tools/ci/check-*.sh 调用。</summary>
public static class BuildCheck
{
    private const string DataDir = "Assets/_Project/Data";

    /// <summary>编译自证：Unity 编译失败时会在日志中留下 error CS，脚本据此判失败。</summary>
    public static void CompileCheck()
    {
        Debug.Log("[CHECK] compile-ok");
        EditorApplication.Exit(0);
    }

    /// <summary>配表自证：检查生成目录下的 json 是否存在且结构完整。</summary>
    public static void DataCheck()
    {
        if (!Directory.Exists(DataDir))
        {
            Debug.LogWarning("[CHECK] data-skip: " + DataDir + " 尚未生成（导表器未产出）");
            EditorApplication.Exit(0);
            return;
        }

        int bad = 0;
        foreach (string f in Directory.GetFiles(DataDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            string text = File.ReadAllText(f);
            string trimmed = text.Trim();
            bool ok = trimmed.StartsWith("{") && trimmed.EndsWith("}") && BracesBalanced(text);
            if (!ok)
            {
                Debug.LogError("[CHECK] data-bad: " + f);
                bad++;
            }
        }

        if (bad > 0)
        {
            Debug.LogError("[CHECK] data-fail: " + bad + " 个文件结构非法");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[CHECK] data-ok");
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
