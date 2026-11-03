using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RulershipPin;
internal static class ProfileDiagnostics
{
 internal static void Run(string output) {
  Directory.CreateDirectory(output);
  if(!UserProfile.TryNormalizeName("  Sky  ",out var name) || name!="Sky" || UserProfile.TryNormalizeName("\n",out _) || UserProfile.TryNormalizeName(new string('x',25),out _) || UserProfile.TryNormalizeName("A\nB",out _) || UserProfile.NameFromGreeting("Hello there <3")!="" || UserProfile.NameFromGreeting("Hello Sky <3")!="Sky") throw new InvalidDataException("Name validation failed");
  string settingsPath=Path.Combine(AppContext.BaseDirectory,"companion-settings.json");byte[]? original=File.Exists(settingsPath)?File.ReadAllBytes(settingsPath):null;
  try {
   if(CompanionSettings.TrySaveUserName("",out _)) throw new InvalidDataException("An empty name was saved");
   if(!CompanionSettings.TrySaveUserName("Sky",out _) || CompanionSettings.LoadUserName()!="Sky" || CompanionSettings.LoadGreeting()!="Hello Sky <3") throw new InvalidDataException("Name was not saved/reloaded");
   if(!CompanionSettings.TrySaveScale(.8,out _) || !CompanionSettings.TrySaveModel("Chibi",out _) || !CompanionSettings.TrySaveAutomaticAdventures(false,out _) || CompanionSettings.LoadUserName()!="Sky") throw new InvalidDataException("Other preferences lost the name");
   var pet=new PetWindow(true);new WindowInteropHelper(pet).EnsureHandle();
   try {
    var prompt=new NamePrompt(pet,true,"Sky",new Point(400,400));
    var bubble=(FrameworkElement)prompt.Content;bubble.Measure(new Size(304,double.PositiveInfinity));bubble.Arrange(new Rect(0,0,304,bubble.DesiredSize.Height));bubble.UpdateLayout();
    Save(bubble,304,(int)Math.Ceiling(bubble.ActualHeight),Path.Combine(output,"name-bubble.png"));prompt.Close();
    var chibi=(ChibiCompanion)pet.CharacterStage.Children[0];chibi.StartReaction(ChibiCompanion.Emotion.Wave,0);chibi.SetTime(1.6);chibi.Measure(new Size(240,360));chibi.Arrange(new Rect(0,0,240,360));chibi.UpdateLayout();Save(chibi,240,360,Path.Combine(output,"personalized-sign.png"));
   } finally {pet.Close();}
   File.WriteAllText(Path.Combine(output,"profile-checks.txt"),"PASS: name trimming/length/control validation; neutral greeting fallback; persistent name and greeting; size/model/adventure saves preserve the name; bubble and personalized sign rendered. Original settings restored after checks. No visible app was launched.\n");
  } finally {if(original is null) File.Delete(settingsPath);else File.WriteAllBytes(settingsPath,original);}
 }
 static void Save(Visual visual,int width,int height,string path) {var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);}
}
