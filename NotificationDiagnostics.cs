using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RulershipPin;
internal static class NotificationDiagnostics
{
 internal static void Run(string output) {
  Directory.CreateDirectory(output);
  var release=new ReleaseInfo("v99.0.0",false,false,[new ReleaseAsset(AppUpdater.AssetName,$"https://github.com/{AppUpdater.Repository}/releases/download/v99.0.0/{AppUpdater.AssetName}","sha256:"+new string('0',64))]);
  if(!AppUpdater.IsNewRelease(release) || AppUpdater.IsNewRelease(release with {Draft=true}) || AppUpdater.IsNewRelease(release with {Prerelease=true}) || AppUpdater.IsNewRelease(release with {Tag="v"+AppUpdater.Current.ToString(3)}) || AppUpdater.IsNewRelease(release with {Assets=[]})) throw new InvalidDataException("Update notification selection failed");
  if(UpdateNotice.Message("Sky")!="Hey Sky, a new update is out!") throw new InvalidDataException("Personalized notification is incorrect");
  string command=WindowsStartup.Command(@"C:\Apps\Rulership_pin_custom");
  if(command!="\"C:\\Apps\\Rulership_pin_custom\\Rulership-Pin.exe\" --startup") throw new InvalidDataException("Startup command is not quoted correctly");
  var pet=new PetWindow(true);new WindowInteropHelper(pet).EnsureHandle();
  var content=(FrameworkElement)pet.Content;content.Measure(new Size(pet.Width,pet.Height));content.Arrange(new Rect(0,0,pet.Width,pet.Height));content.UpdateLayout();
  try {
   var notice=new UpdateNotice(pet,"Sky",()=>{},()=>{});new WindowInteropHelper(notice).EnsureHandle();
   long styles=Native.GetWindowLongPtr(new WindowInteropHelper(notice).Handle,-20).ToInt64();
   if((styles&0x08000000L)==0 || (styles&0x20L)!=0) throw new InvalidDataException("Update bubble must be non-activating and clickable");
   var body=(FrameworkElement)notice.Content;body.Measure(new Size(300,double.PositiveInfinity));body.Arrange(new Rect(0,0,300,body.DesiredSize.Height));body.UpdateLayout();
   var bitmap=new RenderTargetBitmap(300,(int)Math.Ceiling(body.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(body);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,"update-bubble.png"))) encoder.Save(file);notice.Close();
   pet.ReceiveUpdate(release);if(!pet.HasUpdateNotice) throw new InvalidDataException("New update did not create a notification");
   pet.SetCollapsed(true);if(pet.HasUpdateNotice) throw new InvalidDataException("Update bubble survived collapse");
   pet.SetCollapsed(false);if(!pet.HasUpdateNotice) throw new InvalidDataException("Pending notification did not return after expansion");
   pet.StartAdventure("lurk",false);if(pet.HasUpdateNotice) throw new InvalidDataException("Update bubble remained during an outing");
   pet.CancelAdventure();if(!pet.HasUpdateNotice) throw new InvalidDataException("Pending notification did not return after outing");
   pet.Close();if(pet.HasUpdateNotice) throw new InvalidDataException("Update bubble leaked on quit");
   File.WriteAllText(Path.Combine(output,"notification-checks.txt"),"PASS: newer stable releases only; personalized message; correctly quoted startup command; non-activating clickable bubble; pending notification hidden/restored around orb and outings; quit cleanup. No startup registry values were written and no visible notification was shown.\n");
  } finally {pet.Close();}
 }
}
