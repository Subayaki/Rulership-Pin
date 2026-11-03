using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace RulershipPin;
public partial class PetWindow : Window
{
 double companionScale=1;
 double musicVolume=.35;
 bool collapsed,suppressPreviewMovement,orbMoved;
 Native.Rect expandedBounds;
 readonly MenuItem collapseItem=new() {Header="Minimize to orb"};
 readonly List<MenuItem> expandedOnly=[];
 internal bool IsCollapsed=>collapsed;
 Point? pressPoint;
 readonly DispatcherTimer clickTimer=new();
 readonly DispatcherTimer pinButtonTimer=new() {Interval=TimeSpan.FromSeconds(2)};
 readonly Native.WinEventProc eventCallback;
 nint hook;
 readonly Dictionary<nint,PreviewWindow> previews=[];
 DesktopAdventure? adventure;
 internal bool AdventureActive=>adventure?.Active==true;
 internal DesktopAdventure? Adventure=>adventure;
 Point? lastPetPosition;
 internal int PinCount=>previews.Count;
 public PetWindow():this(false) { }
 internal PetWindow(bool diagnostic) {
  InitializeComponent();
  PetSurface.LostMouseCapture+=(_,_)=>{if(pressPoint is not null) {pressPoint=null;if(CharacterStage.Children[0] is ChibiCompanion chibi) chibi.PauseSpontaneous(false);}};
  ExpandedContent.MouseEnter+=(_,_)=>RevealPinButton();
  ExpandedContent.MouseLeave+=(_,_)=>{if(!collapsed) {pinButtonTimer.Stop();pinButtonTimer.Start();}};
  pinButtonTimer.Tick+=(_,_)=>{
   pinButtonTimer.Stop();
   if(!ExpandedContent.IsMouseOver) ChooseButton.Visibility=Visibility.Hidden;
  };
  clickTimer.Interval=TimeSpan.FromMilliseconds(GetDoubleClickTime());
  clickTimer.Tick+=(_,_)=>{clickTimer.Stop();if(CharacterStage.Children[0] is ChibiCompanion chibi) chibi.React();};
  companionScale=CompanionSettings.LoadScale();
  ApplyCompanionScale(companionScale,false);
  Left=Math.Max(SystemParameters.WorkArea.Left,SystemParameters.WorkArea.Right-Width-30);
  Top=Math.Max(SystemParameters.WorkArea.Top,SystemParameters.WorkArea.Bottom-Height-30);
  var menu=new ContextMenu();
  var lurk=new MenuItem {Header="Lurk"};lurk.Click+=(_,_)=>StartAdventure("lurk");
  var escape=new MenuItem {Header="Mini escape"};escape.Click+=(_,_)=>StartAdventure("mini-escape");
  var returnIdle=new MenuItem {Header="Return to idle"};returnIdle.Click+=(_,_)=>{adventure?.Cancel();if(CharacterStage.Children[0] is ChibiCompanion chibi) {chibi.StopLowCortisol();chibi.ReturnToIdle();}};
  var spontaneous=new MenuItem {Header="Spontaneous animations",IsCheckable=true,IsChecked=CompanionSettings.LoadSpontaneousAnimations()};
  var automaticAdventures=new MenuItem {Header="Automatic Lurk & escape",IsCheckable=true,IsChecked=CompanionSettings.LoadAutomaticAdventures()};
  automaticAdventures.Click+=(_,_)=>{
   if(!CompanionSettings.TrySaveAutomaticAdventures(automaticAdventures.IsChecked,out var message)) {automaticAdventures.IsChecked=!automaticAdventures.IsChecked;MessageBox.Show(this,message,"Rulership-Pin – Settings");return;}
   if(CharacterStage.Children[0] is ChibiCompanion chibi) chibi.AutomaticAdventures=automaticAdventures.IsChecked;
   if(!automaticAdventures.IsChecked) adventure?.Cancel();
  };
  spontaneous.Click+=(_,_)=>{
   if(!CompanionSettings.TrySaveSpontaneousAnimations(spontaneous.IsChecked,out var message)) {spontaneous.IsChecked=!spontaneous.IsChecked;MessageBox.Show(this,message,"Rulership-Pin – Settings");return;}
   if(CharacterStage.Children[0] is ChibiCompanion chibi) {chibi.SpontaneousAnimations=spontaneous.IsChecked;chibi.PauseSpontaneous(false);}
  };
  var lowCortisol=new MenuItem {Header="Low cortisol"};
  var playLow=new MenuItem {Header="Play"};
  var stopLow=new MenuItem {Header="Stop"};
  var chooseMusic=new MenuItem {Header="Choose music…"};
  ChibiCompanion? Chibi()=>CharacterStage.Children.Count>0?CharacterStage.Children[0] as ChibiCompanion:null;
  string? SelectMusic() {
   var picker=new Microsoft.Win32.OpenFileDialog {Title="Choose low cortisol music",Filter="Audio files|*.mp3;*.wav;*.m4a;*.wma|All files|*.*",CheckFileExists=true};
   if(picker.ShowDialog(this)!=true) return null;
   if(!CompanionSettings.TrySaveMusic(picker.FileName,out var message)) {MessageBox.Show(this,message,"Rulership-Pin – Settings");return null;}
   return picker.FileName;
  }
  playLow.Click+=(_,_)=>{
   adventure?.Cancel();
   clickTimer.Stop();var chibi=Chibi();if(chibi is null || collapsed) return;
   string? path=CompanionSettings.LoadMusic();
   if(!System.IO.File.Exists(path)) path=System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","low-cortisol.m4a");
   if(!System.IO.File.Exists(path)) path=SelectMusic();
   if(path is not null) chibi.PlayLowCortisol(path,musicVolume);
  };
  stopLow.Click+=(_,_)=>Chibi()?.StopLowCortisol();
  chooseMusic.Click+=(_,_)=>{Chibi()?.StopLowCortisol();SelectMusic();};
  lowCortisol.Items.Add(playLow);lowCortisol.Items.Add(stopLow);lowCortisol.Items.Add(new Separator());lowCortisol.Items.Add(chooseMusic);
  menu.Opened+=(_,_)=>{lowCortisol.IsEnabled=!collapsed && Chibi() is not null;stopLow.IsEnabled=Chibi()?.IsLowCortisolPlaying==true;};
  menu.Opened+=(_,_)=>{lurk.IsEnabled=escape.IsEnabled=!collapsed && Chibi() is not null;};
  string selectedModel=diagnostic?"Chibi":CompanionSettings.LoadModel();
  void ShowModel(string model) {
   adventure?.Cancel();
   clickTimer.Stop();pressPoint=null;
   Chibi()?.StopLowCortisol();
   CharacterStage.Children.Clear();
   CharacterStage.Children.Add(model=="Original"?new RigCompanion():new ChibiCompanion());
   MusicVolume.Visibility=Visibility.Collapsed;
   if(Chibi() is ChibiCompanion chibi) {
    chibi.AdventureRequested=id=>StartAdventure(id);
    chibi.SpontaneousAnimations=spontaneous.IsChecked;
    chibi.AutomaticAdventures=automaticAdventures.IsChecked;
    chibi.LowCortisolPlaybackChanged+=playing=>MusicVolume.Visibility=playing && !collapsed?Visibility.Visible:Visibility.Collapsed;
    if(!diagnostic) chibi.MusicFailed+=message=>MessageBox.Show(this,message,"Rulership-Pin – Audio",MessageBoxButton.OK,MessageBoxImage.Warning);
   }
  }
  ShowModel(selectedModel);
  var modelMenu=new MenuItem {Header="Model"};
  foreach(string model in new[]{"Chibi","Original"}) {
   var item=new MenuItem {Header=model=="Original"?"Original (Beta)":model,Tag=model,IsCheckable=true,IsChecked=model==selectedModel};
   item.Click+=(_,_)=>{
    if(model!=selectedModel) {
     if(!CompanionSettings.TrySaveModel(model,out var message)) {
      item.IsChecked=false;
      MessageBox.Show(this,message,"Rulership-Pin – Settings",MessageBoxButton.OK,MessageBoxImage.Warning);return;
     }
     selectedModel=model;ShowModel(model);
    }
    foreach(MenuItem sibling in modelMenu.Items) sibling.IsChecked=(string)sibling.Tag==selectedModel;
   };
   modelMenu.Items.Add(item);
  }
  menu.Items.Add(modelMenu);
  var animations=new MenuItem {Header="Animations"};
  animations.Items.Add(lurk);animations.Items.Add(escape);animations.Items.Add(lowCortisol);
  animations.Items.Add(new Separator());animations.Items.Add(returnIdle);
  animations.Items.Add(new Separator());animations.Items.Add(spontaneous);
  animations.Items.Add(automaticAdventures);
  menu.Items.Add(animations);
  var choose=new MenuItem {Header="Choose a window…"}; choose.Click+=ChooseWindow; menu.Items.Add(choose);
  var detach=new MenuItem {Header="Close all pins"}; detach.Click+=(_,_)=>{foreach(var pin in previews.Values.ToArray()) pin.Close();}; menu.Items.Add(detach);
  var size=new MenuItem {Header="Adjust companion size"}; size.Click+=AdjustCompanionSize; menu.Items.Add(size);
  expandedOnly.AddRange([modelMenu,animations,choose,size]);
  collapseItem.Click+=(_,_)=>SetCollapsed(!collapsed);menu.Items.Add(collapseItem);
  var update=new MenuItem {Header="Check for updates…"};update.Click+=async(_,_)=>await AppUpdater.Check(this,update);menu.Items.Add(update);
  var exit=new MenuItem {Header="Quit Rulership-Pin"}; exit.Click+=(_,_)=>Close(); menu.Items.Add(exit); ContextMenu=menu;
  eventCallback=OnWindowMoved;
  if(!diagnostic) SourceInitialized+=(_,_)=>hook=Native.SetWinEventHook(0x000B,0x000B,0,eventCallback,0,0,2);
  LocationChanged+=(_,_)=>PositionPreview();lastPetPosition=new Point(Left,Top);UpdatePinButton();
  Closed+=(_,_)=>{adventure?.Dispose();pinButtonTimer.Stop();clickTimer.Stop();Chibi()?.StopLowCortisol(); if(hook!=0) Native.UnhookWinEvent(hook); foreach(var pin in previews.Values.ToArray()) pin.Close(); };
 }
 internal void StartAdventure(string id,bool showOverlay=true) {
  if(collapsed || CharacterStage.Children[0] is not ChibiCompanion chibi) return;
  clickTimer.Stop();chibi.StopLowCortisol();chibi.ReturnToIdle();
  adventure??=new DesktopAdventure(this,CharacterStage,()=>{if(CharacterStage.Children[0] is ChibiCompanion current) current.ReturnToIdle();});
  adventure.Start(id,companionScale,showOverlay);
 }
 internal void CancelAdventure()=>adventure?.Cancel();
 void RevealPinButton() {
  if(collapsed) return;
  ChooseButton.Visibility=Visibility.Visible;
  pinButtonTimer.Stop();pinButtonTimer.Start();
 }
 void MusicVolumeChanged(object sender,RoutedPropertyChangedEventArgs<double> e) {
  musicVolume=e.NewValue/100;
  if(CharacterStage is not null && CharacterStage.Children.Count>0 && CharacterStage.Children[0] is ChibiCompanion chibi) chibi.SetMusicVolume(musicVolume);
  if(MusicVolume is not null) MusicVolume.ToolTip=$"Music volume: {e.NewValue:0}%";
 }
 internal void SetCollapsed(bool value) {
  if(collapsed==value) return;
  adventure?.Cancel();
  pinButtonTimer.Stop();ChooseButton.Visibility=Visibility.Hidden;
  clickTimer.Stop();pressPoint=null;if(PetSurface.IsMouseCaptured) PetSurface.ReleaseMouseCapture();
  var handle=new WindowInteropHelper(this).Handle;suppressPreviewMovement=true;
  try {
   if(value) {
    if(CharacterStage.Children[0] is ChibiCompanion chibi) chibi.StopLowCortisol();
    Native.GetWindowRect(handle,out expandedBounds);collapsed=true;orbMoved=false;
    ExpandedContent.Visibility=Visibility.Collapsed;Orb.Visibility=Visibility.Visible;Width=48;Height=48;
    var monitor=new Native.MonitorInfo {Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>()};Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref monitor);
    var dpi=VisualTreeHelper.GetDpi(this);int side=(int)Math.Round(48*dpi.DpiScaleX);
    int centerY=(expandedBounds.Top+expandedBounds.Bottom)/2;
    int x=(expandedBounds.Left+expandedBounds.Right)/2<(monitor.Work.Left+monitor.Work.Right)/2?monitor.Work.Left:monitor.Work.Right-side;
    Native.SetWindowPos(handle,0,x,Math.Clamp(centerY-side/2,monitor.Work.Top,Math.Max(monitor.Work.Top,monitor.Work.Bottom-side)),side,side,4|16);
   } else {
    Native.GetWindowRect(handle,out var orbBounds);collapsed=false;
    Orb.Visibility=Visibility.Collapsed;ExpandedContent.Visibility=Visibility.Visible;ApplyCompanionScale(companionScale,false);
    if(orbMoved) {
     var info=new Native.MonitorInfo {Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>()};Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref info);
     var dpi=VisualTreeHelper.GetDpi(this);int w=(int)Math.Round(Width*dpi.DpiScaleX),h=(int)Math.Round(Height*dpi.DpiScaleY);
     int x=Math.Clamp((orbBounds.Left+orbBounds.Right-w)/2,info.Work.Left,Math.Max(info.Work.Left,info.Work.Right-w));
     int y=Math.Clamp((orbBounds.Top+orbBounds.Bottom-h)/2,info.Work.Top,Math.Max(info.Work.Top,info.Work.Bottom-h));
     Native.SetWindowPos(handle,0,x,y,w,h,4|16);
    } else Native.SetWindowPos(handle,0,expandedBounds.Left,expandedBounds.Top,expandedBounds.Right-expandedBounds.Left,expandedBounds.Bottom-expandedBounds.Top,4|16);

   }
   foreach(var item in expandedOnly) item.IsEnabled=!collapsed;
   collapseItem.Header=collapsed?"Expand Rulership":"Minimize to orb";
  } finally {
   
   Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,()=>{suppressPreviewMovement=false;});
  }
 }
 void OrbDown(object sender,MouseButtonEventArgs e) {
  e.Handled=true;
  if(e.ClickCount==2) {SetCollapsed(false);return;}
  Native.GetWindowRect(new WindowInteropHelper(this).Handle,out var before);DragMove();
  Native.GetWindowRect(new WindowInteropHelper(this).Handle,out var after);orbMoved|=before.Left!=after.Left || before.Top!=after.Top;
 }


 void ApplyCompanionScale(double scale,bool keepAnchor=true) {
  double center=Left+Width/2,bottom=Top+Height;
  companionScale=Math.Clamp(scale,.5,1.75);
  CharacterStage.Width=240*companionScale;CharacterStage.Height=360*companionScale;
  Width=Math.Max(180,CharacterStage.Width);Height=CharacterStage.Height+38;
  if(keepAnchor) {
   var area=SystemParameters.WorkArea;
   Left=Math.Clamp(center-Width/2,area.Left,Math.Max(area.Left,area.Right-Width));
   Top=Math.Clamp(bottom-Height,area.Top,Math.Max(area.Top,area.Bottom-Height));
   PositionPreview();
  }
 }
 void AdjustCompanionSize(object? sender,RoutedEventArgs e) {
  double originalScale=companionScale;bool applied=false;
  var dialog=new Window {Title="Adjust companion size",Width=390,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,Owner=this,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=new SolidColorBrush(Color.FromRgb(32,26,43)),Foreground=Brushes.White};
  WindowTheme.FollowSystem(dialog);
  var panel=new StackPanel {Margin=new Thickness(20)};
  panel.Children.Add(new TextBlock {Text="Companion size",FontSize=18});
  panel.Children.Add(new TextBlock {Text="Preview changes live. The pinned window keeps its size.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,12)});
  var value=new TextBlock {Text=$"{companionScale:P0}",HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(value);
  var slider=new Slider {Minimum=50,Maximum=175,Value=companionScale*100,TickFrequency=5,IsSnapToTickEnabled=true,Margin=new Thickness(0,12,0,12)};
  slider.ValueChanged+=(_,_)=>{ApplyCompanionScale(slider.Value/100);value.Text=$"{slider.Value:0}%";};panel.Children.Add(slider);
  var error=new TextBlock {Foreground=Brushes.LightSalmon,TextWrapping=TextWrapping.Wrap};panel.Children.Add(error);
  var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
  var reset=new Button {Content="Reset",Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,0,8,0)};reset.Click+=(_,_)=>slider.Value=100;buttons.Children.Add(reset);
  var cancel=new Button {Content="Cancel",IsCancel=true,Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,0,8,0)};cancel.Click+=(_,_)=>dialog.Close();buttons.Children.Add(cancel);
  var apply=new Button {Content="Apply",IsDefault=true,Padding=new Thickness(12,5,12,5)};
  apply.Click+=(_,_)=>{
   if(CompanionSettings.TrySaveScale(companionScale,out var message)) {applied=true;dialog.Close();}
   else error.Text=message;
  };buttons.Children.Add(apply);panel.Children.Add(buttons);dialog.Content=panel;
  dialog.Closed+=(_,_)=>{if(!applied) ApplyCompanionScale(originalScale);};dialog.ShowDialog();
 }
 [System.Runtime.InteropServices.DllImport("user32.dll")]
 static extern uint GetDoubleClickTime();
 void MovePet(object sender,MouseButtonEventArgs e) {
  adventure?.Cancel();
  clickTimer.Stop();
  if(e.ClickCount==2) {pressPoint=null;PetSurface.ReleaseMouseCapture();ChooseWindow(sender,e);return;}
  if(CharacterStage.Children[0] is ChibiCompanion chibi) chibi.PauseSpontaneous(true);
  pressPoint=e.GetPosition(this);PetSurface.CaptureMouse();e.Handled=true;
 }
 void MovePetPointer(object sender,MouseEventArgs e) {
  if(pressPoint is not Point start || e.LeftButton!=MouseButtonState.Pressed) return;
  var delta=e.GetPosition(this)-start;
  if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance) return;
  pressPoint=null;clickTimer.Stop();PetSurface.ReleaseMouseCapture();
  try {DragMove();} finally {if(CharacterStage.Children[0] is ChibiCompanion chibi) chibi.PauseSpontaneous(false);}
 }
 void ReleasePet(object sender,MouseButtonEventArgs e) {
  bool clicked=pressPoint.HasValue && PetSurface.IsMouseCaptured;
  pressPoint=null;PetSurface.ReleaseMouseCapture();
  if(CharacterStage.Children[0] is ChibiCompanion chibi) chibi.PauseSpontaneous(false);
  if(clicked) RevealPinButton();
  if(clicked && CharacterStage.Children[0] is ChibiCompanion) clickTimer.Start();
 }
 void OnWindowMoved(nint h,uint evt,nint hwnd,int obj,int child,uint thread,uint time) {
  if(collapsed) return;
  if(!Native.Eligible(hwnd)) return;
  Native.GetCursorPos(out var p);
  if(!Native.GetWindowRect(new WindowInteropHelper(this).Handle,out var rect)) return;
  if(p.X>=rect.Left && p.X<=rect.Right && p.Y>=rect.Top && p.Y<=rect.Bottom)
   Dispatcher.BeginInvoke(()=>Attach(hwnd));
 }
 void Attach(nint hwnd) {
  if(collapsed || !Native.IsWindow(hwnd)) return;
  if(previews.TryGetValue(hwnd,out var existing)) {existing.Activate();return;}
  if(previews.Count>=3) {ShowPinLimit();return;}
  var pin=new PreviewWindow(hwnd) {Owner=this};
  if(!RegisterPreview(pin)) {pin.Close();return;}
  pin.Show();ArrangePins();
 }
 internal bool RegisterPreview(PreviewWindow pin) {
  if(previews.Count>=3 || previews.ContainsKey(pin.SourceHandle)) return false;
  previews.Add(pin.SourceHandle,pin);
  pin.Closed+=(_,_)=>{previews.Remove(pin.SourceHandle);UpdatePinButton();};UpdatePinButton();return true;
 }
 void UpdatePinButton()=>ChooseButton.Content=$"+ Pin a window ({previews.Count}/3)";
 void ShowPinLimit()=>MessageBox.Show(this,"Close one of your three pins before adding another.","Rulership-Pin – Pin limit",MessageBoxButton.OK,MessageBoxImage.Information);
 void PositionPreview() {
  var current=new Point(Left,Top);
  if(!collapsed && !suppressPreviewMovement && lastPetPosition is Point previous) {
   var delta=current-previous;
   foreach(var pin in previews.Values) {pin.Left+=delta.X;pin.Top+=delta.Y;}
  }
  lastPetPosition=current;
 }
 void ArrangePins() {
  if(previews.Count==0 || collapsed) return;
  var area=SystemParameters.WorkArea;
  var handle=new WindowInteropHelper(this).Handle;
  var monitor=new Native.MonitorInfo {Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>()};
  if(handle!=0 && Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref monitor)) {
   var dpi=VisualTreeHelper.GetDpi(this);area=new Rect(monitor.Work.Left/dpi.DpiScaleX,monitor.Work.Top/dpi.DpiScaleY,(monitor.Work.Right-monitor.Work.Left)/dpi.DpiScaleX,(monitor.Work.Bottom-monitor.Work.Top)/dpi.DpiScaleY);
  }
  var pins=previews.Values.ToArray();const double gap=12;
  int columns=Math.Min(pins.Length,Math.Max(1,(int)((area.Width+gap)/(260+gap))));
  int rows=(pins.Length+columns-1)/columns;
  double width=Math.Min(440,(area.Width-gap*(columns-1))/columns);
  double height=Math.Min(pins.Max(pin=>pin.Height),Math.Max(190,(area.Height-gap*(rows-1))/rows));
  double totalWidth=width*columns+gap*(columns-1),totalHeight=height*rows+gap*(rows-1);
  double left=Math.Clamp(Left+Width/2-totalWidth/2,area.Left,Math.Max(area.Left,area.Right-totalWidth));
  double top=Math.Clamp(Top-totalHeight-8,area.Top,Math.Max(area.Top,area.Bottom-totalHeight));
  for(int i=0;i<pins.Length;i++) {
   pins[i].Width=width;pins[i].Height=height;pins[i].Left=left+i%columns*(width+gap);pins[i].Top=top+i/columns*(height+gap);
  }
 }
 void ChooseWindow(object? sender,RoutedEventArgs e) {
  if(collapsed) return;
  if(previews.Count>=3) {ShowPinLimit();return;}
  var picker=new Window {Title="Rulership-Pin – Choose a window",Width=520,Height=400,Topmost=true,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=new SolidColorBrush(Color.FromRgb(32,26,43)),Foreground=Brushes.White};
  WindowTheme.FollowSystem(picker);
  var panel=new DockPanel {Margin=new Thickness(14)};
  var hint=new TextBlock {Text="Select a window, then double-click or press Enter to pin it.",Margin=new Thickness(0,0,0,12)}; DockPanel.SetDock(hint,Dock.Top);panel.Children.Add(hint);
  var list=new ListBox();
  Native.EnumWindows((hwnd,_)=>{if(Native.Eligible(hwnd) && !previews.ContainsKey(hwnd)) list.Items.Add(new ListBoxItem {Content=Native.Title(hwnd),Tag=hwnd}); return true;},0);
  void Accept() {if(list.SelectedItem is ListBoxItem item) {var hwnd=(nint)item.Tag;picker.Close();Attach(hwnd);}}
  list.MouseDoubleClick+=(_,_)=>Accept();list.KeyDown+=(_,key)=>{if(key.Key==Key.Enter) Accept();};
  panel.Children.Add(list);picker.Content=panel;picker.ShowDialog();
 }
}

