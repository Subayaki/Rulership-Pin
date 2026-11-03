using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace RulershipPin;

internal sealed class DesktopAdventure : IDisposable
{
 readonly Window owner;readonly FrameworkElement home;readonly Action restored;
 readonly DispatcherTimer timer=new(DispatcherPriority.Render) {Interval=TimeSpan.FromMilliseconds(1000.0/30)};
 readonly Stopwatch time=new();
 readonly BitmapSource[] right,left,peek;
 Window? overlay;Image? image;nint hwnd;
 DesktopMonitor? target,origin;
 List<DesktopMonitor> displays=[];
 EscapeRoute? route;
 string mode="";double runSeconds,along;bool toRight;PeekEdge edge;
 Point homePoint;int side;
 internal bool Active=>overlay is not null;
 internal nint OverlayHandle=>hwnd;
 internal DesktopAdventure(Window owner,FrameworkElement home,Action restored) {
  this.owner=owner;this.home=home;this.restored=restored;
  var run=Load("rulership-run.png");
  BitmapSource[] Row(int row)=>Enumerable.Range(0,4).Select(i=>Crop(run,i,row,4,2)).ToArray();
  right=Row(0);left=Row(1);
  var lurk=Load("rulership-lurk.png");
  peek=Enumerable.Range(0,6).Select(i=>RegisterPeek(Crop(lurk,i%3,i/3,3,2))).ToArray();
  timer.Tick+=(_,_)=>Tick();Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
 }
 static BitmapSource Load(string name) {
  var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(Path.Combine(AppContext.BaseDirectory,"Assets",name));bitmap.EndInit();bitmap.Freeze();return bitmap;
 }
 static BitmapSource Crop(BitmapSource sheet,int col,int row,int columns,int rows) {
  var frame=new CroppedBitmap(sheet,new Int32Rect(col*(sheet.PixelWidth/columns),row*(sheet.PixelHeight/rows),sheet.PixelWidth/columns,sheet.PixelHeight/rows));frame.Freeze();return frame;
 }
 static BitmapSource RegisterPeek(BitmapSource frame) {
  int w=frame.PixelWidth,h=frame.PixelHeight;var pixels=new FormatConvertedBitmap(frame,PixelFormats.Bgra32,null,0);var bytes=new byte[w*h*4];pixels.CopyPixels(bytes,w*4,0);
  int last=0;
  for(int y=(int)(h*.55);y<(int)(h*.95);y++) for(int x=(int)(w*.2);x<(int)(w*.8);x++) if(bytes[(y*w+x)*4+3]>180) last=Math.Max(last,y);
  if(last==0) throw new InvalidDataException("Lurk artwork has no registered hand line");
  double offset=Math.Round(h*400.0/512-(last-h*8.0/512));
  var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()) drawing.DrawImage(frame,new Rect(0,offset,w,h));
  var result=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
 }
 internal BitmapSource[] PoseFrames=>[..right,..left,..peek];
 void DisplayChanged(object? sender,EventArgs e)=>owner.Dispatcher.BeginInvoke(Cancel);
 internal bool Start(string id,double scale,bool showOverlay=true) {
  Cancel(false);displays=AdventureGeometry.Monitors();if(displays.Count==0) return false;
  mode=id;target=displays[Random.Shared.Next(displays.Count)];
  var monitor=Native.MonitorFromWindow(new WindowInteropHelper(owner).Handle,2);origin=displays.FirstOrDefault(m=>m.Handle==monitor)??displays[0];
  toRight=Random.Shared.Next(2)==0;edge=(PeekEdge)Random.Shared.Next(4);along=.12+Random.Shared.NextDouble()*.76;
  // One physical sprite size for the entire outing, including DPI boundaries.
  side=Math.Max(32,(int)Math.Round(156*scale*origin.Scale));
  Native.GetWindowRect(new WindowInteropHelper(owner).Handle,out var bounds);
  var offset=home.TranslatePoint(new Point(),owner);var dpi=VisualTreeHelper.GetDpi(home);
  homePoint=new Point(bounds.Left+(offset.X+home.ActualWidth/2)*dpi.DpiScaleX-side/2.0,bounds.Top+(offset.Y+home.ActualHeight)*dpi.DpiScaleY-side);
  image=new Image {Source=toRight?right[0]:left[0],Stretch=Stretch.Uniform,IsHitTestVisible=false};
  overlay=new Window {Title="Rulership – "+(id=="lurk"?"Lurk":"Mini escape"),WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowInTaskbar=false,ShowActivated=false,ResizeMode=ResizeMode.NoResize,Width=side/origin.Scale,Height=side/origin.Scale,Content=image,Owner=owner,IsHitTestVisible=false};
  try {
   hwnd=new WindowInteropHelper(overlay).EnsureHandle();
   var styles=Native.GetWindowLongPtr(hwnd,-20).ToInt64();Native.SetWindowLongPtr(hwnd,-20,(nint)(styles|0x08000000L|0x20L|0x80L));
   home.Visibility=Visibility.Hidden;
   Place((int)homePoint.X,(int)homePoint.Y,[origin.Bounds]);
   if(showOverlay) overlay.Show();
   route=new EscapeRoute(displays,side,side,toRight);runSeconds=Math.Clamp(route.Length/(240*origin.Scale),5,40);
   time.Restart();if(showOverlay) timer.Start();return true;
  } catch {Cancel(false);throw;}
 }
 void Place(int x,int y,IEnumerable<Native.Rect> clips) {
  Native.SetWindowPos(hwnd,(nint)(-1),x,y,side,side,0x10);
  nint region=Native.CreateRectRgn(0,0,0,0);
  foreach(var clip in clips) {
   int l=Math.Clamp(clip.Left-x,0,side),t=Math.Clamp(clip.Top-y,0,side),r=Math.Clamp(clip.Right-x,l,side),b=Math.Clamp(clip.Bottom-y,t,side);
   nint part=Native.CreateRectRgn(l,t,r,b);Native.CombineRgn(region,region,part,2);Native.DeleteObject(part);
  }
  if(Native.SetWindowRgn(hwnd,region,false)==0) Native.DeleteObject(region);
 }
 void Tick()=>Evaluate(time.Elapsed.TotalSeconds);
 internal void Evaluate(double elapsed) {
  if(overlay is null || image is null || target is null || origin is null) return;
  if(elapsed<1.15) {
   image.Source=(toRight?right:left)[(int)(elapsed*9)%4];
   double delta=elapsed/1.15*(side+180*origin.Scale);
   Place((int)Math.Round(homePoint.X+(toRight?delta:-delta)),(int)homePoint.Y,[origin.Bounds]);
   overlay.Opacity=1-Math.Clamp((elapsed-.9)/.25,0,1);return;
  }
  double action=elapsed-2;
  if(action<0) {overlay.Opacity=0;return;}
  if(mode=="mini-escape") {
   if(action>runSeconds+2.5) {Cancel();return;}
   if(action>runSeconds) {overlay.Opacity=0;return;}
   overlay.Opacity=1;image.RenderTransform=Transform.Identity;
   image.Source=(toRight?right:left)[(int)(action*9)%4];
   var point=route!.At(action/runSeconds);Place((int)Math.Round(point.X),(int)Math.Round(point.Y),displays.Select(m=>m.Bounds));
  } else {
   if(action>8.2) {Cancel();return;}
   if(action>5.7) {overlay.Opacity=0;return;}
   overlay.Opacity=1;var placement=AdventureGeometry.Peek(target.Bounds,edge,side,along);
   image.RenderTransformOrigin=new Point(.5,.5);image.RenderTransform=new RotateTransform(placement.angle);
   int[] sequence=[0,1,2,3,4,5,4,3,2,1,0];image.Source=peek[sequence[Math.Min(sequence.Length-1,(int)(action/5.7*sequence.Length))]];
   Place(placement.x,placement.y,[target.Bounds]);
  }
 }
 internal void Cancel()=>Cancel(true);
 void Cancel(bool notify) {
  bool wasActive=Active;timer.Stop();time.Stop();overlay?.Close();overlay=null;image=null;hwnd=0;home.Visibility=Visibility.Visible;
  if(notify && wasActive) restored();
 }
 public void Dispose() {Cancel(false);Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;}
}
