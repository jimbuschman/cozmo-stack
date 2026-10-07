using System;
using System.IO;
float[] original={9.169666f,0f,1.214333f,0.90528f,0f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0f,0f,0f,0f,0f,0f,-10.206374f,0f,1.222037f,0.90528f,0f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0f,0f,0f,0f,0f,0f};
float[] candidate={9.169666f,0f,BitConverter.UInt32BitsToSingle(0x3F9B6F43),BitConverter.UInt32BitsToSingle(0x3F67C075),0f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0f,0f,0f,0f,0f,0f,-10.206374f,0f,1.222037f,BitConverter.UInt32BitsToSingle(0x3F67C075),0f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0.5f,0f,0f,0f,0f,0f,0f};
uint[] expected={0x4112B6F4u,0x00000000u,0x3F9B6F43u,0x3F67C075u,0x00000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x00000000u,0x00000000u,0x00000000u,0x00000000u,0x00000000u,0x00000000u,0xC1234D4Fu,0x00000000u,0x3F9C6BB5u,0x3F67C075u,0x00000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x3F000000u,0x00000000u,0x00000000u,0x00000000u,0x00000000u,0x00000000u,0x00000000u};
using var w=new StreamWriter(args[0]);
w.WriteLine("index,asset,original,candidate");
for(int i=0;i<38;i++){uint a=BitConverter.SingleToUInt32Bits(original[i]);uint c=BitConverter.SingleToUInt32Bits(candidate[i]);w.WriteLine($"{i},{expected[i]:X8},{a:X8},{c:X8}");}
