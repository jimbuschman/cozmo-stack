// Exports every function's Ghidra decompilation to one file per function, plus an index.
// Output: <outdir>/<entry without its last 4 hex digits>/<entry>.c and <outdir>/index.tsv
// The decompilation is a navigation aid, never evidence: citations point at the instructions.
//@category Export
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileOptions;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionManager;

import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;

public class ExportDecomp extends GhidraScript {

    private static final int TIMEOUT_SECONDS = 120;
    private static final int MAX_LISTED = 40;

    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        File out = new File(args.length > 0 ? args[0] : "decomp");
        out.mkdirs();

        DecompInterface di = new DecompInterface();
        di.setOptions(new DecompileOptions());
        di.toggleCCode(true);
        di.toggleSyntaxTree(false);
        di.setSimplificationStyle("decompile");
        if (!di.openProgram(currentProgram)) {
            throw new Exception("the decompiler did not open the program: " + di.getLastMessage());
        }

        FunctionManager fm = currentProgram.getFunctionManager();
        int total = fm.getFunctionCount();
        int done = 0, ok = 0, failed = 0;
        String header = "// " + currentProgram.getName() + ", Ghidra " + getGhidraVersion()
                + " decompilation. A navigation aid, NOT evidence: cite the instructions at these addresses.";

        try (PrintWriter index = writer(new File(out, "index.tsv"))) {
            index.println("entry\tbytes\tstatus\tfile\tname");
            for (Function f : fm.getFunctions(true)) {
                if (monitor.isCancelled()) break;
                done++;
                if (f.isExternal()) continue;

                String entry = f.getEntryPoint().toString();
                String bucket = entry.length() > 4 ? entry.substring(0, entry.length() - 4) : "0";
                String rel = bucket + "/" + entry + ".c";
                long bytes = f.getBody().getNumAddresses();

                String status;
                String code;
                if (f.isThunk()) {
                    Function t = f.getThunkedFunction(true);
                    status = "THUNK";
                    code = "// thunk to " + (t == null ? "?" : t.getName(true) + " @ " + t.getEntryPoint()) + "\n";
                } else {
                    DecompileResults r = di.decompileFunction(f, TIMEOUT_SECONDS, monitor);
                    if (r != null && r.decompileCompleted() && r.getDecompiledFunction() != null) {
                        status = "OK";
                        code = r.getDecompiledFunction().getC();
                        ok++;
                    } else {
                        status = (r != null && r.isTimedOut()) ? "TIMEOUT" : "FAILED";
                        code = "// decompile " + status + ": " + (r == null ? "" : r.getErrorMessage()) + "\n";
                        failed++;
                    }
                }

                File dir = new File(out, bucket);
                dir.mkdirs();
                try (PrintWriter w = writer(new File(dir, entry + ".c"))) {
                    w.println(header);
                    w.println("// " + f.getName(true));
                    w.println("// entry 0x" + entry + "  body " + f.getBody().getMinAddress() + ".."
                            + f.getBody().getMaxAddress() + "  (" + bytes + " bytes)");
                    w.println("// callers: " + names(f.getCallingFunctions(monitor)));
                    w.println("// callees: " + names(f.getCalledFunctions(monitor)));
                    w.println();
                    w.print(code);
                }
                index.println(entry + "\t" + bytes + "\t" + status + "\t" + rel + "\t" + f.getName(true).replace('\t', ' '));

                if (done % 1000 == 0) {
                    println("decompiled " + done + "/" + total + " (ok " + ok + ", failed " + failed + ")");
                    index.flush();
                }
            }
        } finally {
            di.dispose();
        }
        println("done: " + done + " functions, ok " + ok + ", failed or timed out " + failed + ", output " + out);
    }

    private static PrintWriter writer(File f) throws Exception {
        return new PrintWriter(new OutputStreamWriter(new FileOutputStream(f), StandardCharsets.UTF_8));
    }

    private static String names(Set<Function> fs) {
        List<String> l = new ArrayList<>();
        for (Function g : fs) {
            if (l.size() >= MAX_LISTED) { l.add("... (" + fs.size() + " in all)"); break; }
            l.add(g.getName(true) + "@" + g.getEntryPoint());
        }
        return l.isEmpty() ? "(none)" : String.join(", ", l);
    }
}
