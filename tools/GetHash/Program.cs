using System.Security.Cryptography;
using System.Text;

namespace GetHash;

internal static class Program
{
  private static async Task<int> Main(string[] args)
  {
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
      PrintUsage();
      return args.Length == 0 ? 1 : 0;
    }

    // --- 引数解析 (オプションは -o/--out のみ。それ以外は positional のファイルパス) ---
    string? outDir = null;
    var files = new List<string>(args.Length);
    for (var i = 0; i < args.Length; i++)
    {
      switch (args[i])
      {
        case "-o" or "--out" when i + 1 < args.Length:
          outDir = args[++i];
          break;
        case "-o" or "--out":
          await Console.Error.WriteLineAsync("--out には出力先ディレクトリが必要です。");
          return 1;
        default:
          files.Add(args[i]);
          break;
      }
    }

    if (files.Count == 0)
    {
      await Console.Error.WriteLineAsync("ファイルが 1 つも指定されていません。");
      return 1;
    }

    // --- 振り分け + ハッシュ計算 ---
    var windowsEntries = new List<Entry>();
    var macEntries = new List<Entry>();
    var hadError = false;

    foreach (var file in files)
    {
      if (!File.Exists(file))
      {
        await Console.Error.WriteLineAsync($"[警告] ファイルが見つかりません: {file}");
        hadError = true;
        continue;
      }

      var info = DetectArchiveInfo(file);
      if (info is null)
      {
        await Console.Error.WriteLineAsync($"[警告] 種別を判別できませんでした (スキップ): {Path.GetFileName(file)}");
        hadError = true;
        continue;
      }

      var (md5, sha1, sha256) = await ComputeHashesAsync(file);
      var entry = new Entry(info, Path.GetFileName(file), md5, sha1, sha256);

      if (info.Platform == Platform.Windows)
        windowsEntries.Add(entry);
      else
        macEntries.Add(entry);
    }

    // 慣例的な並び順に揃える (arm64 → x64、exe → zip)
    windowsEntries.Sort((a, b) => a.Info.SortOrder.CompareTo(b.Info.SortOrder));
    macEntries.Sort((a, b) => a.Info.SortOrder.CompareTo(b.Info.SortOrder));

    // --- 出力 ---
    var winYaml = windowsEntries.Count > 0 ? BuildWindowsYaml(windowsEntries) : null;
    var macYaml = macEntries.Count > 0 ? BuildMacYaml(macEntries) : null;

    if (winYaml is null && macYaml is null)
    {
      await Console.Error.WriteLineAsync("出力するエントリがありません。");
      return 1;
    }

    if (outDir is null)
    {
      // 標準出力にまとめて出す (コピペ用)
      if (winYaml is not null)
      {
        Console.WriteLine("# ===== Windows =====");
        Console.Write(winYaml);
      }
      if (winYaml is not null && macYaml is not null)
      {
        Console.WriteLine();
      }
      if (macYaml is not null)
      {
        Console.WriteLine("# ===== macOS =====");
        Console.Write(macYaml);
      }
    }
    else
    {
      Directory.CreateDirectory(outDir);
      var encoding = new UTF8Encoding(false);  // BOM なし

      if (winYaml is not null)
      {
        var path = Path.Combine(outDir, "_windows.yaml");
        await File.WriteAllTextAsync(path, winYaml, encoding);
        await Console.Error.WriteLineAsync($"出力: {path}");
      }
      if (macYaml is not null)
      {
        var path = Path.Combine(outDir, "_mac.yaml");
        await File.WriteAllTextAsync(path, macYaml, encoding);
        await Console.Error.WriteLineAsync($"出力: {path}");
      }
    }

