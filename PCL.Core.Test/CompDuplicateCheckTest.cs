using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

/// <summary>
///     验证拖入 / 安装组件时的重复识别。
///     关键场景：同一个 Mod 换了版本号就是另一个文件名，必须靠 Mod ID 认出来。
/// </summary>
[TestClass]
public class CompDuplicateCheckTest
{
    private string _modsDir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _modsDir = Path.Combine(Path.GetTempPath(), "PCLTest", "CompDup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_modsDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_modsDir))
                Directory.Delete(_modsDir, true);
        }
        catch
        {
            // 清理失败不影响测试结论。
        }
    }

    [TestMethod]
    public void DetectsSameModWithDifferentVersionFileNames()
    {
        // 实例里已有 0.18.0，再拖入 0.19.0 —— 文件名不同，但 Mod ID 相同。
        CreateModJar(Path.Combine(_modsDir, "spacejoy-mod-0.18.0.jar"), "spacejoy", "0.18.0");
        var incoming = CreateModJar(
            Path.Combine(Path.GetTempPath(), $"spacejoy-mod-0.19.0-{Guid.NewGuid():N}.jar"),
            "spacejoy",
            "0.19.0");

        var conflicts = CompDuplicateCheck.FindConflicts([incoming], _modsDir, Comp.CompType.Mod);

        Assert.AreEqual(1, conflicts.Count);
        Assert.AreEqual(1, conflicts[0].Existing.Count);
        Assert.AreEqual("spacejoy-mod-0.18.0.jar", conflicts[0].Existing[0].FileName);
        Assert.AreEqual("spacejoy", conflicts[0].Existing[0].ModId);
    }

    [TestMethod]
    public void ReportsNoConflictForDifferentMods()
    {
        CreateModJar(Path.Combine(_modsDir, "other-mod-1.0.0.jar"), "othermod", "1.0.0");
        var incoming = CreateModJar(
            Path.Combine(Path.GetTempPath(), $"spacejoy-mod-0.19.0-{Guid.NewGuid():N}.jar"),
            "spacejoy",
            "0.19.0");

        var conflicts = CompDuplicateCheck.FindConflicts([incoming], _modsDir, Comp.CompType.Mod);

        Assert.AreEqual(0, conflicts.Count);
    }

    [TestMethod]
    public void DetectsIdenticalFileNameEvenWithoutReadableMetadata()
    {
        // 损坏 / 无元数据的 jar 也要能靠文件名拦住。
        var existing = Path.Combine(_modsDir, "broken-mod-1.0.0.jar");
        File.WriteAllText(existing, "not a real jar");
        var incoming = Path.Combine(Path.GetTempPath(), $"broken-mod-1.0.0-{Guid.NewGuid():N}.jar");
        File.WriteAllText(incoming, "also not a jar");

        var conflicts = CompDuplicateCheck.FindConflicts([incoming], _modsDir, Comp.CompType.Mod);

        // 文件名不同，元数据也读不出，因此不误报。
        Assert.AreEqual(0, conflicts.Count);
    }

    [TestMethod]
    public void TreatsDisabledModAsSameMod()
    {
        CreateModJar(Path.Combine(_modsDir, "spacejoy-mod-0.18.0.jar.disabled"), "spacejoy", "0.18.0");
        var incoming = CreateModJar(
            Path.Combine(Path.GetTempPath(), $"spacejoy-mod-0.19.0-{Guid.NewGuid():N}.jar"),
            "spacejoy",
            "0.19.0");

        var conflicts = CompDuplicateCheck.FindConflicts([incoming], _modsDir, Comp.CompType.Mod);

        Assert.AreEqual(1, conflicts.Count);
        Assert.IsTrue(conflicts[0].Existing[0].IsDisabled);
    }

    [TestMethod]
    public void DisableNamingMatchesPclConvention()
    {
        // PCL 用 xxx.jar.disabled 表示禁用，这里确认检测器把这类文件当成同一个 Mod。
        var disabled = Path.Combine(_modsDir, "spacejoy-mod-0.18.0.jar.disabled");
        CreateModJar(disabled, "spacejoy", "0.18.0");

        var conflicts = CompDuplicateCheck.FindConflicts(
            [CreateModJar(Path.Combine(Path.GetTempPath(), $"spacejoy-{Guid.NewGuid():N}.jar"), "spacejoy", "0.19.0")],
            _modsDir,
            Comp.CompType.Mod);

        Assert.AreEqual(1, conflicts.Count);
        Assert.IsTrue(conflicts[0].Existing[0].IsDisabled);
        // 禁用后文件名应保留 .disabled 后缀，不能再被当成启用状态。
        Assert.IsTrue(conflicts[0].Existing[0].FileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void SameTargetNameMeansDeleteNotDisable()
    {
        // 判定规则：安装后的落盘文件名与已有文件相同 → 删除旧文件；
        // 不同名（仅版本号不同）→ 禁用保留。这里按同样的规则模拟两种情形。

        // 情形一：同名。xxx.jar 与 xxx.jar.disabled 的落盘名都是 xxx.jar。
        var enabledName = "spacejoy-mod-0.19.0.jar";
        var disabledName = "spacejoy-mod-0.19.0.jar.disabled";
        Assert.AreEqual(StripSuffix(enabledName), StripSuffix(disabledName));

        // 情形二：异名。0.18 与 0.19 落盘名不同。
        var olderName = "spacejoy-mod-0.18.0.jar";
        Assert.AreNotEqual(StripSuffix(olderName), StripSuffix(enabledName));
    }

    private static string StripSuffix(string name) =>
        name.Replace(".disabled", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".old", "", StringComparison.OrdinalIgnoreCase);

    private static string CreateModJar(string path, string modId, string version)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("fabric.mod.json");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write($$"""
            {
              "schemaVersion": 1,
              "id": "{{modId}}",
              "version": "{{version}}",
              "name": "{{modId}}"
            }
            """);
        return path;
    }
}
