using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;

namespace RulershipPin;

// Use the Windows shell's light/dark preference; let DWM retain native
// caption buttons, accent colors and accessibility behavior.
internal static class WindowTheme
{
 internal static void FollowSystem(Window window)
 {
  HwndSource? source=null;
  void Apply()
  {
   if(source is null) return;
   int dark=0;
   if(!SystemParameters.HighContrast)
   {
    try
    {
     using var settings=Registry.CurrentUser.OpenSubKey(
      @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
     dark=settings?.GetValue("SystemUsesLightTheme") is int light && light==0 ? 1 : 0;
    }
    catch(System.Security.SecurityException) { }
    catch(UnauthorizedAccessException) { }
    catch(System.IO.IOException) { }
   }
   Native.DwmSetWindowAttribute(source.Handle,20,ref dark,sizeof(int));
  }
  nint OnMessage(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
  {
   // Theme, settings and DWM colorization changes are broadcast by Windows.
   if(message is 0x001A or 0x031A or 0x0320)
    window.Dispatcher.BeginInvoke(Apply);
   return 0;
  }
  window.SourceInitialized+=(_,_)=>
  {
   source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
   source?.AddHook(OnMessage);
   Apply();
  };
  window.Closed+=(_,_)=>{source?.RemoveHook(OnMessage);source=null;};
 }
}
