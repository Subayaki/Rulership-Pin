using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace RulershipPin;

internal sealed record DesktopMonitor(nint Handle,Native.Rect Bounds,Native.Rect Work,double Scale);
internal enum PeekEdge {Bottom,Top,Left,Right}
internal sealed class EscapeRoute
{
 internal readonly Point[] Points;
 readonly double[] distances;
 internal double Length=>distances[^1];
 internal EscapeRoute(IReadOnlyList<DesktopMonitor> monitors,int width,int height,bool right) {
  var ordered=monitors.OrderBy(m=>(m.Work.Left+m.Work.Right)/2.0).ThenBy(m=>m.Work.Top).ToArray();
  if(!right) Array.Reverse(ordered);
  int left=monitors.Min(m=>m.Bounds.Left),edge=monitors.Max(m=>m.Bounds.Right);
  int overlapTop=monitors.Max(m=>m.Work.Top),overlapBottom=monitors.Min(m=>m.Work.Bottom);
  bool commonTrack=overlapBottom-overlapTop>=height;
  Point Center(DesktopMonitor monitor)=>new((monitor.Work.Left+monitor.Work.Right-width)/2.0,commonTrack?overlapBottom-height:monitor.Work.Bottom-height);
  var nodes=ordered.Select(Center).ToArray();
  Points=[new Point(right?left-width:edge,nodes[0].Y),..nodes,new Point(right?edge:left-width,nodes[^1].Y)];
  distances=new double[Points.Length];for(int i=1;i<Points.Length;i++) distances[i]=distances[i-1]+(Points[i]-Points[i-1]).Length;
 }
 internal Point At(double progress) {
  double distance=Math.Clamp(progress,0,1)*Length;
  for(int i=1;i<Points.Length;i++) if(distance<=distances[i]) {
   double length=distances[i]-distances[i-1];double t=length<.001?0:(distance-distances[i-1])/length;return Points[i-1]+(Points[i]-Points[i-1])*t;
  }
  return Points[^1];
 }
}
internal static class AdventureGeometry
{
 internal static List<DesktopMonitor> Monitors() {
  var result=new List<DesktopMonitor>();
  Native.EnumDisplayMonitors(0,0,(nint handle,nint dc,ref Native.Rect rect,nint data)=>{
   var info=new Native.MonitorInfo {Size=Marshal.SizeOf<Native.MonitorInfo>()};
   if(Native.GetMonitorInfo(handle,ref info)) {double scale=1;if(Native.GetDpiForMonitor(handle,0,out uint x,out _)==0) scale=x/96.0;result.Add(new(handle,info.Monitor,info.Work,scale));}return true;
  },0);return result;
 }
 internal static (int x,int y,double angle) Peek(Native.Rect screen,PeekEdge edge,int side,double along) {
  int horizontal=(int)Math.Round(screen.Left+along*Math.Max(0,screen.Right-screen.Left-side));
  int vertical=(int)Math.Round(screen.Top+along*Math.Max(0,screen.Bottom-screen.Top-side));
  int grip=(int)Math.Round(side*400.0/512),outside=side-grip;
  return edge switch {
   PeekEdge.Bottom=>(horizontal,screen.Bottom-grip,0),PeekEdge.Top=>(horizontal,screen.Top-outside,180),
   PeekEdge.Left=>(screen.Left-outside,vertical,90),_=>(screen.Right-grip,vertical,-90)
  };
 }
 internal static int RunX(Native.Rect screen,int width,bool right,double progress)=>(int)Math.Round(right?screen.Left-width+progress*(screen.Right-screen.Left+width*2):screen.Right-progress*(screen.Right-screen.Left+width*2));
}


