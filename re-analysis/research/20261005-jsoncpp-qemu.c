/* Research-only, freestanding ARM Linux loader for shipped Android libc++.
 * No host floating-point parser. ELF relocations and minimal numeric imports.
 * All addresses relocate to BASE; CPU/FPSCR and successful allocator are fixtures.
 */
typedef unsigned int U; typedef unsigned char B; typedef unsigned long long Q;
#define BASE 0x10000000u
#define HEAP 0x30000000u
static B file[1048576]; static U filelen,brk=HEAP,err=77,guard=0x12345678;
static B ctype[257]; static unsigned short lower[257];
static B *ctypeptr=ctype; static unsigned short *lowerptr=lower;
static U syscall6(U nr,U a,U b,U c,U d,U e,U f) {
 register U r0 __asm__("r0")=a,r1 __asm__("r1")=b,r2 __asm__("r2")=c;
 register U r3 __asm__("r3")=d,r4 __asm__("r4")=e,r5 __asm__("r5")=f,r7 __asm__("r7")=nr;
 __asm__ volatile("svc 0":"+r"(r0):"r"(r1),"r"(r2),"r"(r3),"r"(r4),"r"(r5),"r"(r7):"memory");return r0;
}
static U len(const char *s){U n=0;while(s[n])n++;return n;}
static void out(const char*s){syscall6(4,1,(U)s,len(s),0,0,0);}
static void quit(U status){syscall6(1,status,0,0,0,0,0);for(;;);}
static void unknown(void){out("UNKNOWN_IMPORT\n");quit(91);}
static int eq(const char*a,const char*b){while(*a&&*a==*b){a++;b++;}return *a==*b;}
static void *copy(void*d,const void*s,U n){B*x=d;const B*y=s;for(U i=0;i<n;i++)x[i]=y[i];return d;}
static void *move(void*d,const void*s,U n){B*x=d;const B*y=s;if(x<=y)return copy(d,s,n);while(n){n--;x[n]=y[n];}return d;}
static void *set(void*d,U v,U n){B*x=d;for(U i=0;i<n;i++)x[i]=v;return d;}
static void clear(void*d,U n){set(d,0,n);}
static void aeabiset(void*d,U n,U v){set(d,v,n);}
static U *errnoaddr(void){return &err;}
static void *allocate(U n){U a=brk;brk+=(n+15)&~15u;if(!n)brk+=16;if(brk>=HEAP+0x1000000)quit(92);return (void*)a;}
static void *callocstub(U n,U size){U count=n*size;void*p=allocate(count);set(p,0,count);return p;}
static U noop(void){return 0;}
static U lowercase(U x){return x>=65&&x<=90?x+32:x;}
static int casecmp(const B*a,const B*b,U n){for(U i=0;i<n;i++){int d=lowercase(a[i])-lowercase(b[i]);if(d||!a[i]||!b[i])return d;}return 0;}
static U resolve(const char*n){
 if(eq(n,"__errno"))return (U)errnoaddr;
 if(eq(n,"__stack_chk_guard"))return (U)&guard;
 if(eq(n,"_ctype_"))return (U)&ctypeptr;
 if(eq(n,"_tolower_tab_"))return (U)&lowerptr;
 if(eq(n,"malloc")||eq(n,"_Znwj")||eq(n,"_Znaj"))return (U)allocate;
 if(eq(n,"calloc"))return (U)callocstub;
 if(eq(n,"memcpy")||eq(n,"__aeabi_memcpy")||eq(n,"__aeabi_memcpy4")||eq(n,"__aeabi_memcpy8"))return (U)copy;
 if(eq(n,"memmove"))return (U)move;
 if(eq(n,"memset"))return (U)set;
 if(eq(n,"__aeabi_memclr")||eq(n,"__aeabi_memclr4")||eq(n,"__aeabi_memclr8"))return (U)clear;
 if(eq(n,"__aeabi_memset")||eq(n,"__aeabi_memset4")||eq(n,"__aeabi_memset8"))return (U)aeabiset;
 if(eq(n,"strncasecmp"))return (U)casecmp;
 if(eq(n,"free")||eq(n,"_ZdlPv")||eq(n,"_ZdaPv")||eq(n,"pthread_mutex_lock")||eq(n,"pthread_mutex_unlock"))return (U)noop;
 return (U)unknown;
}
static void initialize(void){
 U*header=(U*)file; U phoff=header[7],shoff=header[8];
 unsigned short*half=(unsigned short*)file;U phsize=half[21],phcount=half[22],shsize=half[23],shcount=half[24];
 set((void*)BASE,0,0x100000);
 for(U i=0;i<phcount;i++){U*p=(U*)(file+phoff+i*phsize);if(p[0]==1)copy((void*)(BASE+p[2]),file+p[1],p[4]);}
 for(U i=0;i<shcount;i++){
  U*s=(U*)(file+shoff+i*shsize);if(s[1]!=9)continue;
  U*symsection=(U*)(file+shoff+s[6]*shsize);U*strsection=(U*)(file+shoff+symsection[6]*shsize);
  U*symbols=(U*)(file+symsection[4]);const char*strings=(const char*)(file+strsection[4]);
  for(U j=0;j<s[5];j+=8){U*r=(U*)(file+s[4]+j),type=r[1]&255,index=r[1]>>8;U*dest=(U*)(BASE+r[0]);
   if(type==23){*dest+=BASE;continue;}
   if(type!=2&&type!=21&&type!=22){out("UNKNOWN_RELOCATION\n");quit(93);}
   U*symbol=symbols+index*4;U value=symbol[1]?BASE+symbol[1]:resolve(strings+symbol[0]);
   *dest=value+(type==2?*dest:0);
  }
 }
 /* __cloc is unused by this converter; same explicit fixture boundary. */
 *(U*)(BASE+0x473a8)=0x47702000;
 brk=HEAP;err=77;
 __asm__ volatile("vmsr fpscr,%0"::"r"(0):"memory");
}
static void hexword(U v){char s[9];for(U i=0;i<8;i++)s[i]="0123456789ABCDEF"[(v>>(28-i*4))&15];s[8]=0;out(s);}
static void decimal(U v){char b[11];U n=0;do{b[n++]=(v%10)+48;v/=10;}while(v);while(n){char x[2]={b[--n],0};out(x);}}
int mainstart(U*stack){
 if(stack[0]!=3){out("usage: harness libc++_shared.so input\n");return 90;}
 const char*path=(const char*)stack[2];const char*input=(const char*)stack[3];
 U fd=syscall6(5,(U)path,0,0,0,0,0);if((int)fd<0)return 94;
 while(filelen<sizeof(file)){U n=syscall6(3,fd,(U)(file+filelen),sizeof(file)-filelen,0,0,0);if((int)n<0)return 95;if(!n)break;filelen+=n;}syscall6(6,fd,0,0,0,0,0);
 if(filelen<52||file[0]!=127||file[4]!=1||file[5]!=1)return 96;
 if(syscall6(192,BASE,0x100000,7,0x32,~0u,0)!=BASE)return 97;
 if(syscall6(192,HEAP,0x1000000,3,0x32,~0u,0)!=HEAP)return 98;
 for(U i=0;i<256;i++){ctype[i+1]=(i==9||i==10||i==11||i==12||i==13||i==32)?8:0;lower[i+1]=lowercase(i);}
 initialize();const char*end=0;
 Q result=((Q(*)(const char*,const char**,void*))(BASE+0x7e571))(input,&end,0);
 union {Q q;U w[2];} raw={.q=result};U rawerr=err,offset=end-input;
 initialize();U state=0;
 result=((Q(*)(const char*,const char*,U*))(BASE+0x5eb99))(input,input+len(input),&state);
 union {Q q;U w[2];} wrapper={.q=result};
 out("{\"raw_bits\":\"");hexword(raw.w[1]);hexword(raw.w[0]);out("\",\"errno\":");decimal(rawerr);out(",\"end_offset\":");decimal(offset);
 out(",\"wrapper_bits\":\"");hexword(wrapper.w[1]);hexword(wrapper.w[0]);out("\",\"wrapper_errno\":");decimal(err);out(",\"state\":");decimal(state);out("}\n");return 0;
}
__attribute__((naked)) void _start(void){__asm__ volatile("mov r0,sp\nbl mainstart\nmov r7,#1\nsvc #0");}
