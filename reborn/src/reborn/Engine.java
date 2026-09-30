package reborn;

import com.android.tools.smali.dexlib2.DexFileFactory;
import com.android.tools.smali.dexlib2.Opcodes;
import com.android.tools.smali.dexlib2.iface.ClassDef;
import com.android.tools.smali.dexlib2.iface.DexFile;
import com.android.tools.smali.dexlib2.iface.Method;
import com.android.tools.smali.dexlib2.iface.MethodImplementation;
import com.android.tools.smali.dexlib2.immutable.ImmutableClassDef;
import com.android.tools.smali.dexlib2.immutable.ImmutableMethod;
import com.android.tools.smali.dexlib2.dexbacked.DexBackedDexFile;
import com.android.tools.smali.dexlib2.writer.pool.DexPool;
import com.android.tools.smali.dexlib2.writer.io.MemoryDataStore;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.Enumeration;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.regex.Pattern;
import java.util.zip.CRC32;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;
import java.util.zip.ZipOutputStream;

/** Applies patches to the dex files inside a jar and writes a new jar; every other entry is copied untouched. */
public final class Engine {
    private static final Pattern DEX = Pattern.compile("classes\\d*\\.dex");

    public static final class Result {
        public final Map<String, Integer> hits = new LinkedHashMap<>();
        public final List<String> log = new ArrayList<>();
        public final List<String> errors = new ArrayList<>();
        public final List<String> modifiedDex = new ArrayList<>();
        public boolean ok() { return errors.isEmpty(); }
    }

    private Engine() {}

    public static Result patch(File inJar, File outJar, int api, List<Patch> patches) throws IOException {
        Result res = new Result();
        Opcodes opcodes = Opcodes.forApi(Math.min(api, 36));
        Map<String, byte[]> newDex = new LinkedHashMap<>();

        Set<String> candidates = new HashSet<>();
        List<Patch> active = new ArrayList<>();
        for (Patch p : patches) {
            if (!p.appliesTo(api)) {
                res.log.add("skip " + p.id + " (not for API " + api + ")");
                continue;
            }
            active.add(p);
            for (Patch.Step s : p.steps) candidates.addAll(s.classes);
        }

        try (ZipFile zip = new ZipFile(inJar)) {
            List<String> dexNames = new ArrayList<>();
            for (Enumeration<? extends ZipEntry> e = zip.entries(); e.hasMoreElements(); ) {
                String n = e.nextElement().getName();
                if (DEX.matcher(n).matches()) dexNames.add(n);
            }
            if (dexNames.isEmpty()) {
                res.errors.add("no classes*.dex in " + inJar.getName() + " (stripped jar? needs the odex/vdex path)");
                return res;
            }

            for (String name : dexNames) {
                DexFile dex = DexFileFactory.loadDexEntry(inJar, name, true, opcodes).getDexFile();
                boolean has = false;
                for (ClassDef c : dex.getClasses()) {
                    if (candidates.contains(c.getType())) { has = true; break; }
                }
                if (!has) continue;

                List<ClassDef> outClasses = new ArrayList<>();
                boolean changed = false;
                for (ClassDef c : dex.getClasses()) {
                    if (!candidates.contains(c.getType())) { outClasses.add(c); continue; }
                    ClassDef patched = patchClass(c, active, res);
                    if (patched != null) { outClasses.add(patched); changed = true; } else outClasses.add(c);
                }
                if (!changed) continue;

                DexPool pool = new DexPool(Opcodes.forApi(Math.min(api, 34)));
                for (ClassDef c : outClasses) pool.internClass(c);
                MemoryDataStore store = new MemoryDataStore();
                pool.writeTo(store);
                byte[] bytes = java.util.Arrays.copyOf(store.getBuffer(), store.getSize());
                newDex.put(name, bytes);
                res.modifiedDex.add(name);
            }

            for (Patch p : active) {
                int required = 0;
                for (Patch.Step s : p.steps) {
                    res.hits.merge(p.id + "/" + s.name, s.hits, Integer::sum);
                    if (p.requireAny.contains(s.name)) required += s.hits;
                    if (s.hits == 0) res.log.add("no match: " + p.id + "/" + s.name);
                }
                if (!p.requireAny.isEmpty() && required == 0) {
                    res.errors.add(p.id + ": none of " + p.requireAny + " matched on this ROM");
                }
            }
            if (!res.ok()) return res;
            if (newDex.isEmpty()) { res.errors.add("nothing was modified"); return res; }

            writeJar(zip, outJar, newDex);
        }

        // verify by re-reading every method of every class of the result
        verify(outJar, opcodes, res);
        return res;
    }

