using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public sealed class TilePaletteService : ITilePaletteService
{
    private static readonly IReadOnlyDictionary<string, string> Colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"] = "#178600", ["C++"] = "#F34B7D", ["C"] = "#555555", ["Java"] = "#B07219",
        ["JavaScript"] = "#F1E05A", ["TypeScript"] = "#3178C6", ["Python"] = "#3572A5", ["Go"] = "#00ADD8",
        ["Rust"] = "#DEA584", ["Kotlin"] = "#A97BFF", ["Swift"] = "#F05138", ["Dart"] = "#00B4AB",
        ["Ruby"] = "#701516", ["PHP"] = "#4F5D95", ["Shell"] = "#89E051", ["PowerShell"] = "#012456",
        ["Vue"] = "#41B883", ["React"] = "#087EA4", ["Angular"] = "#DD0031", ["Avalonia"] = "#7B5CFA",
        [".NET"] = "#512BD4", ["Docker"] = "#2496ED", ["Kubernetes"] = "#326CE5", ["Git"] = "#F05032",
        ["placeholder"] = "#2D5F8B", ["history"] = "#7B3FA1", ["quote"] = "#00695C", ["hardware"] = "#9A4A00"
    };

    public TilePalette Create(string accentKey)
    {
        var key = accentKey ?? string.Empty;
        var background = key.Length is 7 or 9 && key[0] == '#' ? key : Colors.TryGetValue(key, out var value) ? value : Deterministic(key);
        var whiteRatio = ContrastRatio(background, "#FFFFFF");
        var blackRatio = ContrastRatio(background, "#101010");
        var foreground = whiteRatio >= blackRatio ? "#FFFFFF" : "#101010";
        var secondary = foreground == "#FFFFFF" ? "#EAF2FA" : "#252525";
        var scrim = foreground == "#FFFFFF" ? "#B0000000" : "#B8FFFFFF";
        return new(background, foreground, secondary, scrim);
    }

    public double ContrastRatio(string first, string second)
    {
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static double Luminance(string color)
    {
        var text = color.TrimStart('#');
        if (text.Length == 8) text = text[2..];
        if (text.Length != 6) return 0;
        var values = new[] { text[..2], text.Substring(2, 2), text.Substring(4, 2) }
            .Select(x => int.Parse(x, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d)
            .Select(x => x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4)).ToArray();
        return values[0] * .2126 + values[1] * .7152 + values[2] * .0722;
    }

    private static string Deterministic(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
        var hue = BitConverter.ToUInt16(bytes, 0) % 360; const double saturation = .58; const double lightness = .38;
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var part = hue / 60d; var x = chroma * (1 - Math.Abs(part % 2 - 1));
        var (r, g, b) = part switch { < 1 => (chroma, x, 0d), < 2 => (x, chroma, 0d), < 3 => (0d, chroma, x), < 4 => (0d, x, chroma), < 5 => (x, 0d, chroma), _ => (chroma, 0d, x) };
        var m = lightness - chroma / 2;
        return $"#{(int)((r + m) * 255):X2}{(int)((g + m) * 255):X2}{(int)((b + m) * 255):X2}";
    }
}

