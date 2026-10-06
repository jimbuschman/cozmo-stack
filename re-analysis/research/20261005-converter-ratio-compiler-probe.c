/* Research-only diagnostic: no claim that this is the shipped source. */
typedef union { double d; unsigned int w[2]; } Words;
extern double approx(void *, int *);
extern int limbs(void *);
double branch_ratio(void *a, void *b) {
 Words x,y; int ka,kb; x.d=approx(a,&ka); y.d=approx(b,&kb);
 int k=ka-kb+32*(limbs(a)-limbs(b));
 if(k>0)x.w[1]+=k*0x100000;else y.w[1]+=(-k)*0x100000;
 return x.d/y.d;
}
double selected_pointer_ratio(void *a, void *b) {
 Words x,y; int ka,kb; x.d=approx(a,&ka); y.d=approx(b,&kb);
 int k=ka-kb+32*(limbs(a)-limbs(b));
 unsigned int *p=k>0?x.w:y.w;
 p[1]+=(k>0?k:-k)*0x100000;
 return x.d/y.d;
}
