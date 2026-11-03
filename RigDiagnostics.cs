using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RulershipPin;
internal static class RigDiagnostics
{
 internal static void Render(string directory,bool includeAnimation=true)
 {
  Directory.CreateDirectory(directory);
  var rig=new RigCompanion();
  (double time,string label)[] samples=[(0,"Idle"),(1.1,"Breathing"),(4.01,"Blink"),(9,"Look around"),(17.5,"Raise arm"),(19.2,"Scratch head"),(36,"Hand on hip"),(52,"Look back")];
  rig.Measure(new Size(256,384));rig.Arrange(new Rect(0,0,256,384));rig.UpdateLayout();
  var sheet=new DrawingVisual();
  var hashes=new HashSet<string>();
  using(var dc=sheet.RenderOpen())
  {
   dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(39,33,48)),null,new Rect(0,0,1024,816));
   for(int i=0;i<samples.Length;i++)
   {
    rig.SetTime(samples[i].time);rig.UpdateLayout();
    var bitmap=new RenderTargetBitmap(256,384,96,96,PixelFormats.Pbgra32);bitmap.Render(rig);
    var bytes=new byte[256*384*4];bitmap.CopyPixels(bytes,256*4,0);hashes.Add(Convert.ToHexString(SHA256.HashData(bytes)));
    foreach(var core in new[]{new Point(105,232),new Point(149,276),new Point(125,160),new Point(128,68)})
     if(bytes[((int)core.Y*256+(int)core.X)*4+3]<240)
      throw new InvalidOperationException($"Body coverage is too transparent at {core} in sample {i}.");
    Save(bitmap,Path.Combine(directory,$"rig-{i:00}.png"));
    int x=i%4*256,y=i/4*408;
    dc.DrawImage(bitmap,new Rect(x,y,256,384));
    dc.DrawText(new FormattedText(samples[i].label,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,Brushes.White,1),new Point(x+12,y+384));
   }
  }
  var contact=new RenderTargetBitmap(1024,816,96,96,PixelFormats.Pbgra32);contact.Render(sheet);Save(contact,Path.Combine(directory,"rig-contact-sheet.png"));
  if(hashes.Count!=samples.Length) throw new InvalidOperationException("Animation samples unexpectedly contain duplicate poses.");
  // Confirm that folding the arm keeps limb lengths and reaches the head target.
  var shoulder=new Point(516,325);var target=new Point(513,177);
  var joints=RigCompanion.ArmJoints(shoulder,target,145,186,-1);
  if(Math.Abs((joints.elbow-shoulder).Length-145)>.001 || Math.Abs((joints.hand-joints.elbow).Length-186)>.001 || (joints.hand-target).Length>.001)
   throw new InvalidOperationException("Arm rig cannot reach its scratch target.");
  if(joints.elbow.X>=shoulder.X) throw new InvalidOperationException("Scratch elbow must fold outside the body.");
  var previous=RigCompanion.PoseArms(0);double maximumElbowStep=0;
  for(int frame=1;frame<=64*60;frame++) {
   var current=RigCompanion.PoseArms(frame/60.0);
   foreach(var pair in new[]{(current.left,previous.left,1),(current.right,previous.right,1)}) {
    Vector upper=pair.Item1.Elbow-pair.Item1.Shoulder,lower=pair.Item1.Hand-pair.Item1.Elbow;
    double cross=Vector.CrossProduct(upper,lower)*pair.Item3;
    if(cross<-.001) throw new InvalidOperationException("Elbow changed its anatomical bend branch.");
    double step=(pair.Item1.Elbow-pair.Item2.Elbow).Length;maximumElbowStep=Math.Max(maximumElbowStep,step);
    if(step>20) throw new InvalidOperationException($"Elbow jumped {step:F2} source pixels in one frame.");
   }
   previous=current;
  }
  // Foreground alpha-zero arm edges must never cut a hole in the face.
  for(int frame=0;frame<=30;frame++) {
   rig.SetTime(17+frame*7.0/30);rig.UpdateLayout();
   var frameImage=new RenderTargetBitmap(256,384,96,96,PixelFormats.Pbgra32);frameImage.Render(rig);
   var pixels=new byte[256*384*4];frameImage.CopyPixels(pixels,256*4,0);
   foreach(var point in new[]{new Point(128,68),new Point(125,72),new Point(130,73)})
    if(pixels[((int)point.Y*256+(int)point.X)*4+3]<240) throw new InvalidOperationException("A foreground hand edge punched a hole in the face.");
  }
  var timer=System.Diagnostics.Stopwatch.StartNew();
  for(int i=0;i<120;i++) rig.SetTime(i/30.0);
  timer.Stop();
  File.WriteAllText(Path.Combine(directory,"validation.txt"),$"PASS: eight distinct rendered motion samples. PASS: core body/leg/face alpha >= 240. PASS: two-bone reach and branch orientation over the whole 64s cycle. Maximum elbow step at 60 FPS: {maximumElbowStep:F2} source pixels. PASS: face coverage during 31 scratch frames. Mesh update average: {timer.Elapsed.TotalMilliseconds/120:F2} ms (excludes drawing). Visual contact sheet still requires inspection.");
  if(!includeAnimation) return;
  // A direct render of the running rig, rather than crossfading pose stills.
  // Preview is sampled at 12.5 FPS; the desktop rig updates up to 30 FPS.
  rig.Measure(new Size(192,288));rig.Arrange(new Rect(0,0,192,288));rig.UpdateLayout();
  var gif=new GifBitmapEncoder();
  for(int i=0;i<538;i++)
  {
   rig.SetTime(i*.08);rig.UpdateLayout();
   var transparent=new RenderTargetBitmap(192,288,96,96,PixelFormats.Pbgra32);transparent.Render(rig);
   var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) {
    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(39,33,48)),null,new Rect(0,0,192,288));dc.DrawImage(transparent,new Rect(0,0,192,288));
   }
   var image=new RenderTargetBitmap(192,288,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();
   var metadata=new BitmapMetadata("gif");metadata.SetQuery("/grctlext/Delay",(ushort)8);
   gif.Frames.Add(BitmapFrame.Create(image,null,metadata,null));
  }
  string animationPath=Path.Combine(directory,"rig-motion-preview.gif");
  using(var animationFile=File.Create(animationPath)) gif.Save(animationFile);
  GifAnimationTiming.Apply(animationPath,8);
 }
 static void Save(BitmapSource bitmap,string path) {
  var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
 }
}
