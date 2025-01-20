//---------------------------------------------------------------------- 
// "アタッシェケース4 ( AttachéCase4 )" -- File encryption software.
// Copyright (C) 2016-2025  Mitsuhiro Hibara
//
// * Required .NET Framework 4.6 or later
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.If not, see<http://www.gnu.org/licenses/>.
//---------------------------------------------------------------------- 
using AttacheCase.Properties;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
#if __MACOS__
using AppKit;
#endif

namespace AttacheCase
{
  internal partial class FileEncrypt4
  {
    // Status code
    private const int ENCRYPT_SUCCEEDED = 1; // Encrypt is succeeded.
    private const int DECRYPT_SUCCEEDED = 2; // Decrypt is succeeded.
    private const int DELETE_SUCCEEDED = 3; // Delete is succeeded.
    private const int READY_FOR_ENCRYPT = 4; // Getting ready for encryption or decryption.
    private const int READY_FOR_DECRYPT = 5; // Getting ready for encryption or decryption.
    private const int ENCRYPTING = 6; // Encrypting.
    private const int DECRYPTING = 7; // Decrypting.
    private const int DELETING = 8; // Deleting.

    // Error code
    private const int USER_CANCELED = -1;   // User cancel.
    private const int ERROR_UNEXPECTED = -100;
    private const int NOT_ATC_DATA = -101;
    private const int ATC_BROKEN_DATA = -102;
    private const int NO_DISK_SPACE = -103;
    private const int FILE_INDEX_NOT_FOUND = -104;
    private const int PASSWORD_TOKEN_NOT_FOUND = -105;
    private const int NOT_CORRECT_HASH_VALUE = -106;
    private const int INVALID_FILE_PATH = -107;
    private const int OS_DENIES_ACCESS = -108;
    private const int DATA_NOT_FOUND = -109;
    private const int DIRECTORY_NOT_FOUND = -110;
    private const int DRIVE_NOT_FOUND = -111;
    private const int FILE_NOT_LOADED = -112;
    private const int FILE_NOT_FOUND = -113;
    private const int PATH_TOO_LONG = -114;
    private const int CRYPTOGRAPHIC_EXCEPTION = -115;
    private const int RSA_KEY_GUID_NOT_MATCH = -116;
    private const int IO_EXCEPTION = -117;

    private byte[] buffer;
    private const int BUFFER_SIZE = 8192;

    // Header data variables
    private static bool isBrocken = false;
    private const string STRING_TOKEN_NORMAL = "_AttacheCaseData";
    private const string STRING_TOKEN_BROKEN = "_Atc_Broken_Data";
    private const string STRING_TOKEN_RSA = "_AttacheCase_Rsa";
    private const int DATA_FILE_VERSION = 140;  // ver.4
    private const string ATC_ENCRYPTED_TOKEN = "atc4";

    //Encrypted header data size
    private int _AtcHeaderSize = 0;
    private long _TotalSize = 0;
    //private Int64 _TotalFileSize = 0;
    private long _StartPos = 0;

#if __MACOS__
    private bool _fCancel = false;
    public bool fCancel
    {
        get { return this._fCancel; }
        set { this._fCancel = value; }
    }
#endif

    // The number of files or folders to be encrypted
    public int NumberOfFiles { get; set; } = 0;

    // Total number of files or folders to be encrypted
    public int TotalNumberOfFiles { get; set; } = 1;

    // Set number of times to input password in encrypt files:
    public char MissTypeLimits { get; set; } = (char)3;

    // Self-executable file
    public bool fExecutable { get; set; } = false;

    // .NET Framework Version of Self-executable file  
    public string ExeToolVersionString { get; set; } = "4.6.2";

    // Set the timestamp of encryption file to original files or directories
    public bool fKeepTimeStamp { get; set; } = false;

    // Set the Compression Option ( 0: Optimal, 1: Fastest, 2: NoCompression)
    public int CompressionOption { get; set; } = 1;

    // ATC file ( encrypted file name ) path to output
    public string AtcFilePath { get; private set; } = "";

    // List of files and folders for encryption
    public List<string> FileList { get; private set; }

#if DEBUG
    // 処理するファイル数、ディレクトリ数
    private int fileCount = 0;
    private int dirCount = 0;
#endif

    // Encryption time
    public string EncryptionTimeString { get; private set; }

    // Guid
    public string GuidString { get; set; }

    // RSA Encryption public key XML string
    private string _RsaPublicKeyXmlString;
    public string RsaPublicKeyXmlString
    {
      get => this._RsaPublicKeyXmlString;
      set
      {
        this._RsaPublicKeyXmlString = value;
        this.fRsaEncryption = true;
      }
    }
    // RSA Encryption
    public bool fRsaEncryption { get; private set; } = false;

    //----------------------------------------------------------------------
    // The return value of error ( ReadOnly)
    //----------------------------------------------------------------------
    // Input "Error code" for value
    public int ReturnCode { get; private set; } = -1;

    // File path that caused the error
    public string ErrorFilePath { get; private set; } = "";

    // Drive name to decrypt
    public string DriveName { get; private set; } = "";

    // Total file size of files to be encrypted
    public long TotalFileSize { get; private set; } = 0;

    // Free space on the drive to encrypt the file
    public long AvailableFreeSpace { get; private set; } = -1;

    // Error message by the exception
    public string ErrorMessage { get; private set; } = "";

    private volatile bool _isCancelled;

    private ConcurrentBag<FileSystemEntry> resultsConcurrentBag;
    private volatile int _processedFileCount;
    private readonly Stopwatch swProgress = new Stopwatch();

    public void CancelEncryption()
    {
      _isCancelled = true;
    }

#if DEBUG
    private Stopwatch swDebugEncrypt = new Stopwatch();
#endif