public sealed class TipCatalog : ITipCatalog
{
    private static readonly TipDefinition[] Tips =
    [
        new("lang-c-1972", "语言史", "C · 1972", "Dennis Ritchie 在贝尔实验室设计了 C 语言。", "C"),
        new("lang-python-1991", "语言史", "Python · 1991", "Python 0.9.0 首次公开发布时已经包含类、异常和函数。", "Python"),
        new("lang-rust-2010", "语言史", "Rust · 2010", "Mozilla 在 2010 年首次公开 Rust 项目。", "Rust"),
        new("lang-java-1995", "语言史", "Java · 1995", "Java 在 1995 年正式发布，并很快成为跨平台开发的重要语言。", "Java"),
        new("lang-javascript-1995", "语言史", "JavaScript · 1995", "它诞生于浏览器，却逐步延展到服务端、工具链与桌面应用。", "JavaScript"),
        new("lang-csharp-2000", "语言史", "C# · 2000", "C# 在 2000 年发布，并与 .NET 生态共同演进。", "C#"),
        new("lang-ruby-1995", "语言史", "Ruby · 1995", "Ruby 以开发者体验为中心，影响了许多现代 Web 框架的设计。", "Ruby"),
        new("wide:git-snapshot", "技术冷知识", "Git 的对象是不可变快照", "提交保存的是项目树的快照引用，而不是传统意义上的逐行差异。", "Git", 2, 1),
        new("wide:keyboard", "硬件冷知识", "QWERTY 早于计算机", "今天常见的键位布局最初服务于机械打字机。", "hardware", 2, 1),
        new("wide:utf8", "技术冷知识", "UTF-8 让文本跨越语言", "它以可变长度编码兼容 ASCII，也能表达世界上绝大多数书写系统。", "TypeScript", 2, 1),
        new("wide:tcp", "网络冷知识", "TCP 先保证可靠，再谈速度", "确认、重传与拥塞控制让不可靠网络上的字节流变得可预测。", "Go", 2, 1),
        new("large:apollo", "历史", "阿波罗导航计算机", "其主频约 2 MHz，却完成了实时导航、姿态控制和登月任务。", "hardware", 2, 2),
        new("large:quote-knuth", "程序员名言", "过早优化是万恶之源", "真正完整的语境强调：应把注意力放在那关键的效率部分。", "quote", 2, 2, Attribution: "Donald Knuth"),
        new("large:unix", "系统史", "Unix 的小工具哲学", "让每个程序专注一件事，再通过清晰的接口把它们组合起来。", "Shell", 2, 2),
        new("quote-kay", "程序员名言", "预测未来的最好方法，是创造它。", "把工具做成自己愿意每天使用的产品。", "quote", Attribution: "Alan Kay"),
        new("quote-lamport", "程序员名言", "分布式系统的难题常来自时间", "时钟、顺序与失败都需要被明确建模，而不是假定它们总会正常。", "distributed"),
        new("community-readable", "社区俗语", "代码首先是写给人看的", "机器只是顺便执行它。命名和结构也是产品体验。", "quote"),
        new("community-boring", "社区俗语", "无聊的技术往往更可靠", "成熟、可观察、容易恢复，常常比新奇更有价值。", "placeholder"),
        new("community-observe", "工程原则", "不能观测，就很难可靠地改进", "日志、指标与可重现的失败路径，是产品稳定性的基础。", "C#"),
        new("community-boundary", "工程原则", "边界比聪明更重要", "清晰的模块边界让缓存、认证、同步和 UI 可以独立演进。", ".NET"),
        new("history-linux", "历史上的今天", "Linux 首次公开", "1991 年 8 月 25 日，Linus Torvalds 发布了那封著名的项目公告。", "Linux", 2, 1, 8, 25),
        new("history-web", "历史上的今天", "万维网走向公众", "1991 年 8 月 6 日，世界上第一个网站开始对外提供信息。", "JavaScript", 2, 1, 8, 6),
        new("history-git", "历史上的今天", "Git 开始开发", "2005 年 4 月，Git 为 Linux 内核协作而诞生。", "Git", 2, 1, 4, 7),
        new("history-dotnet", "历史上的今天", ".NET Framework 1.0", "2002 年 2 月 13 日，.NET Framework 1.0 正式发布。", ".NET", 2, 1, 2, 13)
    ];

    public IReadOnlyList<TipDefinition> GetTips(DateOnly date)
    {
        var exact = Tips.Where(x => x.Month == date.Month && x.Day == date.Day);
        return exact.Concat(Tips.Where(x => x.Month is null)).ToList();
    }
}
