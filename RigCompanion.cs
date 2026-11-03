using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
namespace RulershipPin;

// One T-pose texture and measured coordinate system. Separate meshes retain
// matching proportions while arms, coat and tail can move at different depths.
public sealed class RigCompanion : Grid
{
 enum Layer {Body,LeftArm,RightArm,LeftCoat,RightCoat,Tail,LeftSleeve,RightSleeve}
 readonly Viewport3D viewport=new() {ClipToBounds=true,IsHitTestVisible=false};
 readonly OrthographicCamera camera;
 readonly Dictionary<Layer,MeshGeometry3D> meshes=[];
 readonly Dictionary<Layer,Point[]> sources=[];
 readonly Dictionary<Layer,GeometryModel3D> models=[];
 readonly Model3DGroup group=new();
 int foregroundMask=-1;
 readonly Stopwatch clock=new();
 TimeSpan lastFrame;
 public RigCompanion()
 {
  var texture=new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","rulership-tpose-body-v01.png")));
  var coatTexture=new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","rulership-tpose-coat-v01.png")));
  var brush=new ImageBrush(texture) {ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,1,1),TileMode=TileMode.None};brush.Freeze();
  // Diffuse + white ambient preserves the PNG's alpha. Emissive-only output
  // contributes RGB without coverage on a transparent layered desktop window.
  var material=new DiffuseMaterial(brush);material.Freeze();
  var coatBrush=new ImageBrush(coatTexture) {ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,1,1),TileMode=TileMode.None};coatBrush.Freeze();
  var coatMaterial=new DiffuseMaterial(coatBrush);coatMaterial.Freeze();
  var xs=Axis(1254,12,490,755,5);var ys=Axis(1254,12,140,370,5);
  var rest=new List<Point>();foreach(double y in ys) foreach(double x in xs) rest.Add(new Point(x,y));
  var globalIndices=Enum.GetValues<Layer>().ToDictionary(layer=>layer,_=>new List<int>());
  for(int row=0;row<ys.Count-1;row++) for(int col=0;col<xs.Count-1;col++)
  {
   int a=row*xs.Count+col,b=a+1,c=a+xs.Count,d=c+1;
   var layer=Region((xs[col]+xs[col+1])/2,(ys[row]+ys[row+1])/2);
   globalIndices[layer].AddRange([a,c,b,b,c,d]);
   globalIndices[(xs[col]+xs[col+1])/2<627?Layer.LeftCoat:Layer.RightCoat].AddRange([a,c,b,b,c,d]);
  }
  group.Children.Add(new AmbientLight(Colors.White));
  foreach(var layer in new[]{Layer.Tail,Layer.LeftCoat,Layer.RightCoat,Layer.LeftArm,Layer.RightArm,Layer.Body})
  {
   var unique=globalIndices[layer].Distinct().ToArray();
   var map=unique.Select((global,local)=>(global,local)).ToDictionary(p=>p.global,p=>p.local);
   sources[layer]=unique.Select(index=>rest[index]).ToArray();
   var uv=new PointCollection(sources[layer].Select(p=>new Point(p.X/1254,p.Y/1254)));uv.Freeze();
   var indices=new Int32Collection(globalIndices[layer].Select(index=>map[index]));indices.Freeze();
   var mesh=new MeshGeometry3D {TextureCoordinates=uv,TriangleIndices=indices};meshes[layer]=mesh;
   var layerMaterial=layer is Layer.LeftCoat or Layer.RightCoat?coatMaterial:material;
   models[layer]=new GeometryModel3D(mesh,layerMaterial) {BackMaterial=layerMaterial};
  }
  var sleeves=new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","rulership-jacket-sleeves-v01.png")));
  BuildSleeve(Layer.LeftSleeve,TrimSleeve(new CroppedBitmap(sleeves,new Int32Rect(0,0,sleeves.PixelWidth/2,sleeves.PixelHeight))));
  BuildSleeve(Layer.RightSleeve,TrimSleeve(new CroppedBitmap(sleeves,new Int32Rect(sleeves.PixelWidth/2,0,sleeves.PixelWidth-sleeves.PixelWidth/2,sleeves.PixelHeight))));
  viewport.Children.Add(new ModelVisual3D {Content=group});
  camera=new OrthographicCamera(new Point3D(627,-615,2000),new Vector3D(0,0,-1),new Vector3D(0,1,0),870);
  viewport.Camera=camera;Children.Add(viewport);SetTime(0);
  Loaded+=(_,_)=>{clock.Start();CompositionTarget.Rendering+=OnFrame;};
  Unloaded+=(_,_)=>{CompositionTarget.Rendering-=OnFrame;clock.Stop();};
 }
 static BitmapSource TrimSleeve(BitmapSource image) {
  var pixels=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);int w=pixels.PixelWidth,h=pixels.PixelHeight;
  var bytes=new byte[w*h*4];pixels.CopyPixels(bytes,w*4,0);int left=w,right=0,top=h,bottom=0;
  for(int y=0;y<h;y++) for(int x=0;x<w;x++) if(bytes[(y*w+x)*4+3]>100) {left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
  var result=new CroppedBitmap(image,new Int32Rect(left,top,right-left+1,bottom-top+1));result.Freeze();return result;
 }
 void BuildSleeve(Layer layer,BitmapSource image) {
  var points=new List<Point>();var indices=new Int32Collection();
  const int columns=8,rows=16;
  for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++) points.Add(new Point(x/(double)columns,y/(double)rows));
  for(int y=0;y<rows;y++) for(int x=0;x<columns;x++) {int a=y*(columns+1)+x,c=a+columns+1;foreach(int n in new[]{a,c,a+1,a+1,c,c+1}) indices.Add(n);}
  var uv=new PointCollection(points);uv.Freeze();indices.Freeze();
  var mesh=new MeshGeometry3D {TextureCoordinates=uv,TriangleIndices=indices};meshes[layer]=mesh;sources[layer]=points.ToArray();
  var brush=new ImageBrush(image) {ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,1,1)};brush.Freeze();
  var material=new DiffuseMaterial(brush);material.Freeze();models[layer]=new GeometryModel3D(mesh,material) {BackMaterial=material};
 }
 static List<double> Axis(int end,int step,int denseStart,int denseEnd,int denseStep)
 {
  var values=new SortedSet<double> {0,end};for(int i=0;i<end;i+=step) values.Add(i);
  for(int i=denseStart;i<=denseEnd;i+=denseStep) values.Add(i);return values.ToList();
 }
 static Layer Region(double x,double y)
 {
  if(y>=290 && y<365 && x<516) return Layer.LeftArm;
  if(y>=290 && y<365 && x>734) return Layer.RightArm;
  if(y>=615 && y<1030 && x>755) return Layer.Tail;
  return Layer.Body;
 }
 void OnFrame(object? sender,EventArgs e)
 {
  if(!IsVisible || clock.Elapsed-lastFrame<TimeSpan.FromMilliseconds(1000.0/30)) return;
  lastFrame=clock.Elapsed;SetTime(lastFrame.TotalSeconds);
 }
 static double Smooth(double value) {value=Math.Clamp(value,0,1);return value*value*(3-2*value);}
 static double Envelope(double time,double start,double duration,double blend=.9)
  =>Smooth((time-start)/blend)*Smooth((start+duration-time)/blend);
 static Point Lerp(Point a,Point b,double t)=>a+(b-a)*t;
 static Point Rotate(Point point,Point pivot,double degrees)
 {
  double a=degrees*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);var v=point-pivot;
  return pivot+new Vector(v.X*c-v.Y*s,v.X*s+v.Y*c);
 }
 internal static (Point elbow,Point hand) ArmJoints(Point shoulder,Point target,double upper,double lower,int bend)
 {
  Vector direction=target-shoulder;double distance=Math.Clamp(direction.Length,Math.Abs(upper-lower)+.01,upper+lower-.01);
  if(direction.Length<.001) direction=new Vector(0,1);direction.Normalize();
  double along=(upper*upper-lower*lower+distance*distance)/(2*distance),across=Math.Sqrt(Math.Max(0,upper*upper-along*along));
  Point center=shoulder+direction*along;Vector offset=new(-direction.Y*across,direction.X*across);var a=center+offset;var b=center-offset;
  // Keep one anatomical bend branch throughout the entire gesture. Choosing
  // by screen-space elbow X flips the arm when its target crosses the shoulder.
  return (bend<0?b:a,shoulder+direction*distance);
 }
 internal readonly record struct ArmFrame(Point Shoulder,Point Elbow,Point Hand);
 static Point ShoulderPoint(Point p) {
  double weight=Smooth((p.Y-260)/35)*(1-Smooth((p.Y-350)/55));
  return new Point(627+(p.X-627)*(1-.12*weight),p.Y);
 }
 internal static (ArmFrame left,ArmFrame right) PoseArms(double seconds) {
  double t=seconds%64,breath=Math.Sin(seconds*2*Math.PI/4.4),scratch=Envelope(t,17,7,1.5),pose=Envelope(t,33,9,1.3);
  double shift=Math.Sin(seconds*.63)*2.6+pose*5,lift=breath*1.8;
  var ls=ShoulderPoint(new Point(516,325))+new Vector(shift,lift);
  var rs=ShoulderPoint(new Point(734,325))+new Vector(shift,lift);
  var lh=Lerp(ls+new Vector(-78+Math.Sin(seconds*.8)*1.5,320),new Point(510+shift+Math.Sin(seconds*10)*3,177+lift),scratch);
  // Lift outward around the shoulder, not through its unreachable center.
  lh.X-=180*Math.Sin(scratch*Math.PI);
  var rh=Lerp(rs+new Vector(78+Math.Sin(seconds*.8+1)*1.5,320),new Point(734+shift,541+lift),pose);
  var left=ArmJoints(ls,lh,145,186,-1);var right=ArmJoints(rs,rh,145,186,-1);
  return (new(ls,left.elbow,left.hand),new(rs,right.elbow,right.hand));
 }
 void OrderLayers(bool leftFront,bool rightFront) {
  int mask=(leftFront?1:0)|(rightFront?2:0);if(mask==foregroundMask) return;foregroundMask=mask;
  group.Children.Clear();group.Children.Add(new AmbientLight(Colors.White));
  foreach(var layer in new[]{Layer.Tail,Layer.LeftCoat,Layer.RightCoat}) group.Children.Add(models[layer]);
  if(!leftFront) group.Children.Add(models[Layer.LeftArm]);
  if(!rightFront) group.Children.Add(models[Layer.RightArm]);
  group.Children.Add(models[Layer.Body]);
  // Alpha-zero foreground texels must be drawn AFTER the body. If drawn
  // first, their depth can reject the head/body and leave a transparent hole.
  if(leftFront) group.Children.Add(models[Layer.LeftArm]);
  if(rightFront) group.Children.Add(models[Layer.RightArm]);
  group.Children.Add(models[Layer.LeftSleeve]);group.Children.Add(models[Layer.RightSleeve]);
 }
 static Point SkinArm(Point source,bool left,Point shoulder,Point elbow,Point hand)
 {
  double sourceShoulder=left?516:734,sign=left?-1:1,u=(source.X-sourceShoulder)*sign,v=(source.Y-325)*sign;
  double sourceUpper=left?136:141,sourceLower=left?300:295;
  Vector upper=elbow-shoulder,lower=hand-elbow;double upperLength=upper.Length,lowerLength=lower.Length;upper.Normalize();lower.Normalize();
  const double span=16;Point center;Vector tangent;
  if(u<sourceUpper-span) {center=shoulder+upper*(u/sourceUpper*upperLength);tangent=upper;}
  else if(u>sourceUpper+span) {center=elbow+lower*((u-sourceUpper)/sourceLower*lowerLength);tangent=lower;}
  else
  {
   double t=(u-sourceUpper+span)/(2*span);Point a=elbow-upper*(span/sourceUpper*upperLength),b=elbow,c=elbow+lower*(span/sourceLower*lowerLength);
   center=new Point((1-t)*(1-t)*a.X+2*(1-t)*t*b.X+t*t*c.X,(1-t)*(1-t)*a.Y+2*(1-t)*t*b.Y+t*t*c.Y);
   tangent=(b-a)*(1-t)+(c-b)*t;tangent.Normalize();
  }
  return center+new Vector(-tangent.Y,tangent.X)*v;
 }
 internal void SetTime(double seconds,bool referencePose=false)
 {
  double t=seconds%64,breath=Math.Sin(seconds*2*Math.PI/4.4),scratch=Envelope(t,17,7,1.5),pose=Envelope(t,33,9,1.3);
  double look=Envelope(t,7,5)-Envelope(t,49,6),shift=Math.Sin(seconds*.63)*2.6+pose*5,lift=breath*1.8;
  var (left,right)=PoseArms(seconds);
  OrderLayers(!referencePose && scratch>.05,!referencePose && pose>.05);
  double blink=Envelope(seconds%4.9,3.9,.23,.085);
  foreach(var layer in meshes.Keys)
  {
   var positions=new Point3DCollection(sources[layer].Length);
   foreach(Point original in sources[layer])
   {
    Point p=original;double z=0;
    if(layer is Layer.LeftSleeve or Layer.RightSleeve) {
     bool isLeft=layer==Layer.LeftSleeve;var arm=isLeft?left:right;
     Vector forward=arm.Elbow-arm.Shoulder;forward.Normalize();Vector normal=new(-forward.Y,forward.X);
     p=arm.Shoulder+forward*(18+original.Y*115)+normal*((original.X-.5)*68+Math.Sin(original.Y*Math.PI)*Math.Sin(seconds*1.5)*2);
     positions.Add(new Point3D(p.X,-p.Y,8));continue;
    }
    // The separately generated garment master has larger transparent margins
    // and a different scale; register it to the T-pose's measured shoulders.
    if(layer is Layer.LeftCoat or Layer.RightCoat) p=new Point(260+original.X*.575,77+original.Y*.575);
    if(!referencePose)
    {
     if(layer is Layer.LeftArm or Layer.RightArm)
     {
      bool isLeft=layer==Layer.LeftArm;
      p=isLeft?SkinArm(p,true,left.Shoulder,left.Elbow,left.Hand):SkinArm(p,false,right.Shoulder,right.Elbow,right.Hand);
      double along=(original.X-(isLeft?516:734))*(isLeft?-1:1);
      p=Lerp(ShoulderPoint(original)+new Vector(shift,lift),p,Smooth(along/24));
      z=isLeft?(scratch>.05?6:-3):(pose>.05?5:-3);
     }
     else
     {
      p=ShoulderPoint(p);p.X+=shift*(1-Smooth((p.Y-610)/580));p.Y+=lift*(1-Smooth((p.Y-430)/190));
      if(layer is Layer.LeftCoat or Layer.RightCoat)
      {
       double weight=Smooth((p.Y-365)/240);
       p.X+=Math.Sin(seconds*1.2+(layer==Layer.LeftCoat?0:.8))*4*weight;p.Y+=Math.Sin(seconds*1.2-.4)*1.6*weight;z=-4;
      }
      if(layer==Layer.Tail) {
       var moved=Rotate(p,new Point(755+shift,615),Math.Sin(seconds*1.05)*3.2);
       p=Lerp(p,moved,Smooth((original.X-755)/35)*Smooth((original.Y-615)/45));z=-6;
      }
      if(layer==Layer.Body && p.Y<281)
      {
       double weight=1-Smooth((p.Y-245)/36);var pivot=new Point(627+shift,268+lift);
       var head=new Point(pivot.X+(p.X-pivot.X)*(.94-Math.Abs(look)*.025),pivot.Y+(p.Y-pivot.Y)*.94);
       head=Rotate(head,pivot,Math.Sin(seconds*.7)*.5+look*3.5-scratch*3);head.X+=look*4;p=Lerp(p,head,weight);
       double locks=Smooth((Math.Abs(original.X-627)-65)/35)*Smooth((original.Y-135)/50);
       p.X+=Math.Sin(seconds*1.7+original.X*.01)*1.6*locks;
      }
      if(layer==Layer.Body && original.Y>161 && original.Y<213)
      {
       double eyes=Math.Max(Math.Exp(-Math.Pow((original.X-594)/24,4)),Math.Exp(-Math.Pow((original.X-656)/24,4)));
       double edge=1-Smooth((Math.Abs(original.Y-187)-7)/19);p.Y-=(original.Y-187)*blink*.92*eyes*edge;
      }
     }
    }
    positions.Add(new Point3D(p.X,-p.Y,z));
   }
   positions.Freeze();meshes[layer].Positions=positions;
  }
  camera.Width=referencePose?1330:870;
 }
}
