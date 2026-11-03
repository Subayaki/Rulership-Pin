using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RulershipPin;
internal static class OrbDiagnostics
{
 internal static void Run(string output) {
  Directory.CreateDirectory(output);File.Delete(Path.Combine(output,"orb-error.txt"));
  var pet=new PetWindow(true);new WindowInteropHelper(pet).EnsureHandle();
  var sources=Enumerable.Range(0,4).Select(i=>new Window {Title=$"Hidden orb source {i}",Width=640,Height=360}).ToArray();
  var handles=sources.Select(source=>new WindowInteropHelper(source).EnsureHandle()).ToArray();
  var pins=handles.Take(3).Select((handle,i)=>new PreviewWindow(handle) {Owner=pet,Left=40+i*460,Top=130}).ToArray();
  foreach(var pin in pins) {if(!pet.RegisterPreview(pin)) throw new InvalidOperationException("Could not add three previews");new WindowInteropHelper(pin).EnsureHandle();}
  try {
   if(pet.PinCount!=3 || pins.Any(pin=>!pin.HasThumbnail)) throw new InvalidOperationException("DWM registration failed.");
   var fourth=new PreviewWindow(handles[3]) {Owner=pet};
   if(pet.RegisterPreview(fourth)) throw new InvalidOperationException("Fourth preview was accepted");fourth.Close();
   var duplicate=new PreviewWindow(handles[0]) {Owner=pet};
   if(pet.RegisterPreview(duplicate)) throw new InvalidOperationException("Duplicate preview was accepted");duplicate.Close();
   var positions=pins.Select(pin=>new Point(pin.Left,pin.Top)).ToArray();
   pet.SetCollapsed(true);
   if(!pet.IsCollapsed || pet.ExpandedContent.Visibility!=Visibility.Collapsed || pet.Orb.Visibility!=Visibility.Visible || pins.Any(pin=>!pin.HasThumbnail) || pet.WindowState!=WindowState.Normal) throw new InvalidOperationException("Collapse did not preserve previews.");
   pet.Orb.Measure(new Size(48,48));pet.Orb.Arrange(new Rect(0,0,48,48));pet.Orb.UpdateLayout();
   var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(pet.Orb),null,new Rect(0,0,96,96));
   var bitmap=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,"horse-orb-preview.png"))) png.Save(file);
   pet.Left+=25;pet.Top+=10;
   if(pins.Where((pin,i)=>new Point(pin.Left,pin.Top)!=positions[i]).Any()) throw new InvalidOperationException("Orb movement moved previews");
   pet.SetCollapsed(false);
   if(pet.IsCollapsed || pet.ExpandedContent.Visibility!=Visibility.Visible || pins.Any(pin=>!pin.HasThumbnail) || pins.Where((pin,i)=>new Point(pin.Left,pin.Top)!=positions[i]).Any()) throw new InvalidOperationException("Expansion changed or destroyed previews.");
   pins[0].Close();
   if(pet.PinCount!=2 || pins[1..].Any(pin=>!pin.HasThumbnail)) throw new InvalidOperationException("Closing one pin affected others");
   var replacement=new PreviewWindow(handles[3]) {Owner=pet};
   if(!pet.RegisterPreview(replacement)) throw new InvalidOperationException("Closed slot was not reusable");new WindowInteropHelper(replacement).EnsureHandle();
   pet.Close();
   if(pet.PinCount!=0 || pins.Any(pin=>pin.HasThumbnail) || replacement.HasThumbnail) throw new InvalidOperationException("Quit did not clean up all previews");
   File.WriteAllText(Path.Combine(output,"orb-validation.txt"),"PASS: three hidden DWM previews, fourth/duplicate rejection, collapse/expand and orb movement preserve all three thumbnails and positions, independent close, slot reuse, quit cleanup. Orb artwork rendered.\n");
  } finally {pet.Close();foreach(var source in sources) source.Close();}
 }
}
