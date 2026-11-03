using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RulershipPin;
internal static class ChibiReactionDiagnostics
{
 internal static void Render(string directory) {
  Directory.CreateDirectory(directory);
  var contact=new DrawingVisual();
  using(var drawing=contact.RenderOpen()) {
   int column=0;
   foreach(var emotion in Enum.GetValues<ChibiCompanion.Emotion>()) {
    var chibi=new ChibiCompanion();
    double duration=ChibiCompanion.Duration(emotion);
    chibi.StartReaction(emotion,0);
    int row=0;
    foreach(double time in new[]{.6,1.6,emotion==ChibiCompanion.Emotion.Sit?14:3.2,duration+.1}) {
     chibi.SetTime(time);
     if((time<duration)!=(chibi.ActiveEmotion==emotion)) throw new InvalidOperationException("Reaction duration/idle return failed: "+emotion);
     chibi.Measure(new Size(240,360));chibi.Arrange(new Rect(0,0,240,360));chibi.UpdateLayout();
     var bitmap=new RenderTargetBitmap(240,360,96,96,PixelFormats.Pbgra32);bitmap.Render(chibi);
     Save(bitmap,Path.Combine(directory,$"{emotion}-{row}.png"));
     drawing.DrawImage(bitmap,new Rect(column*240,row*380,240,360));
     drawing.DrawText(new FormattedText($"{emotion} {time:0.0}s",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,Brushes.White,1),new Point(column*240+8,row*380+355));
     row++;
    }
    column++;
   }
  }
  var sheet=new RenderTargetBitmap(1200,1520,96,96,PixelFormats.Pbgra32);sheet.Render(contact);Save(sheet,Path.Combine(directory,"contact.png"));
  // The chooser must reach every reaction and never repeat immediately.
  var random=new ChibiCompanion();var seen=new HashSet<ChibiCompanion.Emotion>();ChibiCompanion.Emotion? previous=null;
  for(int i=0;i<200;i++) {random.React();var next=random.ActiveEmotion!.Value;if(next==previous) throw new InvalidOperationException("Immediate reaction repeat");seen.Add(next);previous=next;}
  if(seen.Count!=5) throw new InvalidOperationException("Reaction selection did not cover all five emotions");
  int outings=0;
  var automatic=new ChibiCompanion {AutomaticAdventures=false,AdventureRequested=_=>outings++};var automaticSequence=new List<ChibiCompanion.Emotion>();double lastEnd=0;bool wasActive=false;
  for(int tick=0;tick<40000;tick++) {
   double seconds=tick/10.0;
   automatic.AdvanceIdle(seconds);automatic.SetTime(seconds);
   bool active=automatic.ActiveEmotion.HasValue;
   if(active && !wasActive) {
    double gap=seconds-lastEnd;
    if(gap<7.9 || gap>18.2) throw new InvalidOperationException("Idle spacing out of bounds");
    automaticSequence.Add(automatic.ActiveEmotion!.Value);
    if(automatic.IsLowCortisolPlaying) throw new InvalidOperationException("Idle activity started music");
   }
   if(!active && wasActive) lastEnd=seconds;
   wasActive=active;
  }
  if(automaticSequence.Count<100) throw new InvalidOperationException("Too few automatic animations");
  if(outings!=0) throw new InvalidOperationException("Disabled automatic Lurk/escape was requested");
  for(int i=1;i<automaticSequence.Count;i++) if(automaticSequence[i]==automaticSequence[i-1]) throw new InvalidOperationException("Automatic immediate repeat");
  for(int i=0;i+5<=automaticSequence.Count;i+=5) if(automaticSequence.Skip(i).Take(5).Distinct().Count()!=5) throw new InvalidOperationException("Automatic shuffle skipped an emotion");
  var disabled=new ChibiCompanion {SpontaneousAnimations=false};disabled.AdvanceIdle(1000);
  if(disabled.ActiveEmotion is not null) throw new InvalidOperationException("Disabled idle still animates");
  disabled.SpontaneousAnimations=true;disabled.PauseSpontaneous(true);disabled.AdvanceIdle(1000);
  if(disabled.ActiveEmotion is not null) throw new InvalidOperationException("Paused idle still animates");
  File.WriteAllText(Path.Combine(directory,"idle-checks.txt"),$"PASS: {automaticSequence.Count} automatic animations over 4000 simulated seconds; all five per shuffled set; no immediate repeats; 8–18 second idle gaps; no music; disabled/paused behavior.\n");
  File.WriteAllText(Path.Combine(directory,"checks.txt"),"PASS: five reactions, durations, automatic idle return, random coverage and no immediate repeats. Sitting remains active at 14 seconds and returns at 16 seconds. Desktop input still requires interactive verification.");
 }
 static void Save(BitmapSource bitmap,string path) {
  var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
 }
}
