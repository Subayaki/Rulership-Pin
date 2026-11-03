using System.Windows;
namespace RulershipPin;
public partial class App : Application {
 private System.Threading.Mutex? instanceMutex;
 protected override void OnStartup(StartupEventArgs e) {
  if(e.Args is ["--apply-update",var job]) {ShutdownMode=ShutdownMode.OnExplicitShutdown;Shutdown(AppUpdater.Apply(job));return;}
  if(e.Args is ["--test-profile",var profileOutput]) {
   ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {ProfileDiagnostics.Run(profileOutput);Shutdown(0);}catch(Exception error) {System.IO.Directory.CreateDirectory(profileOutput);System.IO.File.WriteAllText(System.IO.Path.Combine(profileOutput,"profile-error.txt"),error.ToString());Shutdown(1);}return;
  }
  if(e.Args is ["--test-updater",var updateOutput]) {
   ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {UpdateDiagnostics.Run(updateOutput);Shutdown(0);}catch(Exception error) {System.IO.Directory.CreateDirectory(updateOutput);System.IO.File.WriteAllText(System.IO.Path.Combine(updateOutput,"updater-error.txt"),error.ToString());Shutdown(1);}return;
  }
  if(e.Args is ["--test-legacy-greeting",var legacyAssembly,var legacyOutput]) {
   ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {UpdateDiagnostics.RunLegacy(legacyAssembly,legacyOutput);Shutdown(0);}catch(Exception error) {System.IO.Directory.CreateDirectory(legacyOutput);System.IO.File.WriteAllText(System.IO.Path.Combine(legacyOutput,"legacy-error.txt"),error.ToString());Shutdown(1);}return;
  }
  if(e.Args is ["--test-adventures",var adventureOutput]) {
   ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {AdventureDiagnostics.Run(adventureOutput);Shutdown(0);}catch(Exception error) {System.IO.Directory.CreateDirectory(adventureOutput);System.IO.File.WriteAllText(System.IO.Path.Combine(adventureOutput,"error.txt"),error.ToString());Shutdown(1);}return;
  }
  if(e.Args is ["--test-low-cortisol",var lowOutput]) {
   ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {LowCortisolDiagnostics.Run(lowOutput);Shutdown(0);}catch(Exception error) {System.IO.Directory.CreateDirectory(lowOutput);System.IO.File.WriteAllText(System.IO.Path.Combine(lowOutput,"error.txt"),error.ToString());Shutdown(1);}return;
  }
  if(e.Args is ["--test-orb",var output]) {
   ShutdownMode=ShutdownMode.OnExplicitShutdown;
   try {OrbDiagnostics.Run(output);Shutdown(0);}catch(Exception error) {System.IO.Directory.CreateDirectory(output);System.IO.File.WriteAllText(System.IO.Path.Combine(output,"orb-error.txt"),error.ToString());Shutdown(1);}return;
  }
  if(e.Args.Length==2 && e.Args[0]=="--render-chibi-reactions") {
   try {ChibiReactionDiagnostics.Render(e.Args[1]);Shutdown(0);}
   catch(Exception error) {System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"chibi-diagnostic-error.txt"),error.ToString());Shutdown(1);}
   return;
  }
  if(e.Args.Length==2 && e.Args[0]=="--render-chibi") {
   try {
    System.IO.Directory.CreateDirectory(e.Args[1]);
    foreach(double time in new[]{0.0,2.0,24.0,28.0}) {
     var chibi=new ChibiCompanion();chibi.SetTime(time);
     chibi.Measure(new Size(240,360));chibi.Arrange(new Rect(0,0,240,360));chibi.UpdateLayout();
     var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(240,360,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(chibi);
     var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
     using var stream=System.IO.File.Create(System.IO.Path.Combine(e.Args[1],$"chibi-{time:00}.png"));encoder.Save(stream);
    }
    Shutdown(0);
   } catch(Exception error) {System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"chibi-diagnostic-error.txt"),error.ToString());Shutdown(1);}
   return;
  }
  if(e.Args.Length==2 && e.Args[0] is "--render-rig" or "--render-rig-stills") {
   try { RigDiagnostics.Render(e.Args[1],e.Args[0]=="--render-rig"); Shutdown(0); }
   catch(Exception error) { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"rig-diagnostic-error.txt"),error.ToString()); Shutdown(1); }
   return;
  }
  instanceMutex=new System.Threading.Mutex(true,"Local\\RulershipPin.DesktopPet",out bool first);
  if(!first) { Shutdown(); return; }
  base.OnStartup(e);
  MainWindow=new PetWindow();MainWindow.Show();
 }
 protected override void OnExit(ExitEventArgs e) { instanceMutex?.Dispose(); base.OnExit(e); }
 public App() {
  DispatcherUnhandledException+=(_,e)=>{
   System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"rulership-error.txt"),e.Exception.ToString());
   MessageBox.Show(e.Exception.Message,"Rulership-Pin – Startup error"); e.Handled=true; Shutdown(1);
  };
 }
}