    private static ClassDef patchClass(ClassDef c, List<Patch> active, Result res) {
        List<Method> direct = new ArrayList<>();
        List<Method> virt = new ArrayList<>();
        boolean changed = false;
        for (Method m : c.getDirectMethods()) { Method n = patchMethod(c, m, active, res); direct.add(n == null ? m : n); changed |= n != null; }
        for (Method m : c.getVirtualMethods()) { Method n = patchMethod(c, m, active, res); virt.add(n == null ? m : n); changed |= n != null; }
        if (!changed) return null;
        return new ImmutableClassDef(c.getType(), c.getAccessFlags(), c.getSuperclass(), c.getInterfaces(),
                c.getSourceFile(), c.getAnnotations(), c.getStaticFields(), c.getInstanceFields(), direct, virt);
    }

    private static Method patchMethod(ClassDef c, Method m, List<Patch> active, Result res) {
        MethodImplementation impl = m.getImplementation();
        if (impl == null) return null;
        MethodImplementation cur = impl;
        boolean changed = false;
        for (Patch p : active) {
            for (Patch.Step s : p.steps) {
                if (!s.classes.contains(c.getType()) || !s.action.matches(m)) continue;
                MethodImplementation r;
                try {
                    r = s.action.apply(m, cur);
                } catch (RuntimeException ex) {
                    res.errors.add(p.id + "/" + s.name + ": " + ex.getMessage());
                    continue;
                }
                if (r != null) {
                    cur = r;
                    changed = true;
                    s.hits++;
                    res.log.add("patched " + p.id + "/" + s.name + " -> " + c.getType() + "->" + m.getName() + "  [" + s.action.describe() + "]");
                }
            }
        }
        if (!changed) return null;
        return new ImmutableMethod(m.getDefiningClass(), m.getName(), m.getParameters(), m.getReturnType(),
                m.getAccessFlags(), m.getAnnotations(), m.getHiddenApiRestrictions(), cur);
    }

    // ---- jar writing -------------------------------------------------------------------------

    private static final class Counting extends java.io.FilterOutputStream {
        long count;
        Counting(java.io.OutputStream o) { super(o); }
        @Override public void write(int b) throws IOException { out.write(b); count++; }
        @Override public void write(byte[] b, int off, int len) throws IOException { out.write(b, off, len); count += len; }
    }

    private static void writeJar(ZipFile in, File out, Map<String, byte[]> replaced) throws IOException {
        try (java.io.FileOutputStream fos = new java.io.FileOutputStream(out);
             Counting cnt = new Counting(new java.io.BufferedOutputStream(fos));
             ZipOutputStream zos = new ZipOutputStream(cnt)) {
            for (Enumeration<? extends ZipEntry> e = in.entries(); e.hasMoreElements(); ) {
                ZipEntry src = e.nextElement();
                byte[] data = replaced.get(src.getName());
                if (data == null) data = readAll(in.getInputStream(src));
                boolean stored = src.getMethod() == ZipEntry.STORED;
                ZipEntry dst = new ZipEntry(src.getName());
                if (src.getTime() != -1) dst.setTime(src.getTime());
                if (stored) {
                    dst.setMethod(ZipEntry.STORED);
                    dst.setSize(data.length);
                    dst.setCompressedSize(data.length);
                    CRC32 crc = new CRC32();
                    crc.update(data);
                    dst.setCrc(crc.getValue());
                    long dataStart = cnt.count + 30 + dst.getName().getBytes("UTF-8").length;
                    int pad = (int) ((4 - (dataStart % 4)) % 4);
                    if (pad != 0) {
                        int len = pad + 4; // extra header is 4 bytes; pad via the payload
                        byte[] extra = new byte[len];
                        extra[0] = (byte) 0x35; extra[1] = (byte) 0xd9; // zipalign's padding field id 0xd935
                        extra[2] = (byte) (len - 4); extra[3] = 0;
                        dst.setExtra(extra);
                    }
                } else {
                    dst.setMethod(ZipEntry.DEFLATED);
                }
                zos.putNextEntry(dst);
                zos.write(data);
                zos.closeEntry();
            }
        }
    }

    private static byte[] readAll(InputStream is) throws IOException {
        try (InputStream in = is) {
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            byte[] buf = new byte[65536];
            int n;
            while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
            return bos.toByteArray();
        }
    }

    // ---- verification ------------------------------------------------------------------------

    private static void verify(File jar, Opcodes opcodes, Result res) throws IOException {
        try (ZipFile zip = new ZipFile(jar)) {
            for (Enumeration<? extends ZipEntry> e = zip.entries(); e.hasMoreElements(); ) {
                String n = e.nextElement().getName();
                if (!DEX.matcher(n).matches()) continue;
                DexFile dex = DexFileFactory.loadDexEntry(jar, n, true, opcodes).getDexFile();
                int classes = 0, methods = 0;
                for (ClassDef c : dex.getClasses()) {
                    classes++;
                    for (Method m : c.getMethods()) {
                        methods++;
                        MethodImplementation i = m.getImplementation();
                        if (i != null) for (Object ins : i.getInstructions()) { /* force decode */ }
                    }
                }
                res.log.add("verified " + n + ": " + classes + " classes, " + methods + " methods");
            }
        } catch (RuntimeException ex) {
            res.errors.add("verification failed: " + ex);
        }
    }
}