    return hadError ? 2 : 0;
  }

  // ---- Windows 用 YAML: title はクォートなし、arch フィールドなし ----
  private static string BuildWindowsYaml(List<Entry> entries)
  {
    var sb = new StringBuilder();
    sb.AppendLine("archives:");
    foreach (var e in entries)
    {
      sb.AppendLine("- title:");
      sb.AppendLine($"  ja: {e.Info.TitleJa}");
      sb.AppendLine($"  en: {e.Info.TitleEn}");
      sb.AppendLine($"  filename: {e.FileName}");
      sb.AppendLine($"  md5: {e.Md5}");
      sb.AppendLine($"  sha1: {e.Sha1}");
      sb.AppendLine($"  sha256: {e.Sha256}");
    }
    return sb.ToString();
  }

  // ---- macOS 用 YAML: title はダブルクォート、arch フィールドあり ----
  private static string BuildMacYaml(List<Entry> entries)
  {
    var sb = new StringBuilder();
    sb.AppendLine("archives:");
    foreach (var e in entries)
    {
      sb.AppendLine("- title:");
      sb.AppendLine($"  ja: \"{e.Info.TitleJa}\"");
      sb.AppendLine($"  en: \"{e.Info.TitleEn}\"");
      sb.AppendLine($"  filename: {e.FileName}");
      sb.AppendLine($"  arch: {e.Info.Arch}");
      sb.AppendLine($"  md5: {e.Md5}");
      sb.AppendLine($"  sha1: {e.Sha1}");
      sb.AppendLine($"  sha256: {e.Sha256}");
    }
    return sb.ToString();
  }

  // ---- ファイル名から種別を判別 ----
  private static ArchiveInfo? DetectArchiveInfo(string filePath)
  {
    var name = Path.GetFileName(filePath).ToLowerInvariant();
    var ext = Path.GetExtension(name);

    return ext switch
    {
      ".dmg" when name.Contains("-arm64") =>
        new ArchiveInfo(Platform.Mac, SortOrder: 0,
          "Apple Silicon版 (DMG)", "Apple Silicon (DMG)", "arm64"),

      ".dmg" when name.Contains("-x64") =>
        new ArchiveInfo(Platform.Mac, SortOrder: 1,
          "Intel版 (DMG)", "Intel (DMG)", "x64"),

      ".dmg" => null,  // arch 不明な DMG は警告対象

      ".exe" or ".msi" =>
        new ArchiveInfo(Platform.Windows, SortOrder: 0,
          "自己解凍インストーラー", "Self-extracting installer", null),

      ".zip" =>
        new ArchiveInfo(Platform.Windows, SortOrder: 1,
          "ZIPファイル", "ZIP file", null),

      _ => null,
    };
  }

  // ---- 1 回のディスク読み取りで MD5/SHA1/SHA256 を同時計算 ----
  private static async Task<(string Md5, string Sha1, string Sha256)> ComputeHashesAsync(string filePath)
  {
    using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
    using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
    using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    await using var fs = File.OpenRead(filePath);
    var buffer = new byte[81920];
    int read;
    while ((read = await fs.ReadAsync(buffer)) > 0)
    {
      var span = buffer.AsSpan(0, read);
      md5.AppendData(span);
      sha1.AppendData(span);
      sha256.AppendData(span);
    }

    return (
      Convert.ToHexStringLower(md5.GetHashAndReset()),
      Convert.ToHexStringLower(sha1.GetHashAndReset()),
      Convert.ToHexStringLower(sha256.GetHashAndReset()));
  }

  private static void PrintUsage()
  {
    Console.Error.WriteLine("""
                使い方: GetHash [options] <file1> [file2] ...

                  ファイル名から OS / アーキテクチャを自動判別し、
                  Windows / macOS 別に archives: 形式の YAML を生成します。

                判別ルール:
                  *.exe / *.msi      -> Windows (自己解凍インストーラー)
                  *.zip          -> Windows (ZIP ファイル)
                  *-arm64.dmg      -> macOS Apple Silicon
                  *-x64.dmg        -> macOS Intel

                オプション:
                  -o, --out <dir>   出力先ディレクトリ。
                            windows.yaml / mac.yaml を書き出す
                            (該当エントリが無い側は出力しない)
                          省略時は標準出力にまとめて出力
                  -h, --help    このヘルプを表示

                例:
                  GetHash TbMsgFilterCopy100.exe TbMsgFilterCopy100.zip ^
                         TbMsgFilterCopy100-arm64.dmg TbMsgFilterCopy100-x64.dmg

                  GetHash dist\*.exe dist\*.zip dist\*.dmg -o .\out

                終了コード:
                  0  正常終了
                  1  使い方エラー (引数なし、未指定オプションなど)
                  2  一部ファイルが警告でスキップされた
                """);
  }

  // ---- 内部型 ----
  private enum Platform { Windows, Mac }

  private sealed record ArchiveInfo(
    Platform Platform,
    int SortOrder,
    string TitleJa,
    string TitleEn,
    string? Arch);

  private sealed record Entry(
    ArchiveInfo Info,
    string FileName,
    string Md5,
    string Sha1,
    string Sha256);
}
