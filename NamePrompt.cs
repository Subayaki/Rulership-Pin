using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace RulershipPin;
internal sealed class NamePrompt : Window
{
 internal readonly TextBox NameInput;
 internal bool Saved {get;private set;}
 internal NamePrompt(Window owner,bool firstRun,string initialName,Point anchor) {
  Owner=owner;Title="Rulership-Pin – Your name";Width=304;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
  WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;
  var root=new StackPanel();var panel=new StackPanel {Margin=new Thickness(16)};
  panel.Children.Add(new TextBlock {Text=firstRun?"Hi! What's your name?":"What should I call you?",FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White});
  panel.Children.Add(new TextBlock {Text="I'll use it on my sign and in future greetings.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Lavender,FontSize=12,Margin=new Thickness(0,6,0,0)});
  NameInput=new TextBox {Text=initialName,MaxLength=24,FontSize=15,Padding=new Thickness(7,5,7,5),Margin=new Thickness(0,12,0,0)};
  AutomationProperties.SetName(NameInput,"Your name");panel.Children.Add(NameInput);
  panel.Children.Add(new TextBlock {Text="Saved only on this device.",Foreground=Brushes.LightGray,FontSize=11,Margin=new Thickness(0,5,0,0)});
  var error=new TextBlock {Foreground=Brushes.LightSalmon,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,0)};panel.Children.Add(error);
  var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
  var later=new Button {Content=firstRun?"Later":"Cancel",IsCancel=true,Padding=new Thickness(12,4,12,4),Margin=new Thickness(0,0,8,0)};later.Click+=(_,_)=>Close();buttons.Children.Add(later);
  var save=new Button {Content="Save",IsDefault=true,Padding=new Thickness(12,4,12,4)};
  save.Click+=(_,_)=>{if(CompanionSettings.TrySaveUserName(NameInput.Text,out var message)) {Saved=true;Close();}else {error.Text=message;error.Visibility=Visibility.Visible;}};buttons.Children.Add(save);panel.Children.Add(buttons);
  var border=new Border {Background=new SolidColorBrush(Color.FromArgb(248,40,32,53)),BorderBrush=new SolidColorBrush(Color.FromRgb(206,181,140)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Child=panel};root.Children.Add(border);
  root.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse("M0,0 L18,0 L9,13 Z"),Fill=border.Background,Stroke=border.BorderBrush,StrokeThickness=1,Width=18,Height=14,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,-1,0,0)});Content=root;
  void PositionBubble() {
   var area=SystemParameters.WorkArea;var handle=new System.Windows.Interop.WindowInteropHelper(owner).Handle;
   var monitor=new Native.MonitorInfo {Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>()};
   if(handle!=0 && Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref monitor)) {var dpi=VisualTreeHelper.GetDpi(owner);area=new Rect(monitor.Work.Left/dpi.DpiScaleX,monitor.Work.Top/dpi.DpiScaleY,(monitor.Work.Right-monitor.Work.Left)/dpi.DpiScaleX,(monitor.Work.Bottom-monitor.Work.Top)/dpi.DpiScaleY);}
   Left=Math.Clamp(anchor.X-Width/2,area.Left,Math.Max(area.Left,area.Right-Width));Top=Math.Clamp(anchor.Y-ActualHeight-8,area.Top,Math.Max(area.Top,area.Bottom-ActualHeight));
  }
  Loaded+=(_,_)=>{PositionBubble();NameInput.Focus();NameInput.SelectAll();};SizeChanged+=(_,_)=>PositionBubble();
 }
}
