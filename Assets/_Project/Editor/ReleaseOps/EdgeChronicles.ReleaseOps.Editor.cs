// EdgeChronicles.ReleaseOps · 构建入口与资产 Gate
//
// 归属：Assets/** 由工程线与美术线所有，本文件不在工程目录常驻。
//       由 Tools/build/build-player.sh 与 Tools/build/lint-assets.sh 在需要时临时注入，
//       执行完立即清理，保证不与并行改动冲突、不污染工作树。
//
// 命令行约定（Unity 会把自定义 -key value 原样传给进程）：
//   -executeMethod EdgeChronicles.ReleaseOps.PlayerBuild.Build
//     -buildTier low|medium|high
//     -buildOutput <绝对输出目录>
//     -buildBackend il2cpp|mono
//     -buildTarget StandaloneWindows64|StandaloneLinux64|StandaloneOSX
//     -buildVersion <版本号>
//     -buildParams <机型基线摊平后的 key=value 文件>
//     -buildScenes <逗号分隔场景路径>
//     -buildStrict true|false
//
//   -executeMethod EdgeChronicles.ReleaseOps.AssetGate.Lint
//     -lintBaseline <档位 id>
//     -lintVersion <期望版本号>
//     -lintMaxTextureSize <贴图最大边上限>
//
// 日志约定（供 shell 侧解析）：
//   [BUILD] ok / fail / warn / info
//   [LINT]  PASS / FAIL / WARN / SUMMARY
//
// 兼容性写法：QualitySettings 与 ScriptingImplementation 的成员在不同 Unity 版本间
// 增删过（例：postProcessing、textureMipmapLimit、realtimeLightProbes 在 2022.3 上已不存在，
// masterTextureLimit 已废弃）。因此这些写入一律走反射：存在就写、不存在就跳过并记录，
// 绝不因为某个 API 缺失就让整个构建失败，也绝不静默假装写了。

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdgeChronicles.ReleaseOps
{
    internal static class Cli
    {
        /// <summary>解析 -key value 形式的自定义命令行参数。</summary>
        public static Dictionary<string, string> Parse()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.IsNullOrEmpty(a) || a[0] != '-') continue;
                string key = a.Substring(1);
                string val = string.Empty;
                if (i + 1 < args.Length && (args[i + 1].Length == 0 || args[i + 1][0] != '-'))
                {
                    val = args[i + 1];
                    i++;
                }
                map[key] = val;
            }
            return map;
        }

        public static string Get(Dictionary<string, string> args, string key, string fallback = "")
        {
            string v;
            return args.TryGetValue(key, out v) && !string.IsNullOrEmpty(v) ? v : fallback;
        }

        public static bool GetBool(Dictionary<string, string> args, string key, bool fallback = false)
        {
            string v = Get(args, key);
            if (string.IsNullOrEmpty(v)) return fallback;
            return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
        }

        /// <summary>读取摊平后的 key=value 机型参数文件。</summary>
        public static Dictionary<string, string> LoadKeyValues(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return map;
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                map[raw.Substring(0, eq).Trim()] = raw.Substring(eq + 1).Trim();
            }
            return map;
        }
    }

    /// <summary>机型基线参数读取器：按点号路径取值，缺省值显式给出。</summary>
    internal sealed class TierParams
    {
        private readonly Dictionary<string, string> _map;

        public TierParams(Dictionary<string, string> map) { _map = map ?? new Dictionary<string, string>(); }

        public bool Has(string path) { return _map.ContainsKey(path); }

        public string Str(string path, string fallback)
        {
            string v;
            return _map.TryGetValue(path, out v) && !string.IsNullOrEmpty(v) ? v : fallback;
        }

        public int Int(string path, int fallback)
        {
            string v;
            int r;
            if (_map.TryGetValue(path, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) return r;
            return fallback;
        }

        public bool Bool(string path, bool fallback)
        {
            string v;
            if (_map.TryGetValue(path, out v) && !string.IsNullOrEmpty(v))
                return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
            return fallback;
        }

        public int[] IntArray(string path, int[] fallback)
        {
            string v;
            if (!_map.TryGetValue(path, out v) || string.IsNullOrEmpty(v)) return fallback;
            var parts = v.Split(',');
            var res = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out res[i]))
                    return fallback;
            return res;
        }
    }

    /// <summary>反射写入工具：属性不存在时跳过并记录，绝不抛异常中断构建。</summary>
    internal static class Compat
    {
        public static readonly List<string> Applied = new List<string>();
        public static readonly List<string> Skipped = new List<string>();

        private static PropertyInfo Find(Type t, string name)
        {
            return t.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        }

        public static void SetEnum(Type t, string prop, string member)
        {
            var pi = Find(t, prop);
            if (pi == null) { Skipped.Add(prop + "（本 Unity 版本无此属性）"); return; }
            if (!pi.PropertyType.IsEnum) { Skipped.Add(prop + "（不是枚举类型）"); return; }
            if (!Enum.IsDefined(pi.PropertyType, member)) { Skipped.Add(prop + "（无枚举值 " + member + "）"); return; }
            pi.SetValue(null, Enum.Parse(pi.PropertyType, member), null);
            Applied.Add(prop + "=" + member);
        }

        public static void SetBool(Type t, string prop, bool value)
        {
            var pi = Find(t, prop);
            if (pi == null || pi.PropertyType != typeof(bool)) { Skipped.Add(prop + "（本 Unity 版本无此属性）"); return; }
            pi.SetValue(null, value, null);
            Applied.Add(prop + "=" + value);
        }

        public static void SetInt(Type t, string prop, int value)
        {
            var pi = Find(t, prop);
            if (pi == null || pi.PropertyType != typeof(int)) { Skipped.Add(prop + "（本 Unity 版本无此属性）"); return; }
            pi.SetValue(null, value, null);
            Applied.Add(prop + "=" + value);
        }

        public static void SetFloat(Type t, string prop, float value)
        {
            var pi = Find(t, prop);
            if (pi == null || pi.PropertyType != typeof(float)) { Skipped.Add(prop + "（本 Unity 版本无此属性）"); return; }
            pi.SetValue(null, value, null);
            Applied.Add(prop + "=" + value);
        }

        /// <summary>在 UnityEngine.Object 上按序列化字段名写值（用于 URP 资产，避免编译期依赖）。</summary>
        public static bool SetSerialized(UnityEngine.Object target, string field, System.Action<SerializedProperty> setter)
        {
            if (target == null) return false;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Skipped.Add("URP." + field + "（本 URP 版本无此字段）"); return false; }
            setter(prop);
            so.ApplyModifiedPropertiesWithoutUndo();
            Applied.Add("URP." + field);
            return true;
        }
    }

    public static class PlayerBuild
    {
        private static readonly List<string> Warnings = new List<string>();

        public static void Build()
        {
            int exitCode = 0;
            try
            {
                Run();
            }
            catch (Exception e)
            {
                Debug.LogError("[BUILD] fail 构建入口异常: " + e);
                exitCode = 5;
            }
            EditorApplication.Exit(exitCode);
        }

        private static void Run()
        {
            var args = Cli.Parse();
            string tier = Cli.Get(args, "buildTier");
            string output = Cli.Get(args, "buildOutput");
            string backend = Cli.Get(args, "buildBackend", "il2cpp");
            string targetName = Cli.Get(args, "buildTarget", "StandaloneWindows64");
            string version = Cli.Get(args, "buildVersion");
            string paramsPath = Cli.Get(args, "buildParams");
            string sceneArg = Cli.Get(args, "buildScenes");
            bool strict = Cli.GetBool(args, "buildStrict", true);

            if (string.IsNullOrEmpty(tier)) throw new Exception("缺少 -buildTier");
            if (string.IsNullOrEmpty(output)) throw new Exception("缺少 -buildOutput");

            BuildTarget target = ParseTarget(targetName);
            var p = new TierParams(Cli.LoadKeyValues(paramsPath));

            string paramTier = p.Str("id", "");
            if (!string.IsNullOrEmpty(paramTier) && paramTier != tier)
                throw new Exception(string.Format("机型参数文件档位({0})与 -buildTier({1}) 不一致", paramTier, tier));

            var scenes = sceneArg.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (scenes.Length == 0) throw new Exception("没有可构建的场景");

            if (!Directory.Exists(output)) Directory.CreateDirectory(output);
            string exeName = target == BuildTarget.StandaloneWindows64 ? "EdgeChronicles.exe"
                           : target == BuildTarget.StandaloneLinux64 ? "EdgeChronicles.x86_64"
                           : "EdgeChronicles.app";
            string location = Path.Combine(output, exeName);

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.Log("[BUILD] info 切换构建目标: " + EditorUserBuildSettings.activeBuildTarget + " -> " + target);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target))
                    throw new Exception("切换构建目标失败: " + targetName);
            }

            // 渲染管线门控：工程规格是 URP，出 Built-in 包属于静默的规格偏离
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null)
            {
                string msg = "当前渲染管线不是 URP（工程规格为 URP），出 Built-in 包会与美术规格不符。";
                if (strict) throw new Exception(msg);
                Warn(msg + " 已按非严格模式继续。");
            }

            ApplyScriptingSettings(backend);
            ApplyPlayerSettings(p, version, target);
            ApplyQualitySettings(p);
            ApplyUrpSettings(p);

            Warn("帧率上限（targetFrameRate=" + p.Int("target.targetFrameRate", 30) +
                 "）需运行期引导脚本生效，该脚本归工程线；构建期已关闭 vSync，避免与上限互相打架。");
            Warn("相机的后期开关（quality.postProcessing）由运行期的相机配置决定，构建期无法写入，已按基线值记录待核对。");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            Debug.Log("[BUILD] info 开始构建: target=" + targetName + " backend=" + backend +
                      " tier=" + tier + " scenes=" + string.Join(",", scenes));

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception(string.Format("构建失败: result={0} error={1} totalSize={2}",
                    summary.result, summary.totalErrors, summary.totalSize));
            }

            WriteResult(output, tier, backend, targetName, version, scenes, summary);
            Debug.Log("[BUILD] ok 构建完成: " + location);
            foreach (string w in Warnings) Debug.Log("[BUILD] warn " + w);
            foreach (string s in Compat.Skipped) Debug.Log("[BUILD] warn 跳过不兼容参数: " + s);
        }

        private static BuildTarget ParseTarget(string name)
        {
            switch (name)
            {
                case "StandaloneWindows64": return BuildTarget.StandaloneWindows64;
                case "StandaloneLinux64": return BuildTarget.StandaloneLinux64;
                case "StandaloneOSX": return BuildTarget.StandaloneOSX;
                default: throw new Exception("不支持的构建目标: " + name);
            }
        }

        private static void ApplyScriptingSettings(string backend)
        {
            ScriptingImplementation impl = ResolveScriptingImplementation(backend == "mono");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, impl);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);
            if (impl == ScriptingImplementation.IL2CPP)
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Standalone, Il2CppCompilerConfiguration.Release);
            Debug.Log("[BUILD] info 脚本后端: " + impl);
        }

        /// <summary>不同 Unity 版本的 ScriptingImplementation 成员名不一致（Mono / Mono2），用反射择一。</summary>
        private static ScriptingImplementation ResolveScriptingImplementation(bool mono)
        {
            Type t = typeof(ScriptingImplementation);
            string[] candidates = mono ? new[] { "Mono2", "Mono" } : new[] { "IL2CPP" };
            foreach (string name in candidates)
            {
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return (ScriptingImplementation)f.GetValue(null);
                }
            }
            throw new Exception("当前 Unity 的 ScriptingImplementation 里找不到 " + string.Join("/", candidates));
        }

        private static void ApplyPlayerSettings(TierParams p, string version, BuildTarget target)
        {
            int[] res = p.IntArray("target.resolution", new[] { 1920, 1080 });
            PlayerSettings.defaultScreenWidth = res.Length > 0 ? res[0] : 1920;
            PlayerSettings.defaultScreenHeight = res.Length > 1 ? res[1] : 1080;

            string fullscreen = p.Str("target.fullscreenMode", "FullScreenWindow");
            switch (fullscreen)
            {
                case "FullScreen": PlayerSettings.fullScreenMode = FullScreenMode.ExclusiveFullScreen; break;
                case "Windowed": PlayerSettings.fullScreenMode = FullScreenMode.Windowed; break;
                default: PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; break;
            }
            PlayerSettings.resizableWindow = p.Bool("target.resizableWindow", false);
            PlayerSettings.runInBackground = p.Bool("target.runInBackground", false);

            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;

            if (target == BuildTarget.StandaloneWindows64)
            {
                // 集显轻薄本是验收底线，固定 DX11，避免落到 DX12 特性集导致集显掉帧
                PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
            }
        }

        private static void ApplyQualitySettings(TierParams p)
        {
            Type qs = typeof(UnityEngine.QualitySettings);

            string levelName = p.Str("quality.unityQualityLevel", "Low");
            int index = QualitySettings.names.Length > 0 ? Array.IndexOf(QualitySettings.names, levelName) : -1;
            if (index < 0)
            {
                Warn("找不到质量档 '" + levelName + "'，沿用当前档位 " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            }
            else
            {
                QualitySettings.SetQualityLevel(index, true);
                Compat.Applied.Add("qualityLevel=" + levelName);
            }

            bool shadows = p.Bool("quality.shadows.enabled", false);
            // QualitySettings.shadows 的类型在不同版本间变过（ShadowQuality / ShadowCastingMode），用反射写
            Compat.SetEnum(qs, "shadows", shadows ? "All" : "Disable");
            Compat.SetFloat(qs, "shadowDistance", p.Int("quality.shadows.distance", 30));
            Compat.SetEnum(qs, "shadowResolution", ToShadowResolution(p.Int("quality.shadows.resolution", 1024)));

            int cascades = p.Int("quality.shadows.cascades", 2);
            Compat.SetInt(qs, "shadowCascades", cascades >= 4 ? 4 : 2); // Unity 只接受 2 / 4

            Compat.SetInt(qs, "antiAliasing", ToAntiAliasing(p.Int("quality.msaa", 1)));
            Compat.SetBool(qs, "vSyncCount", false);
            Compat.SetFloat(qs, "lodBias", p.Int("quality.lodBias", 100) / 100f);
            Compat.SetInt(qs, "maximumLODLevel", p.Int("quality.maximumLODLevel", 0));
            Compat.SetEnum(qs, "anisotropicFiltering", p.Int("quality.anisotropicFiltering", 1) <= 1 ? "Disable" : "ForceEnable");
            Compat.SetInt(qs, "asyncUploadTimeSlice", p.Int("quality.asyncUploadTimeSlice", 2));
            Compat.SetInt(qs, "asyncUploadBufferSize", p.Int("quality.asyncUploadBufferSize", 4));
            Compat.SetBool(qs, "softParticles", p.Bool("quality.softParticles", false));
            Compat.SetBool(qs, "realtimeReflectionProbes", p.Bool("quality.realtimeReflectionProbes", false));

            // vSync 单独写（是 int）
            PropertyInfo vsync = qs.GetProperty("vSyncCount", BindingFlags.Public | BindingFlags.Static);
            if (vsync != null && vsync.PropertyType == typeof(int))
            {
                vsync.SetValue(null, p.Bool("target.vsync", false) ? 1 : 0, null);
                Compat.Applied.Add("vSyncCount=" + (p.Bool("target.vsync", false) ? 1 : 0));
            }

            // 贴图上限：masterTextureLimit 在 2022.3 已废弃（改用 globalTextureMipmapLimit），
            // 两个都尝试写，写不进去的记为跳过。
            Compat.SetInt(qs, "masterTextureLimit", p.Int("quality.masterTextureLimit", 0));
            Compat.SetInt(qs, "globalTextureMipmapLimit", p.Int("quality.textureMipmapLimit", 0));

            Debug.Log("[BUILD] info 质量档: " + levelName +
                      " 阴影=" + shadows +
                      " MSAA=" + p.Int("quality.msaa", 1) +
                      " LODBias=" + p.Int("quality.lodBias", 100) / 100f);
        }

        /// <summary>把档位参数写进 URP 资产（用 SerializedObject 写，避免编译期依赖 URP 类型）。</summary>
        private static void ApplyUrpSettings(TierParams p)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null)
            {
                Warn("没有 URP 管线资产，档位的 URP 层参数（阴影分辨率/附加灯/HDR/渲染缩放）未写入。");
                return;
            }

            float renderScale = p.Int("target.renderScale", 100) / 100f;
            Compat.SetSerialized(rp, "m_RenderScale", sp => sp.floatValue = renderScale);
            Compat.SetSerialized(rp, "m_SupportsHDR", sp => sp.boolValue = p.Bool("quality.hdr", false));
            Compat.SetSerialized(rp, "m_MSAA", sp => sp.intValue = ToAntiAliasing(p.Int("quality.msaa", 1)));
            Compat.SetSerialized(rp, "m_ShadowDistance", sp => sp.floatValue = p.Int("quality.shadows.distance", 30));
            Compat.SetSerialized(rp, "m_ShadowCascadeCount", sp => sp.intValue = p.Int("quality.shadows.cascades", 2) >= 4 ? 4 : 2);
            Compat.SetSerialized(rp, "m_MainLightShadowmapResolution", sp => sp.intValue = p.Int("quality.shadows.resolution", 1024));
            Compat.SetSerialized(rp, "m_SoftShadowsSupported", sp => sp.boolValue = false);
            Compat.SetSerialized(rp, "m_SupportsCameraDepthTexture", sp => sp.boolValue = false);
            Compat.SetSerialized(rp, "m_SupportsCameraOpaqueTexture", sp => sp.boolValue = false);

            // 附加灯：URP 的 RenderingMode 0=Disabled 1=PerVertex 2=PerPixel
            bool perPixel = p.Bool("quality.additionalLights.perPixel", false);
            bool perObject = p.Bool("quality.additionalLights.perObject", false);
            int lightMode = perPixel ? 2 : (perObject ? 1 : 0);
            Compat.SetSerialized(rp, "m_AdditionalLightsRenderingMode", sp => sp.intValue = lightMode);
            Compat.SetSerialized(rp, "m_AdditionalLightsPerObjectLimit", sp => sp.intValue = p.Int("quality.additionalLights.maxCount", 0));
            Compat.SetSerialized(rp, "m_MainLightRenderingMode", sp => sp.intValue = lightMode);
        }

        private static string ToShadowResolution(int res)
        {
            if (res <= 512) return "Low";
            if (res <= 1024) return "Medium";
            if (res <= 2048) return "High";
            return "VeryHigh";
        }

        private static int ToAntiAliasing(int msaa)
        {
            switch (msaa)
            {
                case 1: return 0;
                case 2: return 1;
                case 4: return 2;
                case 8: return 3;
                default: return 0;
            }
        }

        private static void Warn(string msg)
        {
            Warnings.Add(msg);
            Debug.Log("[BUILD] warn " + msg);
        }

        private static void WriteResult(string output, string tier, string backend, string target,
                                         string version, string[] scenes, BuildSummary summary)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"result\": \"succeeded\",\n");
            sb.Append("  \"tier\": \"" + tier + "\",\n");
            sb.Append("  \"scriptingBackend\": \"" + backend + "\",\n");
            sb.Append("  \"buildTarget\": \"" + target + "\",\n");
            sb.Append("  \"version\": \"" + version + "\",\n");
            sb.Append("  \"sceneCount\": " + scenes.Length + ",\n");
            sb.Append("  \"scenes\": \"" + string.Join(",", scenes) + "\",\n");
            sb.Append("  \"totalSizeBytes\": " + summary.totalSize + ",\n");
            sb.Append("  \"totalErrors\": " + summary.totalErrors + ",\n");
            sb.Append("  \"totalWarnings\": " + summary.totalWarnings + ",\n");
            sb.Append("  \"buildDurationSeconds\": " + summary.totalTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + ",\n");
            sb.Append("  \"appliedSettings\": [");
            for (int i = 0; i < Compat.Applied.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"" + Escape(Compat.Applied[i]) + "\"");
            }
            sb.Append("],\n");
            sb.Append("  \"skippedSettings\": [");
            for (int i = 0; i < Compat.Skipped.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"" + Escape(Compat.Skipped[i]) + "\"");
            }
            sb.Append("],\n");
            sb.Append("  \"warnings\": [");
            for (int i = 0; i < Warnings.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"" + Escape(Warnings[i]) + "\"");
            }
            sb.Append("]\n");
            sb.Append("}\n");
            File.WriteAllText(Path.Combine(output, "unity-build-result.json"), sb.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }
    }

    /// <summary>
    /// 资产 Gate：命名 / 贴图规格 / 材质槽 / LFS 边界 / 版本一致性 / 密钥扫描。
    /// 每条判据都可脚本判定，不存在人工确认项。
    /// </summary>
    public static class AssetGate
    {
        private static int _pass, _fail, _warn;

        public static void Lint()
        {
            int exitCode = 0;
            try
            {
                Run();
            }
            catch (Exception e)
            {
                Debug.LogError("[LINT] FAIL G0 资产 Gate 自身异常: " + e);
                exitCode = 7;
            }
            Debug.Log(string.Format("[LINT] SUMMARY pass={0} fail={1} warn={2}", _pass, _fail, _warn));
            if (_fail > 0) exitCode = 7;
            EditorApplication.Exit(exitCode);
        }

        private static void Run()
        {
            var args = Cli.Parse();
            string tier = Cli.Get(args, "lintBaseline", "low");
            string version = Cli.Get(args, "lintVersion");
            int maxTextureSize = 0;
            string maxTexArg = Cli.Get(args, "lintMaxTextureSize");
            if (!string.IsNullOrEmpty(maxTexArg)) int.TryParse(maxTexArg, out maxTextureSize);

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            GateG1_Scenes();
            GateG2_Naming(root);
            GateG3_Textures(root, maxTextureSize);
            GateG4_MaterialSlots(root);
            GateG5_LfsBoundary(root);
            GateG6_MetaPairs(root);
            GateG7_Version(version);
            GateG8_Secrets(root);
            GateG9_LfsPulled(root);
            Debug.Log("[LINT] INFO 档位 " + tier + " 已完成检查");
        }

        private static void GateG1_Scenes()
        {
            int enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Count();
            if (enabled <= 0)
                Fail("G1", "Build Settings 里没有任何启用的场景，出包会得到空包");
            else
                Pass("G1", "启用场景 " + enabled + " 个");
        }

        private static void GateG2_Naming(string root)
        {
            var problems = new List<string>();
            CheckName(root, "Assets/_Project/Art/UI", @"^UI_(ICON|PANEL)_[A-Za-z0-9]+(_[A-Za-z0-9]+)*$", problems);
            CheckName(root, "Assets/_Project/Art/VFX", @"^VFX_[A-Za-z0-9]+(_[A-Za-z0-9]+)*$", problems);
            CheckName(root, "Assets/_Project/Art/Source", @"^UI_(ICON|PANEL)_[A-Za-z0-9]+(_[A-Za-z0-9]+)*$", problems);
            if (problems.Count > 0)
                Fail("G2", "命名不合规 " + problems.Count + " 处：" + string.Join("; ", problems.Take(10).ToArray()));
            else
                Pass("G2", "美术资产命名符合 UI_ICON_/UI_PANEL_/VFX_ 规范");
        }

        private static void CheckName(string root, string relDir, string pattern, List<string> problems)
        {
            string dir = Path.Combine(root, relDir);
            if (!Directory.Exists(dir)) return;
            var rx = new System.Text.RegularExpressions.Regex(pattern);
            foreach (string f in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
            {
                string rel = f.Replace(root + Path.DirectorySeparatorChar, "").Replace('\\', '/');
                if (!rx.IsMatch(Path.GetFileNameWithoutExtension(rel)))
                    problems.Add(rel);
            }
        }

        private static void GateG3_Textures(string root, int maxTextureSize)
        {
            var problems = new List<string>();
            string[] dirs = { "Assets/_Project/Art/Source", "Assets/_Project/Art/UI", "Assets/_Project/Art/VFX" };
            foreach (string rel in dirs)
            {
                string dir = Path.Combine(root, rel);
                if (!Directory.Exists(dir)) continue;
                foreach (string f in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
                {
                    string assetPath = f.Replace(root + Path.DirectorySeparatorChar, "").Replace('\\', '/');
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null) continue;
                    int w = 0, h = 0;
                    importer.GetSourceTextureWidthAndHeight(out w, out h);
                    if (w <= 0 || h <= 0) continue;

                    if (rel.EndsWith("Source") && (w != 1024 || h != 1024))
                        problems.Add(assetPath + " 母版必须是 1024x1024，实际 " + w + "x" + h);
                    if (!rel.EndsWith("Source") && Math.Max(w, h) > 128)
                        problems.Add(assetPath + " 切片最大边不得超过 128，实际 " + Math.Max(w, h));
                    if (maxTextureSize > 0 && Math.Max(w, h) > maxTextureSize)
                        problems.Add(assetPath + " 超过档位贴图上限 " + maxTextureSize);
                    if (!rel.EndsWith("Source") && importer.textureCompression == TextureImporterCompression.Uncompressed)
                        problems.Add(assetPath + " 切片不允许未压缩");
                }
            }
            if (problems.Count > 0)
                Fail("G3", "贴图规格不合规 " + problems.Count + " 处：" + string.Join("; ", problems.Take(10).ToArray()));
            else
                Pass("G3", "贴图尺寸与压缩符合规");
        }

        private static void GateG4_MaterialSlots(string root)
        {
            var problems = new List<string>();
            string artDir = Path.Combine(root, "Assets/_Project/Art");
            if (!Directory.Exists(artDir)) { Pass("G4", "美术目录尚未建立，跳过材质槽检查"); return; }
            foreach (string f in Directory.GetFiles(artDir, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".fbx" && ext != ".obj") continue;
                string assetPath = f.Replace(root + Path.DirectorySeparatorChar, "").Replace('\\', '/');
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    var mesh = obj as Mesh;
                    if (mesh == null) continue;
                    if (mesh.subMeshCount > 5)
                        problems.Add(assetPath + " 材质槽 " + mesh.subMeshCount + "，上限 5");
                }
            }
            if (problems.Count > 0)
                Fail("G4", "材质槽超限：" + string.Join("; ", problems.Take(10).ToArray()));
            else
                Pass("G4", "模型材质槽不超过 5");
        }

        private static void GateG5_LfsBoundary(string root)
        {
            var problems = new List<string>();
            foreach (string rel in new[] { "Assets", "ProjectSettings" })
            {
                string dir = Path.Combine(root, rel);
                if (!Directory.Exists(dir)) continue;
                foreach (string f in Directory.GetFiles(dir, "*.unity", SearchOption.AllDirectories)
                                   .Concat(Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories)))
                {
                    if (IsLfsPointer(f))
                        problems.Add(f.Replace(root + Path.DirectorySeparatorChar, "").Replace('\\', '/'));
                }
            }
            if (problems.Count > 0)
                Fail("G5", ".unity/.prefab 不得走 LFS（UnityYAMLMerge 无法合并 LFS 内的文本冲突）：" +
                        string.Join("; ", problems.Take(10).ToArray()));
            else
                Pass("G5", ".unity/.prefab 保持文本，未走 LFS");
        }

        private static void GateG6_MetaPairs(string root)
        {
            var orphans = new List<string>();
            var missing = new List<string>();
            string assets = Path.Combine(root, "Assets");
            if (!Directory.Exists(assets)) { Pass("G6", "Assets 目录尚未建立"); return; }
            foreach (string f in Directory.GetFiles(assets, "*", SearchOption.AllDirectories))
            {
                string rel = f.Replace(root + Path.DirectorySeparatorChar, "").Replace('\\', '/');
                if (rel.Contains("/Editor/ReleaseOps/")) continue; // 构建期临时注入，不参与配对检查
                if (f.EndsWith(".meta"))
                {
                    string owner = f.Substring(0, f.Length - 5);
                    if (!File.Exists(owner)) orphans.Add(rel);
                }
                else
                {
                    if (!File.Exists(f + ".meta")) missing.Add(rel);
                }
            }
            if (orphans.Count > 0 || missing.Count > 0)
                Fail("G6", "meta 配对异常：孤立 meta " + orphans.Count + " 个，缺 meta " + missing.Count + " 个：" +
                        string.Join("; ", orphans.Concat(missing).Take(10).ToArray()));
            else
                Pass("G6", "meta 配对完整");
        }

        private static void GateG7_Version(string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                Warn("G7", "未提供期望版本号，跳过版本一致性检查");
                return;
            }
            string actual = PlayerSettings.bundleVersion;
            if (actual != version)
                Fail("G7", "Player Settings 的 bundleVersion(" + actual + ") 与 VERSION(" + version + ") 不一致");
            else
                Pass("G7", "版本号一致：" + version);
        }

        private static void GateG8_Secrets(string root)
        {
            string[] patterns = { ".env", "*.key", "*.keystore", "*.p12", "*.pfx", "*.pem", "*.jks", "*.ulf" };
            // 只扫会被提交进仓库的目录，不扫 Library/Temp/Build/.git（既慢又无关）
            string[] scanRoots = { "Assets", "Packages", "ProjectSettings", "Tools", "docs", "design" };
            var hits = new List<string>();
            foreach (string pat in patterns)
            {
                foreach (string relRoot in scanRoots)
                {
                    string dir = Path.Combine(root, relRoot);
                    if (!Directory.Exists(dir)) continue;
                    foreach (string f in Directory.GetFiles(dir, pat, SearchOption.AllDirectories))
                    {
                        hits.Add(f.Replace(root + Path.DirectorySeparatorChar, "").Replace('\\', '/'));
                    }
                }
                foreach (string f in Directory.GetFiles(root, pat, SearchOption.TopDirectoryOnly))
                {
                    hits.Add(f.Replace(root + Path.DirectorySeparatorChar, "").Replace('\\', '/'));
                }
            }
            if (hits.Count > 0)
                Fail("G8", "仓库内存在疑似密钥文件（密钥只允许走 CI secret）：" + string.Join("; ", hits.Take(10).ToArray()));
            else
                Pass("G8", "仓库内无密钥文件");
        }

        private static void GateG9_LfsPulled(string root)
        {
            string[] exts = { ".fbx", ".blend", ".psd", ".png", ".jpg", ".tga", ".wav", ".mp3", ".ogg" };
            var pointers = new List<string>();
            foreach (string f in Directory.GetFiles(Path.Combine(root, "Assets"), "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (!exts.Contains(ext)) continue;
                if (IsLfsPointer(f))
                    pointers.Add(f.Replace(root + Path.DirectorySeparatorChar, "").Replace('\\', '/'));
            }
            if (pointers.Count > 0)
                Fail("G9", "二进制资产仍是 LFS 指针（未 git lfs pull）：" + string.Join("; ", pointers.Take(10).ToArray()));
            else
                Pass("G9", "LFS 二进制资产已落地");
        }

        private static bool IsLfsPointer(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var buf = new byte[64];
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n <= 0) return false;
                    string head = Encoding.UTF8.GetString(buf, 0, n);
                    return head.StartsWith("version https://git-lfs.github.com/spec/v1");
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void Pass(string id, string msg) { _pass++; Debug.Log("[LINT] PASS " + id + " " + msg); }
        private static void Fail(string id, string msg) { _fail++; Debug.LogError("[LINT] FAIL " + id + " " + msg); }
        private static void Warn(string id, string msg) { _warn++; Debug.LogWarning("[LINT] WARN " + id + " " + msg); }
    }
}
#endif
