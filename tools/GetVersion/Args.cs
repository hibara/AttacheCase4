using System;
using System.Collections.Generic;

namespace GetVersion;

/// <summary>
/// 順不同のコマンドライン引数パーサー（.NET 10）。
/// --key value / --key=value / -k value / -k=value / 値なしフラグ / 位置引数 に対応。
/// "--" 以降は全て位置引数として扱う。
/// </summary>
public sealed class Args
{
  private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);
  private readonly List<string> _positional = [];

  /// <summary>
  /// 位置引数（オプションキーでない引数）。
  /// </summary>
  public IReadOnlyList<string> Positional => _positional;

  public Args(string[] args)
  {
    for (var i = 0; i < args.Length; i++)
    {
      var a = args[i];

      if (a == "--")
      {
        for (var j = i + 1; j < args.Length; j++) _positional.Add(args[j]);
        break;
      }

      if (TryParseKey(a, out var key, out var inline))
      {
        if (inline is not null)
          _values[key] = inline;
        else if (i + 1 < args.Length && !IsKeyArg(args[i + 1]))
          _values[key] = args[++i];
        else
          _flags.Add(key);
      }
      else
      {
        _positional.Add(a);
      }
    }
  }

  /// <summary>
  /// キーが指定されているか（値の有無は問わない）。エイリアス可。
  /// </summary>
  public bool Has(params string[] names)
  {
    return names.Any(n => _flags.Contains(n) || _values.ContainsKey(n));
  }

  /// <summary>
  /// 値を取得（エイリアス指定可、最初にヒットしたものを返す）。
  /// </summary>
  public string? Get(params string[] names)
  {
    foreach (var n in names)
      if (_values.TryGetValue(n, out var v)) return v;
    return null;
  }

  /// <summary>
  /// 値を取得し、無ければ既定値を返す。
  /// </summary>
  public string GetOr(string name, string defaultValue) => _values.GetValueOrDefault(name, defaultValue);

  /// <summary>
  /// 整数として取得（失敗時は null）。
  /// </summary>
  public int? GetInt(string name) => _values.TryGetValue(name, out var v) && int.TryParse(v, out var n) ? n : null;

  private static bool TryParseKey(string arg, out string key, out string? value)
  {
    key = string.Empty;
    value = null;

    var body = arg switch
    {
      { Length: > 2 } when arg.StartsWith("--") => arg[2..],
      { Length: > 1 } when arg.StartsWith('-') && !char.IsDigit(arg[1]) => arg[1..],
      _ => null,
    };
    if (body is null) return false;

    var sep = body.IndexOfAny(['=', ':']);
    if (sep > 0)
    {
      key = body[..sep];
      value = body[(sep + 1)..];
    }
    else
    {
      key = body;
    }
    return key.Length > 0;
  }

  private static bool IsKeyArg(string arg) => TryParseKey(arg, out _, out _);
}