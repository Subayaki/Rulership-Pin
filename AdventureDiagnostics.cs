using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RulershipPin;
internal static class AdventureDiagnostics
{
 internal static void Run(string output) {
  Directory.CreateDirectory(output);
  DesktopMonitor Monitor(int l,int t,int r,int b)=>new(0,new Native.Rect {Left=l,Top=t,Right=r,Bottom=b},new Native.Rect {Left=l,Top=t,Right=r,Bottom=b-40},1);
  foreach(var layout in new[]{new[]{Monitor(-1920,0,0,1080),Monitor(0,0,2560,1440)},new[]{Monitor(0,-1080,1920,0),Monitor(0,0,1920,1080)},new[]{Monitor(-1600,-200,0,700),Monitor(200,0,2120,1080),Monitor(2120,-400,4040,680)}}) {
   foreach(bool right in new[]{true,false}) {
    var path=new EscapeRoute(layout,156,156,right);
    if(path.Length<=0 || (path.At(0)-path.Points[0]).Length>.001 || (path.At(1)-path.Points[^1]).Length>.001) throw new InvalidDataException("Incomplete escape route");
    foreach(var monitor in layout) if(!path.Points.Skip(1).SkipLast(1).Any(p=>p.X+78>=monitor.Work.Left && p.X+78<=monitor.Work.Right && p.Y+78>=monitor.Work.Top && p.Y+78<=monitor.Work.Bottom)) throw new InvalidDataException("Escape skipped a display");
    Point previous=path.At(0);for(int i=1;i<=1000;i++) {var next=path.At(i/1000.0);if((next-previous).Length>path.Length/1000+.01) throw new InvalidDataException("Escape discontinuity");previous=next;}
   }
   foreach(var monitor in layout) foreach(int side in new[]{78,156,273}) foreach(var edge in Enum.GetValues<PeekEdge>()) {
    var p=AdventureGeometry.Peek(monitor.Bounds,edge,side,.42);int grip=(int)Math.Round(side*400.0/512);
    bool anchored=edge switch {PeekEdge.Bottom=>p.y+grip==monitor.Bounds.Bottom,PeekEdge.Top=>p.y+side-grip==monitor.Bounds.Top,PeekEdge.Left=>p.x+side-grip==monitor.Bounds.Left,_=>p.x+grip==monitor.Bounds.Right};
    if(!anchored) throw new InvalidDataException("Peek does not meet monitor edge");
   }
  }
  var pet=new PetWindow(true);new WindowInteropHelper(pet).EnsureHandle();
  var content=(FrameworkElement)pet.Content;content.Measure(new Size(pet.Width,pet.Height));content.Arrange(new Rect(0,0,pet.Width,pet.Height));content.UpdateLayout();
  var source=new Window {Title="Hidden adventure source",Width=640,Height=360};var sourceHandle=new WindowInteropHelper(source).EnsureHandle();
  var pin=new PreviewWindow(sourceHandle) {Owner=pet,Left=100,Top=100};pet.RegisterPreview(pin);new WindowInteropHelper(pin).EnsureHandle();
  try {
   var homeBounds=new Rect(pet.Left,pet.Top,pet.Width,pet.Height);var pinPosition=new Point(pin.Left,pin.Top);
   foreach(string id in new[]{"lurk","mini-escape"}) {
    pet.StartAdventure(id,false);var adventure=pet.Adventure!;
    if(!adventure.Active || pet.CharacterStage.Visibility!=Visibility.Hidden) throw new InvalidDataException("Adventure did not replace home character");
    long styles=Native.GetWindowLongPtr(adventure.OverlayHandle,-20).ToInt64();
    if((styles&0x08000020L)!=0x08000020L) throw new InvalidDataException("Overlay is not click-through/non-activating");
    foreach(double seconds in new[]{.5,2.1,3.2,5.5,8.1}) adventure.Evaluate(seconds);
    if(new Rect(pet.Left,pet.Top,pet.Width,pet.Height)!=homeBounds || new Point(pin.Left,pin.Top)!=pinPosition || !pin.HasThumbnail) throw new InvalidDataException("Adventure moved home or pin");
    if(id=="lurk") RenderPoses(adventure.PoseFrames,output);
    adventure.Evaluate(1000);
    if(adventure.Active || pet.CharacterStage.Visibility!=Visibility.Visible) throw new InvalidDataException("Natural return did not restore home");
   }
   pet.StartAdventure("lurk",false);pet.CancelAdventure();if(pet.AdventureActive) throw new InvalidDataException("Cancel did not close overlay");
   pet.StartAdventure("mini-escape",false);pet.SetCollapsed(true);
   if(pet.AdventureActive || !pin.HasThumbnail || new Point(pin.Left,pin.Top)!=pinPosition) throw new InvalidDataException("Orb did not cancel outing and preserve pin");
   pet.SetCollapsed(false);pet.StartAdventure("lurk",false);pet.Close();
   if(pet.AdventureActive || pin.HasThumbnail) throw new InvalidDataException("Quit leaked overlay or pin");
   File.WriteAllText(Path.Combine(output,"checks.txt"),"PASS: continuous all-monitor routes in both directions for aligned/stacked/offset/separated screens; all four peek edges at three sizes; hidden native overlay non-activation/click-through flags; stationary home/pin; natural return; explicit cancel; orb cleanup; quit cleanup; fourteen poses rendered. No visible test overlay.\n");
  } finally {pet.Close();source.Close();}
 }
 static void RenderPoses(BitmapSource[] frames,string output) {
  var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()) {
   for(int i=0;i<frames.Length;i++) {
    drawing.DrawImage(frames[i],new Rect(i%4*192,i/4*192,192,192));
    if(i>=8) drawing.DrawLine(new Pen(Brushes.Gold,1),new Point(i%4*192,i/4*192+150),new Point(i%4*192+192,i/4*192+150));
   }
  }
  var bitmap=new RenderTargetBitmap(768,768,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,"poses.png"));encoder.Save(file);
 }
}