    // Constructor
    public FileEncrypt4()
    {
    }

    /// <summary>
    /// 指定されたファイルを、提供された設定に基づいて暗号化されたアーカイブに変換します。
    /// </summary>
    /// <param name="sender">イベントの発生元。通常は <see cref="BackgroundWorker"/> です。</param>
    /// <param name="e">イベントデータを含む <see cref="DoWorkEventArgs"/>。</param>
    /// <param name="FilePaths">暗号化対象のファイルパスの配列。</param>
    /// <param name="OutFilePath">暗号化されたアーカイブの出力先ファイルパス。</param>
    /// <param name="Password">暗号化に使用するパスワード（文字列形式）。</param>
    /// <param name="PasswordBinary">暗号化に使用するパスワード（バイナリ形式）。</param>
    /// <param name="NewArchiveName">作成される新しいアーカイブの名前。</param>
    /// <param name="compressionLevel">アーカイブに適用する圧縮レベル。<see cref="CompressionLevel"/> で指定します。</param>
    /// <returns>
    /// 暗号化処理が成功した場合は <c>true</c>、それ以外の場合は <c>false</c> を返します。
    /// </returns>
    public bool Encrypt(
      object sender, DoWorkEventArgs e,
      string[] FilePaths, string OutFilePath, string Password, byte[] PasswordBinary,
      string NewArchiveName, CompressionLevel compressionLevel)
    {
      // Stopwatch for measuring time and adjusting the progress bar display
      var swProgress = new Stopwatch();
      swProgress.Restart();

#if (DEBUG)
      var lg = new Logger();
      lg.Info("-----------------------------------");
      lg.Info(OutFilePath);
      lg.Info("Encryption start.");
      lg.StopWatchStart();
      var swEncrypt = new Stopwatch();
      swEncrypt.Restart();
#endif

      AtcFilePath = OutFilePath;

      var worker = sender as BackgroundWorker;
      if (ShouldCancel(worker))
      {
        e.Cancel = true;
        return false;
      }
      // The timestamp of original file
      var dtCreate = File.GetCreationTime(FilePaths[0]);
      var dtUpdate = File.GetLastWriteTime(FilePaths[0]);
      var dtAccess = File.GetLastAccessTime(FilePaths[0]);

      // Create Header data.
      var MessageList = new ArrayList
      {
        READY_FOR_ENCRYPT,
        Path.GetFileName(AtcFilePath)
      };

      worker?.ReportProgress(0, MessageList);
      FileList = [];

      // RSA Encryption password
      var byteRsaPassword = new byte[32]; // Key size
      if (fRsaEncryption == true)
      {
        using var rng = new RNGCryptoServiceProvider();
        rng.GetBytes(byteRsaPassword);
        PasswordBinary = byteRsaPassword;
      }

      // Salt
      Rfc2898DeriveBytes deriveBytes;
      if (PasswordBinary == null)
      { // String Password
        deriveBytes = new Rfc2898DeriveBytes(Password, 8, 1000);
      }
      else
      {
        // Binary Password
        var random_salt = new byte[8];
        var rng = new RNGCryptoServiceProvider();
        rng.GetBytes(random_salt);
        deriveBytes = new Rfc2898DeriveBytes(PasswordBinary, random_salt, 1000);
      }

      var salt = deriveBytes.Salt;
      var key = deriveBytes.GetBytes(32);
      var iv = deriveBytes.GetBytes(16);

#if (DEBUG)
      var debugString = BitConverter.ToString(salt);
      Console.WriteLine($@"salt: {debugString}");
      debugString = BitConverter.ToString(key);
      Console.WriteLine($@"key: {debugString}");
      debugString = BitConverter.ToString(iv);
      Console.WriteLine($@"iv: {debugString}");
#endif

      try
      {
        var outfs = new FileStream(AtcFilePath, FileMode.Create, FileAccess.Write);
        // 自己実行形式ファイル（Self-executable file）
        if (fExecutable == true)
        {
          // public partial class FileEncrypt4
          // Read from ExeOut4.cs
          if (ExeToolVersionString == "4.0")
          {
            ExeOutFileSize[0] = rawData[0].Length;
            outfs.Write(rawData[0], 0, ExeOutFileSize[0]);
          }
          else // 4.6.2
          {
            ExeOutFileSize[1] = rawData[1].Length;
            outfs.Write(rawData[1], 0, ExeOutFileSize[1]);
          }
        }

        _StartPos = outfs.Seek(0, SeekOrigin.End);

        // Application version
        var ver = AppInfo.Version;
        var vernum = short.Parse(ver.ToString().Replace(".", ""));
        var byteArray = BitConverter.GetBytes(vernum);
        outfs.Write(byteArray, 0, 2);
        // Input password limit
        byteArray = BitConverter.GetBytes(MissTypeLimits);
        outfs.Write(byteArray, 0, 1);
        // Exceed the password input limit, destroy the file?
        byteArray = BitConverter.GetBytes(isBrocken);
        outfs.Write(byteArray, 0, 1);
        // Token that this is the AttacheCase file
        byteArray = Encoding.ASCII.GetBytes(fRsaEncryption == true ? STRING_TOKEN_RSA : STRING_TOKEN_NORMAL);

        outfs.Write(byteArray, 0, 16);
        // File sub version
        byteArray = BitConverter.GetBytes(DATA_FILE_VERSION);
        outfs.Write(byteArray, 0, 4);
        // The size of encrypted Atc header size ( reserved ) 
        byteArray = BitConverter.GetBytes((int)0);
        outfs.Write(byteArray, 0, 4);

        // GUID
        Guid guid;
        if (string.IsNullOrEmpty(RsaPublicKeyXmlString) == false)
        {
          // Read GUID binary from RSA public key string
          var xmlElement = XElement.Parse(RsaPublicKeyXmlString);
          GuidString = xmlElement.Element("id")!.Value;
          guid = Guid.Parse(GuidString);
        }
        else
        {
          // New GUID
          guid = Guid.NewGuid();
          GuidString = guid.ToString();
        }

        outfs.Write(guid.ToByteArray(), 0, 16);

        // Salt
        outfs.Write(salt, 0, 8);

        // RSA encryption password
        if (fRsaEncryption == true)
        {
          // パスワードを暗号化して書き込む
          //RSACryptoServiceProviderオブジェクトの作成
          var rsa = new RSACryptoServiceProvider(2048);
          rsa.FromXmlString(_RsaPublicKeyXmlString); //公開鍵を指定
          //byte[] outbuffer = new byte[214];  // 剰余サイズ(256bytes) -2 -2 * hLen(SHA-1) = 214 Max 
          //string debugString = BitConverter.ToString(byteRsaPassword);
          //Console.WriteLine(debugString);
          var encryptedData = rsa.Encrypt(byteRsaPassword, RSAEncryptionPadding.OaepSHA1); //OAEPパディング=trueでRSA復号
          outfs.Write(encryptedData, 0, encryptedData.Length); // 256 byte
        }

        //-----------------------------------
        // 暗号化ヘッダー
        // Cipher text header
        //-----------------------------------
        var ms = new MemoryStream();

        // Token to refer to when decryption is successful
        byteArray = Encoding.ASCII.GetBytes(ATC_ENCRYPTED_TOKEN);
        ms.Write(byteArray, 0, 4);

#if DEBUG
        var DebugList = new List<string>();
        var OneLine = "";
#endif
        //----------------------------------------------------------------------
        // Put together files in one ( Save as the name ).
        // 複数ファイルを一つにまとめる（ファイルに名前をつけて保存）
        if (NewArchiveName != "")
        {
#if __MACOS__
            NewArchiveName = NewArchiveName + "/";
#else
          if (NewArchiveName.EndsWith("\\") == false)
          {
            NewArchiveName = NewArchiveName + "\\";
          }
#endif
          // File name length
          var FileLen = Encoding.UTF8.GetByteCount(NewArchiveName);
          byteArray = BitConverter.GetBytes((short)FileLen);
          ms.Write(byteArray, 0, 2);

          // File name
          byteArray = Encoding.UTF8.GetBytes(NewArchiveName);
          ms.Write(byteArray, 0, FileLen);

          // File size (Directory)
          const int fileSize = 0;
          byteArray = BitConverter.GetBytes((long)0);
          ms.Write(byteArray, 0, 8);

          // File attribute (Directory)
          const int fileAttr = 16;
          byteArray = BitConverter.GetBytes((int)16);
          ms.Write(byteArray, 0, 4);

          var LastWriteDateString = DateTime.UtcNow.ToString("yyyyMMdd");
          var LastWriteTimeString = DateTime.UtcNow.ToString("HHmmss");
          // Last write date
          byteArray = BitConverter.GetBytes(int.Parse(LastWriteDateString));
          ms.Write(byteArray, 0, 4);
          // Last write time
          byteArray = BitConverter.GetBytes(int.Parse(LastWriteTimeString));
          ms.Write(byteArray, 0, 4);

          var CreationDateString = DateTime.UtcNow.ToString("yyyyMMdd");
          var CreationTimeString = DateTime.UtcNow.ToString("HHmmss");
          // Creation date
          byteArray = BitConverter.GetBytes(int.Parse(CreationDateString));
          ms.Write(byteArray, 0, 4);
          // Creation time
          byteArray = BitConverter.GetBytes(int.Parse(CreationTimeString));
          ms.Write(byteArray, 0, 4);
#if DEBUG
          OneLine += $@"{FileLen}\t";
          OneLine += $@"{NewArchiveName}\t";
          OneLine += $@"{fileSize}\t";
          OneLine += $@"{fileAttr}\t";
          OneLine += $@"{LastWriteDateString}\t";
          OneLine += $@"{LastWriteTimeString}\t";
          OneLine += $@"{CreationDateString}\t";
          OneLine += $@"{CreationTimeString}";
          DebugList.Add(OneLine);
          OneLine = "";
          dirCount++;
#endif
        }

        //----------------------------------------------------------------------
        // When encrypt multiple files
        // 複数のファイルを暗号化する場合
        foreach (var FilePath in FilePaths)
        {
          var ParentPath = Path.GetDirectoryName(FilePath) ?? FilePath;
#if __MACOS__
              if (ParentPath.EndsWith("/") == false)
              {
                ParentPath = ParentPath + "/";
              }
#else
          if (ParentPath.EndsWith("\\") == false)
          {
            ParentPath = ParentPath + "\\";
          }
#endif
          if ((worker.CancellationPending == true))
          {
            e.Cancel = true;
            return (false);
          }

          //-----------------------------------
          // 暗号化リストを生成（ファイル）
          // Create file to encrypt list ( File )
          //-----------------------------------
          if (File.Exists(FilePath))
          {
            var entry = GetFileInfo(ParentPath, FilePath, () => ShouldCancel(worker));

            if (_isCancelled || worker.CancellationPending)
            {
              // キャンセル待ち
              e.Cancel = true;
              return (false);
            }

            TotalFileSize += FileInfoStreamWriter(ref ms, entry, NewArchiveName);
            FileList.Add(entry.FullPath);
#if DEBUG
            OneLine += $"{entry.FullPath}\t";
            OneLine += $"{entry.Size}\t";
            OneLine += $"{entry.Attributes}\t";
            OneLine += $"{entry.LastWriteDate}\t";
            OneLine += $"{entry.LastWriteTime}\t";
            OneLine += $"{entry.CreationDate}\t";
            OneLine += $"{entry.CreationTime}";
            DebugList.Add(OneLine);
            OneLine = "";
            fileCount++;
#endif
          }
          //-----------------------------------
          // 暗号化リストを生成（ディレクトリ）
          // Create file to encrypt list ( Directory )
          //-----------------------------------
          else
          {
            var entryList = GetFileList(ParentPath, FilePath, () => ShouldCancel(worker), worker);
            foreach (var entry in entryList)
            {
              if (_isCancelled || worker.CancellationPending)
              {
                e.Cancel = true;
                return false;
              }

              if (entry.IsDirectory)
              {
                TotalFileSize += FileInfoStreamWriter(ref ms, entry, NewArchiveName);
                FileList.Add(entry.FullPath);
#if DEBUG
                OneLine += $"{entry.FullPath}\t";
                OneLine += $"{entry.Size}\t";
                OneLine += $"{entry.Attributes}\t";
                OneLine += $"{entry.LastWriteDate}\t";
                OneLine += $"{entry.LastWriteTime}\t";
                OneLine += $"{entry.CreationDate}\t";
                OneLine += $"{entry.CreationTime}";
                DebugList.Add(OneLine);
                OneLine = "";
                dirCount++;
#endif
              }
              else
              {
                TotalFileSize += FileInfoStreamWriter(ref ms, entry, NewArchiveName);
                // files only ( Add Files list for encryption )
                FileList.Add(entry.FullPath);
#if DEBUG
                OneLine += $"{entry.FullPath}\t";
                OneLine += $"{entry.Size}\t";
                OneLine += $"{entry.Attributes}\t";
                OneLine += $"{entry.LastWriteDate}\t";
                OneLine += $"{entry.LastWriteTime}\t";
                OneLine += $"{entry.CreationDate}\t";
                OneLine += $"{entry.CreationTime}";
                DebugList.Add(OneLine);
                OneLine = "";
                fileCount++;
#endif
              }

            } // end foreach (ArrayList Item in GetFilesList(ParentPath, FilePath));

          } // if (File.Exists(FilePath) == true);

        } // end foreach (string FilePath in FilePaths);
#if DEBUG
        var DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        using var sw = new StreamWriter(Path.Combine(DesktopDir, "_encrypt_header.txt"), false, Encoding.UTF8);
        foreach (var line in DebugList)
        {
          sw.WriteLine(line);
        }
#endif
        //----------------------------------------------------------------------
        // Check the disk space
        //----------------------------------------------------------------------
        var RootDriveLetter = Path.GetPathRoot(AtcFilePath).Substring(0, 1);

        if (RootDriveLetter == "\\")
        {
          // Network
        }
        else
        {
          var drive = new DriveInfo(RootDriveLetter);

          var driveType = drive.DriveType;
          switch (driveType)
          {
            case DriveType.CDRom:
            case DriveType.NoRootDirectory:
            case DriveType.Unknown:
              break;
            case DriveType.Fixed: // Local Drive
            case DriveType.Network: // Mapped Drive
            case DriveType.Ram: // Ram Drive
            case DriveType.Removable: // Usually a USB Drive

              // The drive is not available, or not enough free space.
              if (drive.IsReady == false || drive.AvailableFreeSpace < TotalFileSize)
              {
                // not available free space
                ReturnCode = NO_DISK_SPACE;
                DriveName = drive.ToString();
                //_TotalFileSize = _TotalFileSize;
                AvailableFreeSpace = drive.AvailableFreeSpace;
                return (false);
              }

              break;
            default:
              throw new ArgumentOutOfRangeException();
          }
        }

        // バッファの初期化
        buffer = new byte[BUFFER_SIZE];
        using (var aesManaged = new AesManaged())
        {
          aesManaged.BlockSize = 128;
          aesManaged.KeySize = 256;
          aesManaged.Mode = CipherMode.CBC;
          aesManaged.Padding = PaddingMode.PKCS7;
          aesManaged.Key = key;
          aesManaged.IV = iv;

          ms.Position = 0;
          //Encryption interface.
          var encryptor = aesManaged.CreateEncryptor(aesManaged.Key, aesManaged.IV);
          using (var cse = new CryptoStream(outfs, encryptor, CryptoStreamMode.Write))
          {
            //----------------------------------------------------------------------
            // ヘッダーの暗号化
            //----------------------------------------------------------------------
            int atcHeaderSize;
            _AtcHeaderSize = 0;
            while ((atcHeaderSize = ms.Read(buffer, 0, BUFFER_SIZE)) > 0)
            {
              cse.Write(buffer, 0, atcHeaderSize);
              _AtcHeaderSize += atcHeaderSize;
            }
          }
        }

        //-----------------------------------
        // ヘッダーサイズの書き込み（平文）
        //-----------------------------------

        outfs.Close();  // 既存のストリームを閉じる
        outfs = new FileStream(AtcFilePath, FileMode.Open, FileAccess.Write);  // 再オープン

        // 実行ファイルならば書き込み位置の調整
        if (fExecutable == true)
        {
          if (ExeToolVersionString == "4.0")
          {
            outfs.Seek(ExeOutFileSize[0] + 24, SeekOrigin.Begin); // self executable file
          }
          else
          {
            outfs.Seek(ExeOutFileSize[1] + 24, SeekOrigin.Begin); // self executable file
          }
        }
        else
        {
          outfs.Seek(24, SeekOrigin.Begin);
        }

        // ヘッダーサイズ（int）を平文で書き込む
        byteArray = BitConverter.GetBytes(_AtcHeaderSize);
        outfs.Write(byteArray, 0, 4);

        // Out file stream position move to end
        outfs.Seek(0, SeekOrigin.End);

        //----------------------------------------------------------------------
        // 本体データの暗号化
        //----------------------------------------------------------------------
        float percent = 0;
        // Encryption interface.
        using (var aesManaged = new AesManaged())
        {
          aesManaged.BlockSize = 128;
          aesManaged.KeySize = 256;
          aesManaged.Mode = CipherMode.CBC;
          aesManaged.Padding = PaddingMode.PKCS7;
          aesManaged.Key = key;
          aesManaged.IV = iv;
          var encryptor = aesManaged.CreateEncryptor(aesManaged.Key, aesManaged.IV);

          using (var cse = new CryptoStream(outfs, encryptor, CryptoStreamMode.Write))
          {
            using (var ds = new DeflateStream(cse, compressionLevel))
            {
              foreach (var path in FileList)  // Where句を除去
              {
                if (File.Exists(path) && !Directory.Exists(path))  // ファイルの場合のみ暗号化
                {
                  buffer = new byte[BUFFER_SIZE];

                  using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                  {
                    var len = 0;
                    while ((len = fs.Read(buffer, 0, BUFFER_SIZE)) > 0)
                    {
                      ds.Write(buffer, 0, len);
                      _TotalSize += len;
                      UpdateProgress(len);

                      string MessageText;
                      if (TotalNumberOfFiles > 1)
                      {
                        MessageText = path + $" ( {NumberOfFiles} / {TotalNumberOfFiles} files )";
                      }
                      else
                      {
                        MessageText = path;
                      }

                      MessageList = new ArrayList
                      {
                        ENCRYPTING,
                        MessageText
                      };

                      // Adjusted the progress bar update interval to 100ms
                      if (swProgress.ElapsedMilliseconds > 100)
                      {
                        percent = ((float)_processedSize / TotalFileSize);
                        worker.ReportProgress((int)(percent * 10000), MessageList);
                        swProgress.Restart();

                        // worker.CancellationPending のチェックを ShouldCancel に置き換え
                        if (ShouldCancel(worker))
                        {
                          e.Cancel = true;
                          return false;
                        }
                      }

                    } // end while ((len = fs.Read(buffer, 0, BUFFER_SIZE)) > 0)

                  } // end using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))

                } // end if (File.Exists(path) && !Directory.Exists(path))

              } // end foreach (string path in _FileList);

            } // end using (var ds = new DeflateStream(cse, compressionLevel))

          } // end using (var cse = new CryptoStream(outfs, encryptor, CryptoStreamMode.Write))

        }

        // Set the timestamp of encryption file to original files or directories
        if (fKeepTimeStamp == true)
        {
          File.SetCreationTime(AtcFilePath, dtCreate);
          File.SetLastWriteTime(AtcFilePath, dtUpdate);
          File.SetLastAccessTime(AtcFilePath, dtAccess);
        }
        else
        {
          dtUpdate = DateTime.Now;
          File.SetLastWriteTime(AtcFilePath, dtUpdate);
        }

#if DEBUG
        swDebugEncrypt.Stop();
        var ts = swEncrypt.Elapsed;
        _EncryptionTimeString = AtcFilePath + Environment.NewLine +
          Convert.ToString(ts.Hours) + "h" + Convert.ToString(ts.Minutes) + "m" +
          Convert.ToString(ts.Seconds) + "s" + Convert.ToString(ts.Milliseconds) + "ms";
        MessageBox.Show(_EncryptionTimeString, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
#endif
        //Encryption succeed.
        ReturnCode = ENCRYPT_SUCCEEDED;
        return (true);

      }
      catch (OperationCanceledException)
      {
        e.Cancel = true;
        return false;
      }
      catch (Exception ex)
      {
        // 例外エラーをまとめて処理する
        HandleEncryptionException(ex);
        return false;
      }
      finally
      {
#if (DEBUG)
        lg.StopWatchStop();
        lg.Info("encryption finished!");

        swEncrypt.Stop();
        swProgress.Stop();

        // 計測時間
        var ts = swEncrypt.Elapsed;
        EncryptionTimeString = $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s {ts.Milliseconds}ms";
        //MessageBox.Show(EncryptionTimeString, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
#endif

      }

    }

    private void HandleEncryptionException(Exception ex)
    {
      switch (ex)
      {
        case UnauthorizedAccessException _:
          // オペレーティング システムが I/O エラーまたは特定の種類のセキュリティエラーのためにアクセスを拒否する場合、スローされる例外
          // The exception that is thrown when the operating system denies access
          // because of an I/O error or a specific type of security error.
          ReturnCode = OS_DENIES_ACCESS;
          ErrorFilePath = AtcFilePath;
          break;
        case DirectoryNotFoundException _:
          // ファイルまたはディレクトリの一部が見つからない場合にスローされる例外
          // The exception that is thrown when part of a file or directory cannot be found
          ReturnCode = DIRECTORY_NOT_FOUND;
          ErrorMessage = ex.Message;
          break;
        case DriveNotFoundException _:
          // 使用できないドライブまたは共有にアクセスしようとするとスローされる例外
          // The exception that is thrown when trying to access a drive or share that is not available
          ReturnCode = DRIVE_NOT_FOUND;
          ErrorMessage = ex.Message;
          break;
        case FileLoadException fileLoadEx:
          // マネージド アセンブリが見つかったが、読み込むことができない場合にスローされる例外
          // The exception that is thrown when a managed assembly is found but cannot be loaded
          ReturnCode = FILE_NOT_LOADED;
          ErrorMessage = fileLoadEx.FileName;
          break;
        case FileNotFoundException fileNotFoundEx:
          // ディスク上に存在しないファイルにアクセスしようとして失敗したときにスローされる例外
          // The exception that is thrown when an attempt to access a file that does not exist on disk fails
          ReturnCode = FILE_NOT_FOUND;
          ErrorMessage = fileNotFoundEx.FileName;
          break;
        case PathTooLongException _:
          // パス名または完全修飾ファイル名がシステム定義の最大長を超えている場合にスローされる例外
          // The exception that is thrown when a path or fully qualified file name is longer than the system-defined maximum length
          ReturnCode = PATH_TOO_LONG;
          break;
        case CryptographicException _:
          // 暗号操作中にエラーが発生すると、スローされる例外
          // Exception thrown if an error occurs during a cryptographic operation
          //
          // ※主に、RSA暗号化中にXML書式のファイルの誤りでエラーが発生するようだ
          ReturnCode = CRYPTOGRAPHIC_EXCEPTION;
          ErrorMessage = ex.Message;
          break;
        case IOException _:
          // I/Oエラーが発生したときにスローされる例外。現在の例外を説明するメッセージを取得します。
          // The exception that is thrown when an I/O error occurs. Gets a message that describes the current exception.
          ReturnCode = IO_EXCEPTION;
          ErrorMessage = ex.Message;
          break;
        default:
          ReturnCode = ERROR_UNEXPECTED;
          ErrorMessage = ex.Message;
          break;
      }
    }

    /// <summary>
    /// 暗号化処理がキャンセルされるべきかどうかを判断する
    /// </summary>
    /// <param name="worker">バックグラウンドワーカーのインスタンス</param>
    /// <returns>キャンセルされるべき場合は <c>true</c>、それ以外の場合は <c>false</c></returns>
    private bool ShouldCancel(BackgroundWorker worker)
    {
      return _isCancelled || (worker?.CancellationPending ?? false);
    }

    private long _processedSize;
    /// <summary>
    /// 処理されたデータのサイズを更新し、進行状況を追跡する
    /// </summary>
    /// <param name="size">処理されたデータのサイズ</param>
    public void UpdateProgress(long size)
    {
      Interlocked.Add(ref _processedSize, size);
    }

    /// <summary>
    /// 指定されたディレクトリとそのサブディレクトリ内のすべてのファイルを取得します。
    /// </summary>
    /// <param name="parentPath">親ディレクトリのパス。</param>
    /// <param name="rootFolderPath">ルートフォルダのパス。</param>
    /// <param name="cancelCheck">キャンセルが必要かどうかを確認するためのデリゲート。</param>
    /// <param name="worker">バックグラウンドワーカーのインスタンス。</param>
    /// <returns>ファイルシステムエントリの列挙。</returns>
    /// <exception cref="OperationCanceledException">操作がキャンセルされた場合にスローされます。</exception>
    public IEnumerable<FileSystemEntry> GetFileList(string parentPath, string rootFolderPath, Func<bool> cancelCheck, BackgroundWorker worker)
    {
      resultsConcurrentBag = new ConcurrentBag<FileSystemEntry>();
      _processedFileCount = 0;
      swProgress.Restart();

      try
      {
        var pending = new Queue<string>();  // ConcurrentQueueではなく通常のQueueを使用
        pending.Enqueue(rootFolderPath);

        while (pending.Count > 0)
        {
          if (cancelCheck()) throw new OperationCanceledException();

          var currentPath = pending.Dequeue();

          // ディレクトリの情報を追加
          var dirInfo = GetDirectoryInfo(parentPath, currentPath);
          if (!string.IsNullOrEmpty(dirInfo.FullPath))
          {
            resultsConcurrentBag.Add(dirInfo);
            ReportProgress(currentPath, worker);
          }

          try
          {
            // サブディレクトリの処理
            foreach (var dir in Directory.GetDirectories(currentPath))
            {
              if (cancelCheck()) throw new OperationCanceledException();
              pending.Enqueue(dir);
            }

            // ファイルの処理
            foreach (var file in Directory.GetFiles(currentPath))
            {
              if (cancelCheck()) throw new OperationCanceledException();

              var fileInfo = GetFileInfo(parentPath, file, cancelCheck);
              if (!string.IsNullOrEmpty(fileInfo.FullPath))
              {
                resultsConcurrentBag.Add(fileInfo);
                ReportProgress(file, worker);
              }
            }
          }
          catch (UnauthorizedAccessException ex)
          {
            Debug.WriteLine($"Access denied to {currentPath}: {ex.Message}");
          }
          catch (DirectoryNotFoundException ex)
          {
            Debug.WriteLine($"Directory not found {currentPath}: {ex.Message}");
          }
        }

        // 結果を確実に返す
        var sortedResults = resultsConcurrentBag
            .Where(entry => !string.IsNullOrEmpty(entry.FullPath))
            .OrderBy(e => e.RelativePath)
            .ToList();  // 結果を確実にリストとして保持

        return sortedResults;
      }
      finally
      {
        swProgress.Restart();
      }
    }

    /// <summary>
    /// 現在の進行状況を報告する
    /// </summary>
    /// <param name="currentPath">現在処理中のパス</param>
    /// <param name="worker">バックグラウンドワーカーのインスタンス</param>
    private void ReportProgress(string currentPath, BackgroundWorker worker)
    {
      Interlocked.Increment(ref _processedFileCount);

      if (swProgress.ElapsedMilliseconds > 100)
      {
        worker?.ReportProgress(-1, new ArrayList
        {
          READY_FOR_ENCRYPT,
          $"{currentPath} ({_processedFileCount})"
        });
        swProgress.Restart();
      }
    }

    /*
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentPath"></param>
    /// <param name="rootFolderPath"></param>
    /// <param name="cancelCheck"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    public IEnumerable<FileSystemEntry> GetFileList(string parentPath, string rootFolderPath, Func<bool> cancelCheck)
    {
      if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(rootFolderPath))
      {
        throw new ArgumentException("Parent path and root folder path must not be null or empty");
      }

      // パスの正規化
      parentPath = Path.GetFullPath(parentPath);
      rootFolderPath = Path.GetFullPath(rootFolderPath);

      var pending = new ConcurrentQueue<string>();
      var results = new ConcurrentBag<FileSystemEntry>();
      pending.Enqueue(rootFolderPath);

      while (!pending.IsEmpty)
      {
        if (cancelCheck()) throw new OperationCanceledException();

        if (pending.TryDequeue(out var currentPath))
        {
          try
          {
            var dirInfo = new DirectoryInfo(currentPath);
            if (!dirInfo.Exists)
            {
              continue;
            }

            var dirEntry = GetDirectoryInfo(parentPath, currentPath);
            results.Add(dirEntry);

            foreach (var dir in dirInfo.GetDirectories())
            {
              pending.Enqueue(dir.FullName);
            }

            foreach (var file in dirInfo.GetFiles())
            {
              if (cancelCheck()) throw new OperationCanceledException();

              var fileEntry = GetFileInfo(parentPath, file.FullName, cancelCheck);
              results.Add(fileEntry);
            }
          }
          catch (Exception ex)
          {
            continue;
          }
        }
      }

      var resultList = results.Where(entry => !string.IsNullOrEmpty(entry.FullPath)).OrderBy(e => e.RelativePath).ToList();
      return resultList;
    }
    */

    /// <summary>
    /// 暗号化ファイルに格納する各ファイル情報をMemoryStreamに書き込む
    /// </summary>
    /// <param name="ms">参照するMemoryStreamインスタンス</param>
    /// <param name="entry">各ファイル情報</param>
    /// <param name="newArchiveName">親ディレクトリがあればその名前</param>
    /// <returns></returns>
    private static long FileInfoStreamWriter(ref MemoryStream ms, FileSystemEntry entry, string newArchiveName)
    {
      if (ms == null)
      {
        throw new ArgumentNullException(nameof(ms));
      }

      if (string.IsNullOrEmpty(entry.RelativePath))
      {
        Debug.WriteLine($"Warning: Entry has no relative path. FullPath: {entry.FullPath}");
        return 0;
      }

      newArchiveName ??= string.Empty;  // newArchiveNameがnullの場合は空文字を使用

      Debug.WriteLine($"Writing entry: RelativePath={entry.RelativePath}, Size={entry.Size}, IsDirectory={entry.IsDirectory}");

      var fileLen = Encoding.UTF8.GetByteCount(newArchiveName + entry.RelativePath);
      var byteArray = BitConverter.GetBytes((short)fileLen);
      ms.Write(byteArray, 0, 2);

      // Directory or file name
      byteArray = Encoding.UTF8.GetBytes(newArchiveName + entry.RelativePath);
      ms.Write(byteArray, 0, fileLen);

      // Size
      byteArray = BitConverter.GetBytes(entry.Size);
      ms.Write(byteArray, 0, 8);

      // File attribute
      byteArray = BitConverter.GetBytes((int)entry.Attributes);
      ms.Write(byteArray, 0, 4);

      // Last write date (UTC)
      byteArray = BitConverter.GetBytes(entry.LastWriteDate);
      ms.Write(byteArray, 0, 4);

      // Last write time (UTC)
      byteArray = BitConverter.GetBytes(entry.LastWriteTime);
      ms.Write(byteArray, 0, 4);

      // Creation date (UTC)
      byteArray = BitConverter.GetBytes(entry.CreationDate);
      ms.Write(byteArray, 0, 4);

      // Creation time (UTC)
      byteArray = BitConverter.GetBytes(entry.CreationTime);
      ms.Write(byteArray, 0, 4);

      // MD5ハッシュ（ディレクトリでない場合のみ）
      if (entry is { IsDirectory: false, Size: > 0 })
      {
        ms.Write(entry.Md5Hash, 0, 16);
      }

      return entry.Size;
    }

    public class FileSystemEntry  // structからclassに変更
    {
      public bool IsDirectory { get; set; }
      public string FullPath { get; set; }
      public string RelativePath { get; set; }
      public long Size { get; set; }
      public FileAttributes Attributes { get; set; }
      public int LastWriteDate { get; set; }
      public int LastWriteTime { get; set; }
      public int CreationDate { get; set; }
      public int CreationTime { get; set; }
      public byte[] Md5Hash { get; set; }
    }

    private static FileSystemEntry GetDirectoryInfo(string parentPath, string dirPath)
    {
      try
      {
#if __MACOS__
      if (ParentPath.EndsWith('/') == false)
      {
        ParentPath = ParentPath + "/";
      }
#endif
        var di = new DirectoryInfo(dirPath);
        var relativePath = CreateRelativePath(parentPath, dirPath);

        // ディレクトリの場合のみ末尾にデリミタを追加
        if (relativePath.EndsWith("\\") == false)
        {
          relativePath += "\\";
        }

        return new FileSystemEntry
        {
          IsDirectory = true, // File flag
          FullPath = dirPath, // Absolute file path
          RelativePath = relativePath, // (string)Remove parent directory path.
          Size = 0, // (Int64)File size
          Attributes = di.Attributes, // (int)File attribute
          LastWriteDate = GetDateInt(di.LastWriteTimeUtc), // Last write Date (UTC)
          LastWriteTime = GetTimeInt(di.LastWriteTimeUtc), // Last write Time (UTC)
          CreationDate = GetDateInt(di.CreationTimeUtc), // Creation Date (UTC)
          CreationTime = GetTimeInt(di.CreationTimeUtc), // Creation Time (UTC)
          Md5Hash = [] // Check Sum (MD5)
        };

      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        throw new FileProcessingException($"Error processing file: {dirPath}", dirPath, ex);
      }

    }

    private static FileSystemEntry GetFileInfo(string parentPath, string filePath, Func<bool> cancelCheck)
    {
      try
      {
#if __MACOS__
      if (ParentPath.EndsWith('/') == false)
      {
        ParentPath = ParentPath + "/";
      }
#endif
        var fi = new FileInfo(filePath);
        return new FileSystemEntry
        {
          IsDirectory = false, // Directory flag
          FullPath = filePath, // Absolute file path
          RelativePath = CreateRelativePath(parentPath, filePath), // (string)Remove parent directory path.
          Size = fi.Length, // File size = 0
          Attributes = fi.Attributes, // (int)File attribute
          LastWriteDate = GetDateInt(fi.LastWriteTimeUtc), // Last write Date (UTC)
          LastWriteTime = GetTimeInt(fi.LastWriteTimeUtc), // Last write Time (UTC)
          CreationDate = GetDateInt(fi.CreationTimeUtc), // Creation Date (UTC)
          CreationTime = GetTimeInt(fi.CreationTimeUtc), // Creation Time (UTC)
          Md5Hash = fi.Length == 0 ? null : GetMd5Hash(filePath, cancelCheck) // Check Sum (MD5)
        };
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        throw new FileProcessingException($"Error processing file: {filePath}", filePath, ex);
      }
    }

    public class FileProcessingException(string message, string filePath, Exception inner) : Exception(message, inner)
    {
      public string FilePath { get; } = filePath;
    }

    private static string CreateRelativePath(string parentPath, string fullPath)
    {
      if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(fullPath))
      {
        return string.Empty;
      }

      // パスの正規化
      parentPath = Path.GetFullPath(parentPath);
      fullPath = Path.GetFullPath(fullPath);

      if (!fullPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase))
      {
        return string.Empty;
      }

