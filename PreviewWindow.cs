using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace RulershipPin;
internal sealed class PreviewWindow : Window
{
 readonly nint source; nint thumbnail;
 internal nint SourceHandle=>source;
 internal bool HasThumbnail=>thumbnail!=0;
 readonly DispatcherTimer monitor=new() {Interval=TimeSpan.FromSeconds(1)};
 readonly TextBlock status=new() {Foreground=Brushes.LightGray,FontSize=11,Margin=new Thickness(8,0,8,0)};
 readonly Border viewport=new() {Background=Brushes.Black};
 public PreviewWindow(nint source) {
  WindowTheme.FollowSystem(this);
  this.source=source;Title="Rulership-Pin – "+Native.Title(source);Width=440;Height=310;MinWidth=260;MinHeight=190;Topmost=true;ShowInTaskbar=false;
  Background=new SolidColorBrush(Color.FromRgb(32,26,43));Foreground=Brushes.White;ResizeMode=ResizeMode.CanResizeWithGrip;
  var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition {Height=new GridLength(24)});
  var open=new Button {Content="Open original window",Margin=new Thickness(6),Padding=new Thickness(8,3,8,3)};
  open.Click+=(_,_)=>{if(Native.IsIconic(source)) Native.ShowWindow(source,9);Native.SetForegroundWindow(source);};grid.Children.Add(open);
  Grid.SetRow(viewport,1);grid.Children.Add(viewport);Grid.SetRow(status,2);grid.Children.Add(status);Content=grid;
  SourceInitialized+=(_,_)=>{int hr=Native.DwmRegisterThumbnail(new WindowInteropHelper(this).Handle,source,out thumbnail);if(hr<0) status.Text=$"Preview unavailable (0x{hr:X8})";};
  ContentRendered+=(_,_)=>UpdateThumbnail();SizeChanged+=(_,_)=>Dispatcher.BeginInvoke(UpdateThumbnail);
  monitor.Tick+=(_,_)=>{if(!Native.IsWindow(source)) {Close();return;} UpdateThumbnail();};monitor.Start();
  Closed+=(_,_)=>{monitor.Stop();if(thumbnail!=0) {Native.DwmUnregisterThumbnail(thumbnail);thumbnail=0;}};
 }
 void UpdateThumbnail() {
  if(thumbnail==0 || viewport.ActualWidth<=0 || viewport.ActualHeight<=0) return;
  if(Native.DwmQueryThumbnailSourceSize(thumbnail,out var size)<0 || size.Width<=0 || size.Height<=0) return;
  var position=viewport.TranslatePoint(new Point(),this); var dpi=VisualTreeHelper.GetDpi(this);
  double scale=Math.Min(viewport.ActualWidth/size.Width,viewport.ActualHeight/size.Height);
  double w=size.Width*scale,h=size.Height*scale,x=position.X+(viewport.ActualWidth-w)/2,y=position.Y+(viewport.ActualHeight-h)/2;
  var props=new Native.ThumbnailProperties {Flags=1|4|8|16,Opacity=255,Visible=true,ClientOnly=false,Destination=new Native.Rect {Left=(int)(x*dpi.DpiScaleX),Top=(int)(y*dpi.DpiScaleY),Right=(int)((x+w)*dpi.DpiScaleX),Bottom=(int)((y+h)*dpi.DpiScaleY)}};
  int hr=Native.DwmUpdateThumbnailProperties(thumbnail,ref props);
  status.Text=hr<0 ? $"Preview error (0x{hr:X8})" : Native.IsIconic(source) ? "Source minimized – live preview may pause." : "Live preview • Interact in the original window";
 }
}
