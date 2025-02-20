using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AttacheCase
{
  internal static class Program
  {
    // File Type
    private const int FILE_TYPE_ERROR = -1;
    private const int FILE_TYPE_NONE = 0;
    private const int FILE_TYPE_ATC = 1;
    private const int FILE_TYPE_ATC_EXE = 2;
    private const int FILE_TYPE_PASSWORD_ZIP = 3;
    private const int FILE_TYPE_RSA_DATA = 4;
    private const int FILE_TYPE_RSA_PRIVATE_KEY = 5;
    private const int FILE_TYPE_RSA_PUBLIC_KEY = 6;

    [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true, EntryPoint = "SetDllDirectory")]
    private static extern bool SetDllDirectoryEntryPoint(string lpPathName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);
    // Delegate for SendMessage function
    private delegate IntPtr SendMessageDelegate(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

    public const uint WM_COPYDATA = 0x004A;

    [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true, EntryPoint = "SetDefaultDllDirectories")]
    private static extern bool SetDefaultDllDirectoriesEntryPoint(uint directoryFlags);

    // LoadLibrary
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr hModule);
    // GetProcAddress
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    // LOAD_LIBRARY_SEARCH_APPLICATION_DIR : 0x00000200
    // LOAD_LIBRARY_SEARCH_DEFAULT_DIRS    : 0x00001000
    // LOAD_LIBRARY_SEARCH_SYSTEM32        : 0x00000800
    // LOAD_LIBRARY_SEARCH_USER_DIRS       : 0x00000400
    private const uint DllSearchFlags = 0x00000800;

    [StructLayout(LayoutKind.Sequential)]
    public struct COPYDATASTRUCT
    {
      public IntPtr dwData;
      public int cbData;
      public IntPtr lpData;
    }

#if TEST
    private const string MutexName = "p4Cryptan-Test";
#else
    private const string MutexName = "p4Cryptan";
#endif

    [STAThread]
    static void Main(string[] args)
    {
      // DLLプリロード攻撃対策
      SetDllDirectoryEntryPoint(Path.GetDirectoryName(Application.ExecutablePath));
      SetDefaultDllDirectoriesEntryPoint(DllSearchFlags);

#if DEBUG
      //-----------------------------------
      // 1) 実行ファイル直下にログを書いてみる
      var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
      // ログファイルのパス
      const string logFileName = "debug_console_log.txt";
      var logFilePath = Path.Combine(exeDir, logFileName);

      var logText = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss: ") + " クリプタン起動\n";

      try
      {
        // まずは実行ファイル直下で書き込みを試す
        File.AppendAllText(logFilePath, logText);
      }
      catch (Exception ex)
      {
        // 2) 失敗したらデスクトップへ書き込む
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var logPathDesktop = Path.Combine(desktopPath, logFileName);

        try
        {
          File.AppendAllText(logPathDesktop, logText);
        }
        catch (Exception)
        {
          // ignored
        }
      }

      //// ログファイルを追記モードで開くリスナーを作成
      //var textListener = new TextWriterTraceListener(new StreamWriter(logFilePath, true));
      //// 既存のリスナーをクリア（必要なら）
      //Debug.Listeners.Clear();
      //// カスタムリスナーを追加
      //Debug.Listeners.Add(textListener);

#endif

      //-----------------------------------
      // Load Options
      AppSettings.Instance.ReadOptions();

      //-----------------------------------
      // アプリケーションのバージョン文字列を取得する
      AppSettings.Instance.ApplicationPath = Application.ExecutablePath;

      var asm = System.Reflection.Assembly.GetExecutingAssembly();
      var ver = asm.GetName().Version;
      AppSettings.Instance.AppVersion = int.Parse(ver.ToString().Replace(".", ""));

      //-----------------------------------
      // 言語切り替え
      switch (AppSettings.Instance.Language)
      {
        case "ja":
          Thread.CurrentThread.CurrentCulture = new CultureInfo("ja-JP");
          Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
          break;
        case "en":
          Thread.CurrentThread.CurrentCulture = new CultureInfo("", true);
          Thread.CurrentThread.CurrentUICulture = new CultureInfo("", true);
          break;
        default:
          if (CultureInfo.CurrentCulture.Name == "ja-JP")
          {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("ja-JP");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
          }
          else
          {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("", true);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("", true);
          }

          break;
      }

#if DEBUG
      // ja
      //Thread.CurrentThread.CurrentCulture = new CultureInfo("ja-JP");
      //Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");

      // en
      //Thread.CurrentThread.CurrentCulture = new CultureInfo("", true);
      //Thread.CurrentThread.CurrentUICulture = new CultureInfo("", true);

#endif
      //----------------------------------------------------------------------
      // Mutex
      //----------------------------------------------------------------------
      using var mutex = new Mutex(true, MutexName, out var isNew);

      if (isNew)
      {
        // 初回起動時
        foreach (var t in args)
        {
          if (File.Exists(t))
          {
            AppSettings.Instance.FileList.Add(t);
          }
        }

#if DEBUG
        //AppSettings.Instance.FileList.Add("C:\\Users\\hibara\\Desktop\\クマさん.atc");
#endif

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Form1());
      }
      else
      {
        // 既に実行中のインスタンスが存在するかを確認します
        var current = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(current.ProcessName);
        foreach (var process in processes)
        {
          // 現在のプロセスと同じプロセスIDを持つ場合や、メインモジュールのファイル名が異なる場合はスキップします
          // 既に別のインスタンスが実行中の場合、ファイルパスをそのインスタンスに送信します
          if (current.MainModule != null && process.MainModule != null && (process.Id == current.Id ||
                                                                           process.MainModule.FileName !=
                                                                           current.MainModule.FileName)) continue;

          var systemPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
          var dllPath = Path.Combine(systemPath, "user32.dll");

          var pDll = LoadLibrary(dllPath);
          var pFunc = GetDelegateForFunctionAddress<SendMessageDelegate>(pDll, "SendMessageW");

          // コマンドライン引数を使用してファイルパスを取得します
          var arguments = Environment.GetCommandLineArgs();
          if (arguments.Length > 1) // 最初の引数は実行ファイルのパスです
          {
            var filePath = string.Join("\t", arguments.Skip(1)); // 最初の引数をスキップしてファイルパスを結合します
            COPYDATASTRUCT cds;
            cds.dwData = IntPtr.Zero;
            cds.cbData = (filePath.Length + 1) * 2; // Unicodeのために2倍にし、Null終端文字も含めます
            cds.lpData = Marshal.StringToHGlobalUni(filePath);

            // SendMessage関数の定義
            const uint WM_COPYDATA = 0x004A;

            pFunc(process.MainWindowHandle, WM_COPYDATA, current.MainWindowHandle, ref cds);

            Marshal.FreeHGlobal(cds.lpData);
          }

          FreeLibrary(pDll);

          // 現在のインスタンスを終了します
          Environment.Exit(0);

        }
      }

    }

    private static TDelegate GetDelegateForFunctionAddress<TDelegate>(IntPtr dllHandle, string functionName)
    {
      var procAddress = GetProcAddress(dllHandle, functionName);
      return procAddress == IntPtr.Zero ? default : Marshal.GetDelegateForFunctionPointer<TDelegate>(procAddress);
    }



  }
}
