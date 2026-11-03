using System.IO;
using Microsoft.Win32;
namespace RulershipPin;
internal static class WindowsStartup
{
 const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run",ValueName="RulershipPin";
 internal static string Command(string directory)=>"\""+Path.Combine(Path.GetFullPath(directory),"Rulership-Pin.exe")+"\" --startup";
 internal static bool IsEnabled() {try {using var key=Registry.CurrentUser.OpenSubKey(KeyPath);return key?.GetValue(ValueName) is string;}catch(Exception error) when(error is System.Security.SecurityException or UnauthorizedAccessException or IOException) {return false;}}
 internal static bool TrySet(bool enabled,out string message) {
  try {
   string executable=Path.Combine(AppContext.BaseDirectory,"Rulership-Pin.exe");if(enabled && !File.Exists(executable)) throw new IOException("The app executable could not be found.");
   using var key=Registry.CurrentUser.CreateSubKey(KeyPath,true);
   if(enabled) key.SetValue(ValueName,Command(AppContext.BaseDirectory),RegistryValueKind.String);else key.DeleteValue(ValueName,false);
   message="";return true;
  } catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException) {message="Could not change Windows startup.\n\n"+error.Message;return false;}
 }
}