      var relativeLength = fullPath.Length - parentPath.Length;
      if (relativeLength <= 0)
      {
        return string.Empty;
      }

      var relativePath = fullPath.Substring(parentPath.Length, relativeLength)
        .TrimStart(Path.DirectorySeparatorChar);

      // ディレクトリの場合のみ末尾にデリミタを追加
      if (Directory.Exists(fullPath) && !relativePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
      {
        relativePath += Path.DirectorySeparatorChar;
      }

      return relativePath;
    }

    /// <summary>
    /// DateTime型の日付をint形式にして返す（たとえば、2025/01/06 → 20250106）
    /// </summary>
    /// <param name="dt">DateTime型の日付</param>
    /// <returns></returns>
    private static int GetDateInt(DateTime dt)
    {
      return (dt.Year * 10000) + (dt.Month * 100) + dt.Day;
    }

    /// <summary>
    /// DateTime型の時間をint形式にして返す（たとえば、12:38:12 → 123812）
    /// </summary>
    /// <param name="dt">DateTime型の時間</param>
    /// <returns></returns>
    private static int GetTimeInt(DateTime dt)
    {
      return (dt.Hour * 10000) + (dt.Minute * 100) + dt.Second;
    }

    /// <summary>
    /// 指定されたファイルのMD5ハッシュ値を計算します。
    /// </summary>
    /// <param name="filePath">ハッシュ値を計算する対象のファイルのパス。</param>
    /// <param name="cancelCheck">
    /// 計算処理中に定期的に呼び出されるデリゲート関数。
    /// この関数が<c>true</c>を返した場合、処理がキャンセルされ、<c>null</c>が返されます。
    /// </param>
    /// <returns>
    /// ファイルのMD5ハッシュ値を格納したバイト配列。処理がキャンセルされた場合は<c>null</c>を返します。
    /// </returns>
    private static byte[] GetMd5Hash(string filePath, Func<bool> cancelCheck)
    {
      using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      using var md5 = new MD5CryptoServiceProvider();
      var buffer = new byte[8192];
      int bytesRead;
      while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
      {
        if (cancelCheck())
        {
          return null;
        }
        md5.TransformBlock(buffer, 0, bytesRead, null, 0);
      }
      md5.TransformFinalBlock(buffer, 0, 0);
      return md5.Hash;
    }

    /// <summary>
    /// アセンブリ情報を取得するクラス
    /// Get assembly information
    /// </summary>
    private static class AppInfo
    {
      private static readonly Assembly _assembly = Assembly.GetCallingAssembly();

      // アセンブリ情報からはバージョン番号だけ取得
      public static Version Version => _assembly.GetName().Version;

      /*
      public static string Title
      {
        get
        {
          var title = GetAttribute<AssemblyTitleAttribute>()?.Title;
          return !string.IsNullOrEmpty(title)
            ? title
            : Path.GetFileNameWithoutExtension(_assembly.CodeBase);
        }
      }
      public static string ProductName => GetAttribute<AssemblyProductAttribute>()?.Product ?? "";
      public static string Description => GetAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";
      public static string CopyrightHolder => GetAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
      public static string CompanyName => GetAttribute<AssemblyCompanyAttribute>()?.Company ?? "";

      private static T GetAttribute<T>() where T : Attribute
      {
        return _assembly.GetCustomAttributes(typeof(T), false).FirstOrDefault() as T;
      }
      */
    }

  }// end class FileEncrypt()

}// end namespace

