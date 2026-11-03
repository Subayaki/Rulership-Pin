using System.Runtime.InteropServices;
using System.Text;
namespace RulershipPin;
internal static class Native
{
 internal delegate bool MonitorProc(nint monitor,nint dc,ref Rect rect,nint data);
 [DllImport("user32.dll")] internal static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorProc callback,nint data);
 [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor,int type,out uint x,out uint y);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint window,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] internal static extern nint SetWindowLongPtr(nint window,int index,nint value);
 [DllImport("gdi32.dll")] internal static extern nint CreateRectRgn(int left,int top,int right,int bottom);
 [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint region);
 [DllImport("gdi32.dll")] internal static extern int CombineRgn(nint destination,nint first,nint second,int mode);
 [DllImport("user32.dll")] internal static extern int SetWindowRgn(nint window,nint region,bool redraw);
 internal delegate bool EnumProc(nint hwnd, nint data);
 [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
 [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint window,uint flags);
 [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
 internal delegate void WinEventProc(nint hook, uint evt, nint hwnd, int obj, int child, uint thread, uint time);
 [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
 [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width,Height; }
 [StructLayout(LayoutKind.Sequential)] internal struct ThumbnailProperties {
  public uint Flags; public Rect Destination, Source; public byte Opacity;
  [MarshalAs(UnmanagedType.Bool)] public bool Visible;
  [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
 }
 [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback,nint data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetWindowText(nint hwnd,StringBuilder text,int length);
 [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
 [DllImport("user32.dll")] internal static extern bool IsWindow(nint hwnd);
 [DllImport("user32.dll")] internal static extern bool IsIconic(nint hwnd);
 [DllImport("user32.dll")] internal static extern nint GetWindow(nint hwnd,uint command);
 [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
 [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
 [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd,out Rect rect);
 [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint hwnd);
 [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd,int command);
 [DllImport("user32.dll")] internal static extern nint SetWinEventHook(uint min,uint max,nint module,WinEventProc callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(nint hook);
 [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(nint dest,nint source,out nint thumbnail);
 [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(nint thumbnail);
 [DllImport("dwmapi.dll")] internal static extern int DwmQueryThumbnailSourceSize(nint thumbnail,out Size size);
 [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(nint thumbnail,ref ThumbnailProperties properties);
 [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(nint hwnd,int attribute,out int value,int size);
 [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
 internal static string Title(nint hwnd) { var text=new StringBuilder(1024); GetWindowText(hwnd,text,text.Capacity); return text.ToString(); }
 internal static bool Eligible(nint hwnd) {
  GetWindowThreadProcessId(hwnd,out var pid);
  if(pid==Environment.ProcessId || !IsWindowVisible(hwnd) || GetWindow(hwnd,4)!=0 || string.IsNullOrWhiteSpace(Title(hwnd))) return false;
  return DwmGetWindowAttribute(hwnd,14,out var cloaked,4)!=0 || cloaked==0;
 }
}
