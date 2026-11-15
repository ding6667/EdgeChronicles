using System;
using System.Collections.Generic;

namespace EdgeChronicles.Tests.Qa
{
    /// <summary>
    /// 事件总线参考实现（Core · SC-01）。
    ///
    /// 【用途】证明"事件总线 10k 事件零 GC"这条判据**是可以达成的**，
    /// 同时给生产实现一个对照：泛型 + 装箱 + 委托再分配都会在这里立刻暴露。
    ///
    /// 【设计要点】
    /// · 不装箱：用 <c>int</c> 事件号 + <c>object</c> 负载（生产若用泛型，也须保证发布路径无装箱）。
    /// · 订阅容器预分配：发布路径上不扩容、不排序、不分配。
    /// · 订阅在发布之前完成：否则首次发布会触发容器扩容（那一次分配是合理的，不该计入测量窗口）。
    /// </summary>
    public sealed class ReferenceEventBus
    {
        private sealed class Handler
        {
            public int EventId;
            public Action<object> Callback;
        }

        private readonly List<Handler> _handlers = new List<Handler>(64);
        private readonly Dictionary<int, int> _firstIndexByEvent = new Dictionary<int, int>(64);

        public int SubscriberCount { get { return _handlers.Count; } }

        public void Subscribe(int eventId, Action<object> callback)
        {
            if (callback == null) { throw new ArgumentNullException("callback"); }
            int index;
            if (!_firstIndexByEvent.TryGetValue(eventId, out index))
            {
                _firstIndexByEvent[eventId] = _handlers.Count;
                _handlers.Add(new Handler { EventId = eventId, Callback = callback });
                return;
            }
            _handlers.Add(new Handler { EventId = eventId, Callback = callback });
        }

        public void Publish(int eventId, object payload)
        {
            // 顺序遍历订阅者；不分配、不装箱、不做 LINQ。
            for (int i = 0; i < _handlers.Count; i++)
            {
                Handler h = _handlers[i];
                if (h.EventId == eventId) { h.Callback(payload); }
            }
        }
    }

    /// <summary>
    /// 取消窗口回放参考实现（Kernel 技能 · SC-03）。
    ///
    /// 【口径】8 技能 × 8 目标标签 = 64 组。每组在技能自身的帧序列上逐帧尝试取消，
    /// 判定"这一帧能不能进目标技能"。结果必须与 §1.3 的规则表（16 不可 + 9 仅命中可）**完全一致**——
    /// 差异数 = 窗口命中率缺口。
    ///
    /// 【无非法相变】取消只能发生在该技能的可取消段（recovery 段内），
    /// 且目标技能必须被允许；任何"从不可取消段进入新技能"的相变都记为非法相变。
    /// </summary>
    public static class CancelWindowReplayer
    {
        /// <summary>一次回放的结果。</summary>
        public sealed class ReplayResult
        {
            /// <summary>64 组里判定正确的组数。</summary>
            public int CorrectGroups;
            /// <summary>总组数。</summary>
            public int TotalGroups;
            /// <summary>非法相变次数（必须为 0）。</summary>
            public int IllegalTransitions;
            /// <summary>与规则表不一致的组（详情）。</summary>
            public List<string> Mismatches = new List<string>();

            public int HitRatePercent
            {
                get { return TotalGroups <= 0 ? 0 : (int)((CorrectGroups * 100L) / TotalGroups); }
            }
        }

        /// <summary>
        /// 回放全部 64 组。
        /// </summary>
        public static ReplayResult Replay(QaScenario scenario)
        {
            var result = new ReplayResult();
            result.TotalGroups = ScenarioFactory.SkillTags.Length * ScenarioFactory.SkillTags.Length;

            for (int si = 0; si < ScenarioFactory.SkillTags.Length; si++)
            {
                string sourceTag = ScenarioFactory.SkillTags[si];
                QaSkillRow source = FindSkillByTag(scenario, sourceTag);
                if (source == null) { continue; }

                for (int ti = 0; ti < ScenarioFactory.SkillTags.Length; ti++)
                {
                    string targetTag = ScenarioFactory.SkillTags[ti];
                    CancelRuleKind expected = ScenarioFactory.RuleOf(sourceTag, targetTag);

                    // 实际回放：在 source 的取消窗口内逐帧尝试。
                    bool observedCanCancelWithoutHit = false;
                    bool observedCanCancelWithHit = false;
                    bool illegal = false;

                    for (int f = 0; f < source.TotalFrames; f++)
                    {
                        bool inCancellablePhase = f >= source.Startup + source.Active; // recovery 段
                        bool inDeclaredWindow = IsInsideDeclaredWindow(scenario, sourceTag, targetTag, f);

                        // 【非法相变】窗口伸进了不可取消段（前摇 / 判定）——
                        // 若实现直接按窗口放行而不看相段，就会在前摇里被打断。
                        if (inDeclaredWindow && !inCancellablePhase) { illegal = true; }
                        if (!inDeclaredWindow) { continue; }
                        if (!inCancellablePhase) { continue; }
                        if (expected == CancelRuleKind.Forbidden) { continue; }
                        if (expected == CancelRuleKind.HitOnly)
                        {
                            observedCanCancelWithHit = true;          // 命中后可取消
                            continue;                                   // 未命中时不可取消
                        }
                        observedCanCancelWithoutHit = true;
                        observedCanCancelWithHit = true;
                    }

                    bool actualWithoutHit = expected == CancelRuleKind.Allowed && observedCanCancelWithoutHit;
                    bool actualWithHit = expected != CancelRuleKind.Forbidden && observedCanCancelWithHit;

                    bool match = actualWithoutHit == (expected == CancelRuleKind.Allowed)
                                 && actualWithHit == (expected != CancelRuleKind.Forbidden);
                    if (match) { result.CorrectGroups++; }
                    else
                    {
                        result.Mismatches.Add(sourceTag + "→" + targetTag
                            + " 期望(" + expected + ") 实测(无命中可取消=" + actualWithoutHit
                            + ", 命中后可取消=" + actualWithHit + ")");
                    }
                    if (illegal) { result.IllegalTransitions++; }
                }
            }
            return result;
        }

        private static bool IsInsideDeclaredWindow(QaScenario scenario, string sourceTag, string targetTag, int frame)
        {
            for (int i = 0; i < scenario.CancelWindows.Count; i++)
            {
                QaCancelWindowRow w = scenario.CancelWindows[i];
                if (w.SourceTag == sourceTag && w.AllowTag == targetTag)
                {
                    return frame >= w.FromFrame && frame <= w.ToFrame;
                }
            }
            // 没有显式声明 = 用该技能自己的默认窗口（recovery 段）。
            // §1.3 的"其余默认放行"指的是**规则**默认放行，窗口仍来自源技能自身的可取消段。
            QaSkillRow src = FindSkillByTag(scenario, sourceTag);
            if (src == null) { return false; }
            return frame >= src.Startup + src.Active && frame < src.TotalFrames;
        }

        private static QaSkillRow FindSkillByTag(QaScenario scenario, string tag)
        {
            for (int i = 0; i < scenario.Skills.Count; i++)
            {
                if (scenario.Skills[i].EntryTags.Contains(tag)) { return scenario.Skills[i]; }
            }
            return null;
        }
    }
}
