import sys, re, collections, cpp_demangle
def dem(n):
    try: return cpp_demangle.demangle(n)
    except Exception: return None
out=open(sys.argv[2],"w",encoding="utf-8")
ns=collections.Counter(); cls=collections.Counter(); n=0
for line in open(sys.argv[1],encoding="utf-8"):
    addr,name=line.rstrip("\n").split(" ",1)
    d=dem(name) or name
    out.write(f"{addr} {d}\n"); n+=1
    m=re.match(r"^((?:[A-Za-z_][A-Za-z0-9_]*::)+)",d)
    if m:
        parts=m.group(1).rstrip(':').split('::')
        ns[parts[0]]+=1
        cls['::'.join(parts[:3])]+=1
print("demangled",n)
print("== top namespaces ==")
for k,v in ns.most_common(20): print(f"{v:6d} {k}")
print("== Anki:: classes (top 90) ==")
for k,v in [(k,v) for k,v in cls.most_common(2000) if k.startswith("Anki")][:90]: print(f"{v:6d} {k}")
