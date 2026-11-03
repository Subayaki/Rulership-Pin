using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace RulershipPin;
internal static class LowCortisolDiagnostics
{
 internal static void Run(string output) {
  Directory.CreateDirectory(output);
  var pet=new PetWindow(true);
  var chibi=(ChibiCompanion)pet.CharacterStage.Children[0];var contact=new DrawingVisual();
  if(pet.MusicVolume.Visibility!=Visibility.Collapsed) throw new InvalidOperationException("Volume visible before playback");
  using(var drawing=contact.RenderOpen()) {
   foreach(int index in Enumerable.Range(0,4)) {
    chibi.SetTime(0);chibi.RenderLowCortisol(.25+index*.5);
    chibi.Measure(new Size(240,360));chibi.Arrange(new Rect(0,0,240,360));chibi.UpdateLayout();
    var bitmap=new RenderTargetBitmap(240,360,96,96,PixelFormats.Pbgra32);bitmap.Render(chibi);
    drawing.DrawImage(bitmap,new Rect(index*240,0,240,360));
   }
  }
  var sheet=new RenderTargetBitmap(960,360,96,96,PixelFormats.Pbgra32);sheet.Render(contact);
  var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(sheet));using(var file=File.Create(Path.Combine(output,"low-cortisol-poses.png"))) png.Save(file);
  for(int i=0;i<100;i++) {chibi.React();if(chibi.IsLowCortisolPlaying) throw new InvalidOperationException("Random click started music");}
  var frame=new DispatcherFrame();var deadline=Stopwatch.StartNew();string? failure=null;bool played=false;
  chibi.MusicFailed+=message=>{failure=message;frame.Continue=false;};
  var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(50)};
  timer.Tick+=(_,_)=>{
   if(chibi.LowCortisolAudioReady && chibi.LowCortisolAudioPosition>.3) {
    if(pet.MusicVolume.Visibility!=Visibility.Visible) {failure="Volume slider was not shown";frame.Continue=false;return;}
    pet.MusicVolumeSlider.Value=0;
    if(chibi.LowCortisolVolume!=0) {failure="Volume slider did not mute";frame.Continue=false;return;}
    chibi.RenderLowCortisol(.75);
    var content=(FrameworkElement)pet.Content;content.Measure(new Size(pet.Width,pet.Height));content.Arrange(new Rect(0,0,pet.Width,pet.Height));content.UpdateLayout();
    var preview=new RenderTargetBitmap((int)Math.Ceiling(pet.Width),(int)Math.Ceiling(pet.Height),96,96,PixelFormats.Pbgra32);preview.Render(content);
    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(preview));using(var file=File.Create(Path.Combine(output,"volume-preview.png"))) encoder.Save(file);
    played=true;frame.Continue=false;
   }
   if(deadline.Elapsed.TotalSeconds>12) {failure="Muted audio decode/playback timed out";frame.Continue=false;}
  };
  timer.Start();
  try {chibi.PlayLowCortisol(Path.Combine(AppContext.BaseDirectory,"Assets","low-cortisol.m4a"),0);Dispatcher.PushFrame(frame);}
  finally {timer.Stop();chibi.StopLowCortisol();pet.Close();}
  if(!played || failure is not null) throw new InvalidOperationException(failure??"Audio did not play");
  if(chibi.IsLowCortisolPlaying) throw new InvalidOperationException("Audio stop failed");
  if(pet.MusicVolume.Visibility!=Visibility.Collapsed) throw new InvalidOperationException("Volume slider did not hide after Stop");
  File.WriteAllText(Path.Combine(output,"checks.txt"),"PASS: four poses rendered; 100 random clicks never start music; supplied Short audio decoded and advanced in muted playback; volume slider hidden before play, visible while playing, live mute verified, hidden after Stop; Stop closed playback. No visible app or audible test playback.\n");
 }
}
