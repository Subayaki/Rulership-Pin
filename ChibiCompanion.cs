using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RulershipPin;

// Official game artwork, animated as 2D sprites; no extracted 3D rig.
public sealed class ChibiCompanion : Grid
{
 readonly Image idle, happy;
 readonly Image reactionA=new() {Width=256,Height=256},reactionB=new() {Width=256,Height=256};
 readonly BitmapSource[] reactions;
 readonly BitmapSource[] lowCortisolFrames;
 MediaPlayer? music;
 bool musicReady;
 double musicRequestedAt;
 internal bool IsLowCortisolPlaying=>music is not null;
 internal bool LowCortisolAudioReady=>musicReady;
 internal double LowCortisolAudioPosition=>music?.Position.TotalSeconds??0;
 internal double LowCortisolVolume=>music?.Volume??0;
 internal event Action<string>? MusicFailed;
 internal event Action<bool>? LowCortisolPlaybackChanged;
 internal Action<string>? AdventureRequested;
 internal void SetMusicVolume(double volume) {if(music is not null) music.Volume=Math.Clamp(volume,0,1);}
 internal const double LowCortisolDuration=16;
 readonly Canvas effects=new() {Width=280,Height=280,IsHitTestVisible=false};
 readonly TextBlock[] hearts;
 internal enum Emotion {Shy,Wave,Dance,Sit,Joy}
 internal Emotion? ActiveEmotion {get;private set;}
 Emotion? previousEmotion;
 readonly Queue<Emotion> reactionBag=new();
 double nextIdleAt=Random.Shared.Next(8,19);
 bool spontaneousPaused;
 internal bool SpontaneousAnimations {get;set;}=true;
 internal bool AutomaticAdventures {get;set;}=true;
 internal void PauseSpontaneous(bool paused) {spontaneousPaused=paused;ScheduleIdle(clock.Elapsed.TotalSeconds);}
 void ScheduleIdle(double seconds)=>nextIdleAt=seconds+Random.Shared.Next(8,19);
 Emotion NextReaction() {
  if(reactionBag.Count==0) {
   var choices=Enum.GetValues<Emotion>();Random.Shared.Shuffle(choices);
   if(choices[0]==previousEmotion) (choices[0],choices[1])=(choices[1],choices[0]);
   foreach(var choice in choices) reactionBag.Enqueue(choice);
  }
  return reactionBag.Dequeue();
 }
 internal void AdvanceIdle(double seconds) {
  if(!SpontaneousAnimations || spontaneousPaused || music is not null || ActiveEmotion is not null || seconds<nextIdleAt) return;
  if(AutomaticAdventures && AdventureRequested is not null && Random.Shared.Next(5)==0) {ScheduleIdle(seconds);AdventureRequested(Random.Shared.Next(2)==0?"lurk":"mini-escape");return;}
  StartReaction(NextReaction(),seconds);
 }
 internal void ReturnToIdle() {ActiveEmotion=null;ScheduleIdle(clock.Elapsed.TotalSeconds);SetTime(clock.Elapsed.TotalSeconds);}
 double reactionStart;
 internal static double Duration(Emotion emotion)=>emotion switch {Emotion.Shy=>5,Emotion.Wave=>6,Emotion.Dance=>8,Emotion.Sit=>16,_=>5};
 readonly ScaleTransform breathing=new(1,1);
 readonly RotateTransform sway=new();
 readonly TranslateTransform bob=new();
 readonly Stopwatch clock=new();
 TimeSpan lastFrame;
 bool subscribed;
 public ChibiCompanion() {
  IsHitTestVisible=false;
  var sheet=new BitmapImage();sheet.BeginInit();sheet.CacheOption=BitmapCacheOption.OnLoad;
  sheet.UriSource=new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","rulership-chibi-sheet.png"));sheet.EndInit();sheet.Freeze();
  Image Sprite(int x) {
   var source=new CroppedBitmap(sheet,new Int32Rect(x,0,256,256));source.Freeze();
   return new Image {Source=source,Width=256,Height=256,Stretch=Stretch.Uniform};
  }
  idle=Sprite(0);happy=Sprite(256);
  var atlas=new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","rulership-chibi-reactions.png")));atlas.Freeze();
  reactions=Enumerable.Range(0,6).Select(i=>{
   var frame=new CroppedBitmap(atlas,new Int32Rect(i%3*(atlas.PixelWidth/3),i/3*(atlas.PixelHeight/2),atlas.PixelWidth/3,atlas.PixelHeight/2));frame.Freeze();return (BitmapSource)frame;
  }).ToArray();
  // Letter each sign in its own source-image coordinates. Text and artwork
  // become one texture, so pose changes, fading and motion cannot separate them.
  reactions[1]=LetterSign(reactions[1],new Point(105,361),new Point(218,355),new Point(108,431));
  reactions[2]=LetterSign(reactions[2],new Point(103,367),new Point(222,361),new Point(104,436));
  var lowAtlas=new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","rulership-low-cortisol.png")));lowAtlas.Freeze();
  lowCortisolFrames=Enumerable.Range(0,4).Select(i=>{
   var frame=new CroppedBitmap(lowAtlas,new Int32Rect(i%2*(lowAtlas.PixelWidth/2),i/2*(lowAtlas.PixelHeight/2),lowAtlas.PixelWidth/2,lowAtlas.PixelHeight/2));frame.Freeze();return (BitmapSource)frame;
  }).ToArray();
  // Square artwork stays proportional inside the existing companion stage.
  var stage=new Grid {Width=280,Height=280,RenderTransformOrigin=new Point(.5,.9)};
  var transforms=new TransformGroup();transforms.Children.Add(breathing);transforms.Children.Add(sway);transforms.Children.Add(bob);
  stage.RenderTransform=transforms;stage.Children.Add(idle);stage.Children.Add(happy);stage.Children.Add(reactionA);stage.Children.Add(reactionB);
  hearts=Enumerable.Range(0,5).Select(_=>new TextBlock {Text="♥",FontSize=23,Foreground=Brushes.HotPink}).ToArray();
  foreach(var heart in hearts) effects.Children.Add(heart);
  stage.Children.Add(effects);
  Children.Add(new Viewbox {Child=stage,Stretch=Stretch.Uniform,Margin=new Thickness(2,0,2,0),VerticalAlignment=VerticalAlignment.Bottom});
  Loaded+=(_,_)=>UpdateSubscription();Unloaded+=(_,_)=>Stop();IsVisibleChanged+=(_,_)=>UpdateSubscription();
  SetTime(0);
 }
 static BitmapSource LetterSign(BitmapSource sprite,Point topLeft,Point topRight,Point bottomLeft) {
  var visual=new DrawingVisual();
  using(var drawing=visual.RenderOpen()) {
   drawing.DrawImage(sprite,new Rect(0,0,sprite.PixelWidth,sprite.PixelHeight));
   double scale=sprite.PixelWidth/512.0;
   Vector horizontal=(topRight-topLeft)/120,vertical=(bottomLeft-topLeft)/72;
   drawing.PushTransform(new MatrixTransform(new Matrix(horizontal.X*scale,horizontal.Y*scale,vertical.X*scale,vertical.Y*scale,topLeft.X*scale,topLeft.Y*scale)));
   drawing.PushClip(new RectangleGeometry(new Rect(4,9,112,59)));
   var ink=new SolidColorBrush(Color.FromRgb(91,57,43));ink.Freeze();
   // A warm handwritten ink style belongs to the cream board surface.
   string greeting=CompanionSettings.LoadGreeting().Replace("Hello ","Hello\n",StringComparison.Ordinal);
   var lettering=new FormattedText(greeting,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe Print"),19,ink,1) {TextAlignment=TextAlignment.Center,LineHeight=24};
   if(lettering.WidthIncludingTrailingWhitespace>108) lettering.SetFontSize(Math.Max(7,19*108/lettering.WidthIncludingTrailingWhitespace));
   drawing.DrawText(lettering,new Point(60,15));
   drawing.Pop();drawing.Pop();
  }
  var result=new RenderTargetBitmap(sprite.PixelWidth,sprite.PixelHeight,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
 }
 public void React() {
  StopLowCortisol();
  StartReaction(NextReaction(),clock.Elapsed.TotalSeconds);
 }
 internal void PlayLowCortisol(string path,double volume=.35) {
  StopLowCortisol();ActiveEmotion=null;
  var player=new MediaPlayer {Volume=Math.Clamp(volume,0,1)};music=player;musicReady=false;musicRequestedAt=clock.Elapsed.TotalSeconds;
  player.MediaOpened+=(_,_)=>{if(!ReferenceEquals(music,player)) return;musicReady=true;musicRequestedAt=clock.Elapsed.TotalSeconds;player.Position=TimeSpan.Zero;player.Play();LowCortisolPlaybackChanged?.Invoke(true);};
  player.MediaEnded+=(_,_)=>{if(ReferenceEquals(music,player)) StopLowCortisol();};
  player.MediaFailed+=(_,_)=>{if(!ReferenceEquals(music,player)) return;StopLowCortisol();MusicFailed?.Invoke("Could not play the music file. Choose a supported MP3 or WAV file.");};
  try {player.Open(new Uri(Path.GetFullPath(path)));}
  catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) {StopLowCortisol();MusicFailed?.Invoke("Could not open the music file. Choose another MP3 or WAV file.");}
 }
 internal void StopLowCortisol() {
  var player=music;music=null;musicReady=false;player?.Close();
  ScheduleIdle(clock.Elapsed.TotalSeconds);
  LowCortisolPlaybackChanged?.Invoke(false);
  if(player is not null) SetTime(clock.Elapsed.TotalSeconds);
 }
 internal void StartReaction(Emotion emotion,double seconds) {ActiveEmotion=emotion;previousEmotion=emotion;reactionStart=seconds;SetTime(seconds);}
 static double Smooth(double x) {x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
 void UpdateSubscription() {
  if(!IsLoaded || !IsVisible) {Stop();return;}
  if(subscribed) return;
  subscribed=true;clock.Start();ScheduleIdle(clock.Elapsed.TotalSeconds);CompositionTarget.Rendering+=Frame;
 }
 void Stop() {
  spontaneousPaused=false;
  StopLowCortisol();
  if(!subscribed) return;
  CompositionTarget.Rendering-=Frame;subscribed=false;clock.Stop();
 }
 void Frame(object? sender,EventArgs e) {
  if(clock.Elapsed-lastFrame<TimeSpan.FromSeconds(1.0/30)) return;
  lastFrame=clock.Elapsed;AdvanceIdle(lastFrame.TotalSeconds);SetTime(lastFrame.TotalSeconds);
 }
 internal void SetTime(double seconds) {
  double breath=Math.Sin(seconds*Math.PI*2/4.2);
  breathing.ScaleY=1+breath*.012;breathing.ScaleX=1-breath*.004;
  sway.Angle=Math.Sin(seconds*.72)*1.5;
  bob.Y=Math.Sin(seconds*1.1)*1.1;
  bob.X=0;
  happy.Visibility=Visibility.Hidden;
  idle.Visibility=Visibility.Visible;
  idle.Opacity=happy.Opacity=1;
  reactionA.Opacity=reactionB.Opacity=0;
  foreach(var heart in hearts) heart.Opacity=0;
  if(music is not null) {
   double elapsed=seconds-musicRequestedAt;
   if(!musicReady) {
    if(elapsed>10) {StopLowCortisol();MusicFailed?.Invoke("The music file did not open. Please choose another file.");}
    return;
   }
   double position=music.Position.TotalSeconds;
   if(position>=LowCortisolDuration || elapsed>=LowCortisolDuration+2) {StopLowCortisol();return;}
   RenderLowCortisol(position);return;
  }
  if(ActiveEmotion is not Emotion emotion) return;
  double time=Math.Max(0,seconds-reactionStart),duration=Duration(emotion);
  if(time>=duration) {ActiveEmotion=null;ScheduleIdle(seconds);return;}
  double weight=Smooth(time/.25)*Smooth((duration-time)/.45);
  idle.Opacity=happy.Opacity=1-weight;
  int first=0,second=0;double blend=0;
  switch(emotion) {
   case Emotion.Shy:
    first=second=0;sway.Angle+=Math.Sin(time*4)*2*weight;bob.Y+=3*weight;break;
   case Emotion.Wave:
    first=1;second=2;blend=Math.Sin(time*9)>=0?1:0;
    sway.Angle+=Math.Sin(time*2)*2*weight;break;
   case Emotion.Dance:
    first=3;second=4;blend=Math.Sin(time*Math.PI*2*1.65)>=0?1:0;
    sway.Angle=Math.Sin(time*Math.PI*2*1.65)*7*weight;
    bob.X=Math.Sin(time*Math.PI*2*.825)*13*weight;
    bob.Y=-Math.Abs(Math.Sin(time*Math.PI*2*1.65))*7*weight;
    breathing.ScaleY+=Math.Sin(time*Math.PI*2*3.3)*.014*weight;break;
   case Emotion.Sit:
    first=second=5;sway.Angle=Math.Sin(time*.8)*.8;
    bob.Y=Math.Sin(time*1.3)*.6;break;
   case Emotion.Joy:
    // Happy original pose, with three soft hops and floating hearts.
    reactionA.Source=happy.Source;reactionB.Source=happy.Source;
    double jump=Math.Max(0,Math.Sin(Math.Min(time,3)*Math.PI*2));
    bob.Y=-jump*22*weight;sway.Angle=Math.Sin(time*5)*3*weight;
    for(int i=0;i<hearts.Length;i++) {
     double phase=(time*.45+i*.19)%1;
     Canvas.SetLeft(hearts[i],35+i*43+Math.Sin(time*2+i)*5);
     Canvas.SetTop(hearts[i],115-phase*90);
     hearts[i].Opacity=Math.Sin(phase*Math.PI)*weight;
    }
    break;
  }
  if(emotion!=Emotion.Joy) {reactionA.Source=reactions[first];reactionB.Source=reactions[second];}
  reactionA.Opacity=(1-blend)*weight;reactionB.Opacity=blend*weight;
 }
 internal void RenderLowCortisol(double time) {
  // Four held drawings with continuous motion, evaluated at the audio position.
  // This activity is deliberately separate from the five random emotions.
  double weight=Smooth(time/.25)*Smooth((LowCortisolDuration-time)/.5);
  idle.Opacity=happy.Opacity=1-weight;
  double phase=time*Math.PI;
  int frame=(int)Math.Floor(time*2)%4;
  reactionA.Source=lowCortisolFrames[frame];reactionA.Opacity=weight;reactionB.Opacity=0;
  sway.Angle=Math.Sin(phase)*2.5*weight;
  bob.X=Math.Sin(phase)*5*weight;bob.Y=(1-Math.Cos(phase*2))*.8*weight;
  breathing.ScaleX=1;breathing.ScaleY=1+Math.Sin(phase*2)*.004*weight;
  foreach(var heart in hearts) heart.Opacity=0;
 }
}
