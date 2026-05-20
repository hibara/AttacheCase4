using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;

namespace GetVersion;

internal class Program
{
  public static int Main(string[] args)
  {
    var cli = new Args(args);

    if (cli.Has("help", "h", "?") || args.Length == 0)
    {
      Console.WriteLine("""
                      .csproj または実行ファイルからバージョン番号を抽出するツール

                      オプション（順不同）:
                        --filepath,         -p <パス>      対象の *.csproj か、実行ファイル *.exe
                        --version,          -ver           Versionを取得して返す
                        --assemblyversion,  -asm           AssemblyVersionを取得して返す
                        --fileversion,      -fv            FileVersionを取得して返す
                        --nodot,            -nd            ドット無しのバージョン情報を返す
                        --help,             -h             このヘルプを表示
                      """);
      return cli.Has("help", "h", "?") ? 0 : 1;
    }

    var filePath = cli.Get("filepath", "p");

    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
    {
      Console.Error.WriteLine($"指定されたファイルが存在しません: {filePath}");
      return 1;
    }

    var fNoDot = cli.Has("nodot", "nd");

    if (string.Equals(Path.GetExtension(filePath), ".csproj", StringComparison.OrdinalIgnoreCase))
    {
      int ret;
      string versionString;

      if (cli.Has("version", "ver"))
      {
        (ret, versionString) = GetVersion(filePath);
      }
      else if (cli.Has("assemblyversion", "asm"))
      {
        (ret, versionString) = GetAssemblyVersion(filePath);
      }
      else if (cli.Has("fileversion", "fv"))
      {
        (ret, versionString) = GetFileVersion(filePath);
      }
      else
      {
        Console.Error.WriteLine("エラー: --version / --assemblyversion / --fileversion のいずれかを指定してください。");
        return 1;
      }

      if (ret > 0)
      {
        Console.Error.WriteLine(versionString);
        return 1;
      }

      if (fNoDot) versionString = versionString.Replace(".", "");
      Console.WriteLine(versionString);
      return 0;
    }

    if (string.Equals(Path.GetExtension(filePath), ".exe", StringComparison.OrdinalIgnoreCase))
    {
      var vi = FileVersionInfo.GetVersionInfo(filePath);
      Console.WriteLine(
        fNoDot ? "{0}{1}{2}{3}" : "{0}.{1}.{2}.{3}",
        vi.ProductMajorPart, vi.ProductMinorPart, vi.ProductBuildPart, vi.ProductPrivatePart);
      return 0;
    }

    Console.Error.WriteLine($"サポートされていないファイル形式です: {filePath}");
    return 1;
  }

  /// <summary>
  /// 指定された .csproj ファイルから <c>Version</c> 要素の値を取得します。
  /// </summary>
  /// <param name="csprojPath">対象の .csproj ファイルのパス。</param>
  /// <returns>
  /// タプル形式の結果を返します。<br/>
  /// <c>ret</c>: 処理結果コード（0: 成功, 1: エラー）。<br/>
  /// <c>verstring</c>: バージョン文字列またはエラーメッセージ。
  /// </returns>
  /// <exception cref="FileNotFoundException">指定されたファイルが存在しない場合にスローされます。</exception>
  /// <exception cref="XmlException">指定されたファイルが無効な XML の場合にスローされます。</exception>
  /// <exception cref="InvalidOperationException">指定されたファイルに <c>Version</c> 要素が存在しない場合にスローされます。</exception>
  private static (int ret, string verstring) GetVersion(string csprojPath)
  {
    try
    {
      var doc = XDocument.Load(csprojPath);
      return (0, doc.Descendants("Version").First().Value);
    }
    catch (FileNotFoundException)
    {
      return (1, "エラー: 指定されたファイルが存在しません。");
    }
    catch (XmlException)
    {
      return (1, "エラー: 指定されたファイルは無効なXMLです。");
    }
    catch (InvalidOperationException)
    {
      return (1, "エラー: 指定されたファイルには`Version`要素が存在しません。");
    }
  }

  /// <summary>
  /// 指定された .csproj ファイルから <c>AssemblyVersion</c> を取得します。
  /// </summary>
  /// <param name="csprojPath">対象の .csproj ファイルのパス。</param>
  /// <returns>
  /// タプル形式の結果を返します。<br/>
  /// <c>ret</c>: 処理結果コード（0: 成功, 1: エラー）。<br/>
  /// <c>verstring</c>: 取得したバージョン文字列、またはエラーメッセージ。
  /// </returns>
  /// <exception cref="FileNotFoundException">指定されたファイルが存在しない場合にスローされます。</exception>
  /// <exception cref="XmlException">指定されたファイルが無効な XML の場合にスローされます。</exception>
  /// <exception cref="InvalidOperationException">指定されたファイルに <c>AssemblyVersion</c> 要素が存在しない場合にスローされます。</exception>
  private static (int ret, string verstring) GetAssemblyVersion(string csprojPath)
  {
    try
    {
      var doc = XDocument.Load(csprojPath);
      return (0, doc.Descendants("AssemblyVersion").First().Value);
    }
    catch (FileNotFoundException)
    {
      return (1, "エラー: 指定されたファイルが存在しません。");
    }
    catch (XmlException)
    {
      return (1, "エラー: 指定されたファイルは無効なXMLです。");
    }
    catch (InvalidOperationException)
    {
      return (1, "エラー: 指定されたファイルには`AssemblyVersion`要素が存在しません。");
    }
  }

  /// <summary>
  /// 指定された .csproj ファイルから <c>FileVersion</c> 要素の値を取得します。
  /// </summary>
  /// <param name="csprojPath">対象の .csproj ファイルのパス。</param>
  /// <returns>
  /// タプル形式の結果を返します。<br/>
  /// <c>ret</c>: 処理結果を示す整数値（0: 成功、1: エラー）。<br/>
  /// <c>verstring</c>: 取得したバージョン文字列、またはエラーメッセージ。
  /// </returns>
  /// <exception cref="FileNotFoundException">指定されたファイルが存在しない場合にスローされます。</exception>
  /// <exception cref="XmlException">指定されたファイルが無効な XML である場合にスローされます。</exception>
  /// <exception cref="InvalidOperationException">指定されたファイルに <c>FileVersion</c> 要素が存在しない場合にスローされます。</exception>
  private static (int ret, string verstring) GetFileVersion(string csprojPath)
  {
    try
    {
      var doc = XDocument.Load(csprojPath);
      return (0, doc.Descendants("FileVersion").First().Value);
    }
    catch (FileNotFoundException)
    {
      return (1, "エラー: 指定されたファイルが存在しません。");
    }
    catch (XmlException)
    {
      return (1, "エラー: 指定されたファイルは無効なXMLです。");
    }
    catch (InvalidOperationException)
    {
      return (1, "エラー: 指定されたファイルには`FileVersion`要素が存在しません。");
    }
  }
}