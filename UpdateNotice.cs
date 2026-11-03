using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace RulershipPin;
internal sealed class UpdateNotice : Window
{
 internal static string Message(string name)=>"Hey "+name+", a new update is out!";
 internal UpdateNotice(Window owner,string name,Action update,Action later) {
  Owner=owner;Title="Rulership-Pin – Update available";Width=300;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
  WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
  var root=new StackPanel();var panel=new StackPanel {Margin=new Thickness(14)};
  var header=new DockPanel();var close=new Button {Content="×",ToolTip="Remind me later",Focusable=false,Padding=new Thickness(5,0,5,0),HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
  header.Children.Add(new TextBlock {Text="Update available",Foreground=Brushes.Lavender,FontSize=11,VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(header);
  var message=new StackPanel();message.Children.Add(new TextBlock {Text=Message(name),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White,FontSize=15,FontWeight=FontWeights.SemiBold});
  message.Children.Add(new TextBlock {Text="Click to update",Foreground=Brushes.LightGray,FontSize=11,Margin=new Thickness(0,5,0,0)});
  var activate=new Button {Content=message,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Margin=new Thickness(0,8,0,0),Padding=new Thickness(0),Focusable=false,Cursor=System.Windows.Input.Cursors.Hand,HorizontalContentAlignment=HorizontalAlignment.Stretch};activate.Click+=(_,_)=>update();close.Click+=(_,_)=>later();panel.Children.Add(activate);
  var border=new Border {Background=new SolidColorBrush(Color.FromArgb(248,40,32,53)),BorderBrush=new SolidColorBrush(Color.FromRgb(206,181,140)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Child=panel};root.Children.Add(border);
  root.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse("M0,0 L18,0 L9,13 Z"),Fill=border.Background,Stroke=border.BorderBrush,StrokeThickness=1,Width=18,Height=14,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,-1,0,0)});Content=root;
  SourceInitialized+=(_,_)=>{var handle=new WindowInteropHelper(this).Handle;long styles=Native.GetWindowLongPtr(handle,-20).ToInt64();Native.SetWindowLongPtr(handle,-20,(nint)(styles|0x08000000L|0x80L));};
 }
 internal void Position(Point anchor) {
  var area=SystemParameters.WorkArea;var owner=Owner;var handle=new WindowInteropHelper(owner).Handle;
  var monitor=new Native.MonitorInfo {Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>()};
  if(handle!=0 && Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref monitor)) {var dpi=VisualTreeHelper.GetDpi(owner);area=new Rect(monitor.Work.Left/dpi.DpiScaleX,monitor.Work.Top/dpi.DpiScaleY,(monitor.Work.Right-monitor.Work.Left)/dpi.DpiScaleX,(monitor.Work.Bottom-monitor.Work.Top)/dpi.DpiScaleY);}
  Left=Math.Clamp(anchor.X-Width/2,area.Left,Math.Max(area.Left,area.Right-Width));Top=Math.Clamp(anchor.Y-Math.Max(ActualHeight,130)-8,area.Top,Math.Max(area.Top,area.Bottom-Math.Max(ActualHeight,130)));
 }
}
