using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Management;

namespace AttacheCase
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Text;
  using System.Security.Cryptography;
  using System.Management;

  /// <summary>
  /// INIファイルの警告理由を表す列挙型
  /// Enum representing the reason for INI file warnings
  /// </summary>
  internal enum WarningReason
  {
    /// <summary>警告なし（承認済み） / No warning (approved)</summary>
    None,
    /// <summary>新しいINIファイル / New INI file</summary>
    NewFile,
    /// <summary>承認済みのファイルが変更された / Approved file has been modified</summary>
    ModifiedFile,
    /// <summary>別のデバイスで承認されたファイル / File approved on a different device</summary>
    DifferentDevice,
    /// <summary>不明なエラー / Unknown error</summary>
    Unknown
  }

  /// <summary>
  /// INIファイルのセキュリティを管理するクラス
  /// 外部設定ファイルの検証と承認を処理する
  /// Class to manage the security of INI files
  /// Handles verification and approval of external configuration files
  /// </summary>
  internal class IniSecurityManager
  {
    // HMAC-SHA256 用の共有秘密鍵 (32 byte)。
    // .approved マーカーを公開情報 (SHA-256 + ボリュームシリアル) のみで偽造できないようにするために導入。
    // この鍵が更新された場合、既存の .approved マーカーは無効化され、初回起動時にユーザーへ
    // 再承認を求めるダイアログが表示される。
    //
    // 注意: AttacheCase は OSS のため、ソースを読める攻撃者にとってこの鍵は秘密ではない。
    //       それでも、「ユーザー権限の悪意ある INI を読まされる」局面で「INI の中身さえ書ければ
    //       誰でも .approved を再計算できる」状態よりは、攻撃ハードルを 1 段上げる効果がある。
    //       より強固にしたい場合は、ユーザー/マシン固有の値から鍵を導出する仕組みに置き換えること。
    private static readonly byte[] HmacKey = new byte[]
    {
      0x7d, 0x4c, 0x9a, 0xe2, 0x6f, 0x1b, 0x83, 0x95,
      0x4e, 0xa2, 0xd7, 0x18, 0xc3, 0x60, 0xfb, 0x49,
      0x21, 0xb5, 0xee, 0x82, 0x37, 0xa9, 0xd0, 0x66,
      0x5c, 0xb7, 0xf3, 0x14, 0x9e, 0x8d, 0x40, 0x6a
    };

    // 読み込んだINIファイルパス
    // Path of the loaded INI file
    private readonly string _iniPath;

    // 承認マーカーファイルのパス（_AtcCase.approved）
    // Path of the approval marker file (_AtcCase.approved)
    private readonly string _approvalMarkerPath;

    // INIファイルが詠み込めず、メモリ内での承認状態
    // Approval status in memory if the INI file cannot be read
    private bool _memoryApproved = false;

    // INIファイルの現在のデバイスIDとファイルハッシュ
    // Current device ID and file hash of the INI file
    private string _currentDeviceId = null;
    private string _currentFileHash = null;

    // アプリケーション起動時にINIファイルの初期状態を記録
    // Initial state of the INI file recorded at application startup
    private string _initialFileHash = null;

    /// <summary>
    /// IniSecurityManagerのインスタンスを初期化
    /// Initializes an instance of IniSecurityManager
    /// </summary>
    /// <param name="iniPath">INIファイルのパス / Path of the INI file</param>
    public IniSecurityManager(string iniPath)
    {
      _iniPath = iniPath;
      var directory = Path.GetDirectoryName(iniPath);
      var fileName = Path.GetFileNameWithoutExtension(iniPath);
      _approvalMarkerPath = Path.Combine(directory!, fileName + ".approved");
    }

    /// <summary>
    /// INIファイルの初期状態を記録する
    /// Record the initial state of the INI file
    /// </summary>
    public void RecordInitialState()
    {
      EnsureCurrentValues();
      _initialFileHash = _currentFileHash;
    }

    /// <summary>
    /// INIファイルが起動後に変更されたかどうかを確認
    /// Check if the INI file has been modified after startup
    /// </summary>
    /// <returns>
    /// 変更された場合はtrue、そうでない場合はfalse
    /// True if changed, false otherwise
    /// </returns>
    public bool HasChangedSinceStart()
    {
      if (_initialFileHash == null) return false;

      // 現在のハッシュを再計算
      _currentFileHash = CalculateFileHash(_iniPath);
      return _initialFileHash != _currentFileHash;
    }

    /// <summary>
    /// 変更があった場合のみ承認マーカーを更新
    /// Update approval markers only when changes are made
    /// </summary>
    /// <returns>
    /// 更新が行われた場合はtrue、それ以外はfalse
    /// True if an update has taken place, false otherwise
    /// </returns>
    public bool UpdateApprovalIfChanged()
    {
      if (HasChangedSinceStart())
      {
        return MarkAsApproved();
      }
      return false;
    }

    /// <summary>
    /// INIファイルの警告理由を取得する
    /// Gets the reason for the INI file warning
    /// </summary>
    /// <returns>
    /// 警告の理由を示す列挙型。警告が不要な場合はNone
    /// Enum indicating the reason for the warning. None if no warning is needed
    /// </returns>
    public WarningReason CheckWarning()
    {
      // メモリ内で承認済みならば警告不要
      // No warning needed if approved in memory
      if (_memoryApproved) return WarningReason.None;

      // INIファイルが存在しない場合は警告不要
      // No warning needed if the INI file does not exist
      if (!File.Exists(_iniPath)) return WarningReason.None;

      // 承認マーカーファイルが存在しない場合は新規ファイル
      // New file if the approval marker file does not exist
      if (!File.Exists(_approvalMarkerPath)) return WarningReason.NewFile;

      try
      {
        // 現在のデバイスIDとファイルハッシュの取得
        // Get the current device ID and file hash
        EnsureCurrentValues();

        // 保存されている承認情報の取得
        // Get the stored approval information
        var storedData = File.ReadAllBytes(_approvalMarkerPath);
        var storedContent = Encoding.UTF8.GetString(storedData);

        // 保存内容の分解
        // フォーマット: {HMAC}|{deviceID1}|{deviceID2}|... (デバイスIDは複数承認を許可)
        // Format: {HMAC}|{deviceID1}|{deviceID2}|... (multiple approved devices allowed)
        var parts = storedContent.Split('|');
        if (parts.Length < 2) return WarningReason.Unknown;

        var storedHash = parts[0];
        var storedDeviceIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < parts.Length; i++)
        {
          if (string.IsNullOrEmpty(parts[i]) == false) storedDeviceIds.Add(parts[i]);
        }

        // ファイルハッシュが異なる場合 (内容変更を優先して報告する)
        // If the file hash is different (report content modification first)
        if (storedHash != _currentFileHash) return WarningReason.ModifiedFile;

        // 承認済みデバイス一覧に現在のデバイスが含まれない場合
        // If the current device is not in the approved device list
        // NO_DEVICE_ID (デバイスID取得失敗) は以前バイパスを許容していたが、
        // 攻撃者が .approved に "NO_DEVICE_ID" を埋め込むだけでデバイス検証を回避できる
        // 抜け道になるため、厳密一致に変更した。
        // 取得失敗が常態化する環境 (一部のネットワークドライブ等) では、毎回 DifferentDevice
        // 警告が出るが、利便性よりも検証の厳密性を優先する。
        if (storedDeviceIds.Contains(_currentDeviceId) == false) return WarningReason.DifferentDevice;

        // すべてのチェックに合格
        // All checks passed
        return WarningReason.None;
      }
      catch
      {
        // 例外発生時は不明なエラー
        // Unknown error in case of exception
        return WarningReason.Unknown;
      }
    }

    /// <summary>
    /// 現在のINIファイルを承認済みとしてマークする
    /// Marks the current INI file as approved
    /// </summary>
    /// <returns>
    /// 承認情報の保存に成功した場合はtrue、それ以外はfalse
    /// true if the approval information is successfully saved, otherwise false
    /// </returns>
    public bool MarkAsApproved()
    {
      try
      {
        // 現在の値を確実に取得
        // Ensure current values are obtained
        EnsureCurrentValues();

        // 既存マーカーから承認済みデバイス一覧を読み出し、現在のデバイスを追加する。
        // 同一 USB を複数 PC に挿して使うポータブル運用を想定し、承認済みデバイスは
        // 内容変更があっても引き継ぐ (PC#A → PC#B → PC#A の往復で毎回ダイアログが出ないように)。
        // 「承認済みデバイス間で互いに信頼する」というモデル上、ある PC で INI が書き換えられた場合、
        // 他の承認済み PC ではそれが伝播してくる。この設計は portable use 優先のトレードオフ。
        // Read the existing approved-device list and append the current device.
        // Approved devices are preserved across content changes to support portable use
        // (e.g., the same USB stick on multiple PCs). This implies trust between approved
        // devices: a change made on one approved PC is silently accepted on the others.
        var devices = ReadStoredDeviceIds();
        if (devices.Contains(_currentDeviceId) == false) devices.Add(_currentDeviceId);

        // 承認情報の構築
        // フォーマット: {HMAC}|{deviceID1}|{deviceID2}|...
        // Build the approval information. Format: {HMAC}|{deviceID1}|{deviceID2}|...
        var content = _currentFileHash + "|" + string.Join("|", devices);
        var data = Encoding.UTF8.GetBytes(content);

        // マーカーが既に存在する場合のみ、隠し/読み取り専用属性を解除する
        // (File.GetAttributes は存在しないファイルに対して例外を投げるため、存在チェックが必要)
        // Only clear attributes if the marker already exists; File.GetAttributes throws
        // when the file does not exist, so guard with File.Exists.
        if (File.Exists(_approvalMarkerPath))
        {
          var attributes = File.GetAttributes(_approvalMarkerPath);
          if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
              (attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
          {
            attributes = attributes & ~FileAttributes.Hidden & ~FileAttributes.ReadOnly;
            File.SetAttributes(_approvalMarkerPath, attributes);
          }
        }

        // ファイルへの書き込み
        // Write to the file
        File.WriteAllBytes(_approvalMarkerPath, data);

        try
        {
          // 可能であれば隠しファイル属性を設定
          // Set hidden attribute if possible
          File.SetAttributes(_approvalMarkerPath, FileAttributes.Hidden);
        }
        catch
        {
          // 属性設定の失敗は無視
          // Ignore failure to set attributes
        }

        // メモリ内に承認状態を記録
        // Record approval status in memory
        _memoryApproved = true;
        return true;
      }
      catch
      {
        // ファイル書き込みに失敗した場合もメモリ内には保存
        // Save in memory even if file write fails
        _memoryApproved = true;
        return false;
      }
    }

    /// <summary>
    /// 既存の .approved マーカーから承認済みデバイスID一覧を読み出す。
    /// マーカーが存在しないか壊れている場合は空のリストを返す。
    /// Read the list of approved device IDs from the existing .approved marker.
    /// Returns an empty list if the marker is missing or unparseable.
    /// </summary>
    private List<string> ReadStoredDeviceIds()
    {
      var devices = new List<string>();
      if (File.Exists(_approvalMarkerPath) == false) return devices;
      try
      {
        var data = File.ReadAllBytes(_approvalMarkerPath);
        var content = Encoding.UTF8.GetString(data);
        var parts = content.Split('|');
        for (var i = 1; i < parts.Length; i++)
        {
          if (string.IsNullOrEmpty(parts[i])) continue;
          if (devices.Contains(parts[i]) == false) devices.Add(parts[i]);
        }
      }
      catch
      {
        // パース失敗時は空リスト扱い (新規承認として扱われる)
        // Treat parse failure as no existing devices (will be a fresh approval)
      }
      return devices;
    }

    /// <summary>
    /// 現在のデバイスIDとファイルハッシュを確実に取得
    /// Ensure the current device ID and file hash are obtained
    /// </summary>
    private void EnsureCurrentValues()
    {
      _currentDeviceId ??= GetDeviceIdentifier();
      _currentFileHash ??= CalculateFileHash(_iniPath);
    }

    /// <summary>
    /// 承認情報がメモリ内のみで保持されているかどうかを取得
    /// Check if the approval information is held only in memory
    /// </summary>
    /// <returns>
    /// 承認がメモリ内のみの場合はtrue、ファイルに保存されている場合はfalse
    /// true if approval is only in memory, false if saved to file
    /// </returns>
    public bool IsTemporaryApproval()
    {
      return _memoryApproved && !File.Exists(_approvalMarkerPath);
    }

    /// <summary>
    /// デバイスの固有識別子を取得
    /// Get the unique identifier of the device
    /// </summary>
    /// <returns>
    /// デバイスの固有識別子、取得できない場合は特殊な値
    /// Unique identifier of the device, special value if not obtainable
    /// </returns>
    private string GetDeviceIdentifier()
    {
      try
      {
        // INIファイルが存在するドライブのドライブレターを取得
        // Get the drive letter of the drive where the INI file exists
        var drivePath = Path.GetPathRoot(_iniPath);
        if (string.IsNullOrEmpty(drivePath)) return "NO_DEVICE_ID";

        var driveLetter = drivePath.Replace(":\\", "");

        // WMIを使用してボリュームシリアル番号を取得
        // Get the volume serial number using WMI
        using var disk = new ManagementObject("win32_logicaldisk.deviceid=\"" + driveLetter + ":\"");
        disk.Get();

        if (disk["VolumeSerialNumber"] != null)
        {
          return disk["VolumeSerialNumber"].ToString();
        }
      }
      catch
      {
        // デバイスID取得に失敗した場合
        // If device ID retrieval fails
      }

      return "NO_DEVICE_ID";
    }

    /// <summary>
    /// ファイルの HMAC-SHA256 (共有秘密鍵によるキー付きハッシュ) を計算する。
    /// 戻り値のフォーマットは旧 SHA-256 ハッシュと同じ Base64 文字列だが、
    /// 鍵を持たない攻撃者は同じ値を再計算できないため、.approved マーカーの偽造耐性が上がる。
    /// Compute the HMAC-SHA256 (keyed hash) of the file using the embedded secret key.
    /// Output format is the same Base64 string as the previous SHA-256 hash, but an attacker
    /// without the key cannot recompute the same value, making the .approved marker
    /// resistant to forgery.
    /// </summary>
    private static string CalculateFileHash(string filePath)
    {
      using var hmac = new HMACSHA256(HmacKey);
      using var stream = File.OpenRead(filePath);
      var hash = hmac.ComputeHash(stream);
      return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// デバイスIDが取得可能かどうかを確認
    /// Check if the device ID is obtainable
    /// </summary>
    /// <returns>
    /// デバイスIDが有効な場合はtrue、それ以外はfalse
    /// True if the device ID is valid, otherwise false</returns>
    public bool HasValidDeviceId()
    {
      _currentDeviceId ??= GetDeviceIdentifier();
      return _currentDeviceId != "NO_DEVICE_ID";
    }

    /// <summary>
    /// 現在のINIファイルの詳細情報を取得
    /// Get detailed information about the current INI file
    /// </summary>
    /// <returns>
    /// ファイルとデバイスの詳細情報を含む文字列
    /// String containing detailed information about the file and device
    /// </returns>
    public string GetDebugInfo()
    {
      EnsureCurrentValues();

      var sb = new StringBuilder();
      sb.AppendLine($"INIファイルパス: {_iniPath} / INI file path: {_iniPath}");
      sb.AppendLine($"承認マーカーパス: {_approvalMarkerPath} / Approval marker path: {_approvalMarkerPath}");
      sb.AppendLine($"INIファイル存在: {File.Exists(_iniPath)} / INI file exists: {File.Exists(_iniPath)}");
      sb.AppendLine($"承認マーカー存在: {File.Exists(_approvalMarkerPath)} / Approval marker exists: {File.Exists(_approvalMarkerPath)}");
      sb.AppendLine($"デバイスID: {_currentDeviceId} / Device ID: {_currentDeviceId}");
      sb.AppendLine($"ファイルハッシュ: {_currentFileHash} / File hash: {_currentFileHash}");
      sb.AppendLine($"メモリ内承認: {_memoryApproved} / Memory approval: {_memoryApproved}");
      sb.AppendLine($"警告理由: {CheckWarning()} / Warning reason: {CheckWarning()}");

      return sb.ToString();
    }
  }
}
