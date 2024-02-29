using System;
using System.Runtime.InteropServices;

namespace AttacheCase
{
  // アンマネージドDLLを動的にロードする
  // Dynamically load unmanaged DLLs
  // ref. https://stackoverflow.com/questions/8836093/how-can-i-specify-a-dllimport-path-at-runtime
  // ref. https://anis774.net/codevault/loadlibrary.html
  //
  // 2024/01/31
  // ChatGPT により、さらに安全策を追加した
  //   
  public class UnManagedDll : IDisposable
  {
    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadLibrary(string lpFileName);
    [DllImport("kernel32", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    [DllImport("kernel32")]
    static extern bool FreeLibrary(IntPtr hModule);

    private IntPtr _moduleHandle;

    public UnManagedDll(string lpFileName)
    {
      _moduleHandle = LoadLibrary(lpFileName);
      if (_moduleHandle == IntPtr.Zero)
      {
        throw new Exception("Failed to load unmanaged DLL.");
      }
    }

    public T GetProcDelegate<T>(string method) where T : class
    {
      IntPtr methodHandle = GetProcAddress(_moduleHandle, method);
      if (methodHandle == IntPtr.Zero)
      {
        throw new Exception($"Failed to get the address of the method '{method}'.");
      }

      T r = Marshal.GetDelegateForFunctionPointer(methodHandle, typeof(T)) as T;
      return r ?? throw new Exception($"Could not create delegate for method '{method}'. ");
    }

    public void Dispose()
    {
      FreeLibrary(_moduleHandle);
      _moduleHandle = IntPtr.Zero;
    }
  }
  
}