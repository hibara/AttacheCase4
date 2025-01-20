using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AttacheCase
{
  internal class TaskbarProgress
  {
    [ComImport()]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
      [PreserveSig]
      void HrInit();
      [PreserveSig]
      void AddTab(IntPtr hwnd);
      [PreserveSig]
      void DeleteTab(IntPtr hwnd);
      [PreserveSig]
      void ActivateTab(IntPtr hwnd);
      [PreserveSig]
      void SetActiveAlt(IntPtr hwnd);
      [PreserveSig]
      void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
      [PreserveSig]
      void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
      [PreserveSig]
      void SetProgressState(IntPtr hwnd, TaskbarProgressBarStatus tbpFlags);
    }

    public enum TaskbarProgressBarStatus
    {
      NoProgress = 0,
      Indeterminate = 0x1,
      Normal = 0x2,
      Error = 0x4,
      Paused = 0x8
    }

    [ComImport()]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarInstance { }

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
      public uint cbSize;
      public IntPtr hwnd;
      public uint dwFlags;
      public uint uCount;
      public uint dwTimeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    private readonly ITaskbarList3 _taskbarList;
    private readonly IntPtr _windowHandle;
    private bool _disposed;

    public TaskbarProgress(IntPtr windowHandle)
    {
      _windowHandle = windowHandle;
      _taskbarList = (ITaskbarList3)new TaskbarInstance();
      _taskbarList.HrInit();
    }

    /// <summary>
    /// 進捗状態を更新
    /// </summary>
    /// <param name="value">0-100の進捗値</param>
    public void UpdateProgress(int value)
    {
      EnsureNotDisposed();
      value = Math.Max(0, Math.Min(100, value));

      try
      {
        _taskbarList.SetProgressValue(_windowHandle, (ulong)value, 100);
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException("プログレス更新に失敗しました", ex);
      }
    }

    /// <summary>
    /// プログレスバーの状態を設定
    /// </summary>
    public void SetState(TaskbarProgressBarStatus state)
    {
      EnsureNotDisposed();
      try
      {
        _taskbarList.SetProgressState(_windowHandle, state);
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException("状態の設定に失敗しました", ex);
      }
    }

    /// <summary>
    /// タスクバーアイコンを点滅
    /// </summary>
    /// <param name="count">点滅回数</param>
    public void Flash(uint count = 3)
    {
      EnsureNotDisposed();
      FLASHWINFO flash = new FLASHWINFO
      {
        cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(FLASHWINFO))),
        hwnd = _windowHandle,
        dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
        uCount = count,
        dwTimeout = 0
      };

      FlashWindowEx(ref flash);
    }

    /// <summary>
    /// プログレス表示をリセット
    /// </summary>
    public void Reset()
    {
      EnsureNotDisposed();
      try
      {
        _taskbarList.SetProgressValue(_windowHandle, 0, 100);
        _taskbarList.SetProgressState(_windowHandle, TaskbarProgressBarStatus.NoProgress);
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException("リセットに失敗しました", ex);
      }
    }

    private void EnsureNotDisposed()
    {
      if (_disposed)
      {
        throw new ObjectDisposedException(nameof(TaskbarProgress));
      }
    }

    // IDisposableの実装
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    // 派生クラスでオーバーライド可能な仮想Disposeメソッド
    protected virtual void Dispose(bool disposing)
    {
      if (!_disposed)
      {
        if (disposing)
        {
          if (_taskbarList != null)
          {
            Marshal.ReleaseComObject(_taskbarList);
          }
        }
        _disposed = true;
      }
    }

  }
}
