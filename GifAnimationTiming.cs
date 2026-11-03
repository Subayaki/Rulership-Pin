using System.IO;
namespace RulershipPin;
internal static class GifAnimationTiming
{
 // WPF's GIF encoder emits the frames but ignores their delay metadata.
 // Fix container timing only; preserve every encoded pixel block/palette.
 internal static void Apply(string path,ushort delay)
 {
  byte[] bytes=File.ReadAllBytes(path);
  int start=13;if((bytes[10]&128)!=0) start+=3*(1<<((bytes[10]&7)+1));
  int p=start;
  void SubBlocks() {while(true) {int length=bytes[p++];if(length==0) return;p+=length;}}
  while(p<bytes.Length)
  {
   int block=bytes[p++];if(block==0x3B) break;
   if(block==0x21)
   {
    int label=bytes[p++];
    if(label==0xF9) {bytes[p+1]=(byte)((bytes[p+1]&1)|8);bytes[p+2]=(byte)delay;bytes[p+3]=(byte)(delay>>8);}
    SubBlocks();
   }
   else if(block==0x2C) {int packed=bytes[p+8];p+=9;if((packed&128)!=0) p+=3*(1<<((packed&7)+1));p++;SubBlocks();}
   else throw new InvalidDataException("Unexpected GIF block.");
  }
  using var output=new MemoryStream();output.Write(bytes,0,start);
  output.Write(new byte[]{0x21,0xFF,11,78,69,84,83,67,65,80,69,50,46,48,3,1,0,0,0});
  output.Write(bytes,start,bytes.Length-start);File.WriteAllBytes(path,output.ToArray());
 }
}
