using EdgeChronicles.Tests.Qa;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace EdgeChronicles.Tests.Smoke
{
    /// <summary>
    /// SC-08 · 存档：写入 → CRC → 回读；篡改 1 字节必须失败。
    ///
    /// 【口径】量 = 字节（byte）；篡改偏移覆盖头部魔数 / 版本 / CRC 字段 / 本体多处
    /// （EL-03 §5 ④ 的 20 个偏移）。
    /// </summary>
    public class SmokeSaveTests
    {
        /// <summary>参考读档：校验魔数、版本、CRC，任一不符必须返回 false。</summary>
        private static bool TryRead(byte[] blob, out byte[] body)
        {
            body = null;
            if (blob == null || blob.Length < 16) { return false; }
            if (blob[0] != 0x45 || blob[1] != 0x43 || blob[2] != 0x48 || blob[3] != 0x43) { return false; }
            uint stored = (uint)((blob[8] << 24) | (blob[9] << 16) | (blob[10] << 8) | blob[11]);
            int bodyLength = blob.Length - 16;
            var tmp = new byte[bodyLength];
            Array.Copy(blob, 16, tmp, 0, bodyLength);
            uint computed = Crc32Reference.Compute(tmp);
            if (computed != stored) { return false; }
            body = tmp;
            return true;
        }

        [Test]
        public void 写入CRC回读_合法存档必须通过()
        {
            ScenarioFactory.SaveBlob blob = ScenarioFactory.SaveLegal();
            byte[] body;
            Assert.IsTrue(TryRead(blob.Bytes, out body), "合法存档回读失败。");
            Assert.AreEqual(blob.Body.Length, body.Length);
            Assert.AreEqual(Crc32Reference.Compute(blob.Body), Crc32Reference.Compute(body));
        }

        [Test]
        public void 篡改一个字节_必须失败()
        {
            int[] offsets = ScenarioFactory.TamperOffsets;
            Assert.AreEqual(20, offsets.Length, "篡改偏移应为 20 个（EL-03 §5 ④）。");

            var failed = new List<string>();
            for (int i = 0; i < offsets.Length; i++)
            {
                ScenarioFactory.SaveBlob tampered = ScenarioFactory.SaveTampered(offsets[i]);
                byte[] body;
                if (TryRead(tampered.Bytes, out body))
                {
                    failed.Add("偏移 " + offsets[i] + " 篡改后仍能读出存档");
                }
            }
            Assert.IsEmpty(failed,
                "以下篡改没有被检出（存档必须对任意 1 字节篡改失败）：\n" + string.Join("\n", failed.ToArray()));
        }

        [Test]
        public void 篡改本体任意位置_都必须失败()
        {
            // 除固定偏移外，再对本体做一次全覆盖扫描，确保没有"漏检区间"。
            ScenarioFactory.SaveBlob legal = ScenarioFactory.SaveLegal();
            var failed = new List<string>();
            for (int off = 16; off < legal.Bytes.Length; off++)
            {
                ScenarioFactory.SaveBlob tampered = ScenarioFactory.SaveTampered(off);
                byte[] body;
                if (TryRead(tampered.Bytes, out body)) { failed.Add("本体偏移 " + off); }
            }
            Assert.IsEmpty(failed,
                "本体中存在未被检出的篡改位置（数量 " + failed.Count + "）：\n" + string.Join("\n", failed.ToArray()));
        }
    }
}
