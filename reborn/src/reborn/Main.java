package reborn;

import com.android.tools.smali.dexlib2.DexFileFactory;
import com.android.tools.smali.dexlib2.Opcodes;
import com.android.tools.smali.dexlib2.iface.ClassDef;
import com.android.tools.smali.dexlib2.iface.DexFile;
import com.android.tools.smali.dexlib2.iface.Method;

import java.io.File;
import java.util.Enumeration;
import java.util.HashMap;
import java.util.Map;
import java.util.regex.Pattern;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;

public class Main {
    public static void main(String[] a) throws Exception {
        if (a.length == 0) { usage(); return; }
        Map<String, String> o = new HashMap<>();
        for (int i = 1; i < a.length - 1; i += 2) o.put(a[i], a[i + 1]);
        switch (a[0]) {
            case "patch" -> {
                int api = Integer.parseInt(o.getOrDefault("--api", "36"));
                Engine.Result r = Engine.patch(new File(o.get("--in")), new File(o.get("--out")), api,
                        Catalog.select(o.get("--patches")));
                r.log.forEach(System.out::println);
                System.out.println("hits: " + r.hits);
                System.out.println("modified: " + r.modifiedDex);
                if (!r.ok()) { r.errors.forEach(e -> System.err.println("ERROR: " + e)); System.exit(2); }
                System.out.println("OK");
            }
            case "find" -> find(new File(o.get("--jar")), Pattern.compile(o.get("--re")), Integer.parseInt(o.getOrDefault("--api", "36")));
            case "baksmali" -> Class.forName("com.android.tools.smali.baksmali.Main").getMethod("main", String[].class)
                    .invoke(null, (Object) java.util.Arrays.copyOfRange(a, 1, a.length));
            default -> usage();
        }
    }

    static void usage() {
        System.out.println("""
                smalipatcher-reborn engine
                  patch --in services.jar --out out.jar --api 36 [--patches mock-hide,mock-permission,secure-flag,high-volume,gnss-off,overlay-any]
                  find  --jar services.jar --re <regex> [--api 36]     (matches Lclass;->method)
                  baksmali <baksmali args...>""");
    }

    static void find(File jar, Pattern re, int api) throws Exception {
        Opcodes op = Opcodes.forApi(Math.min(api, 36));
        try (ZipFile z = new ZipFile(jar)) {
            for (Enumeration<? extends ZipEntry> e = z.entries(); e.hasMoreElements(); ) {
                String n = e.nextElement().getName();
                if (!n.matches("classes\\d*\\.dex")) continue;
                DexFile d = DexFileFactory.loadDexEntry(jar, n, true, op).getDexFile();
                for (ClassDef c : d.getClasses()) {
                    if (re.matcher(c.getType()).find()) System.out.println(n + " " + c.getType());
                    for (Method m : c.getMethods()) {
                        String s = c.getType() + "->" + m.getName();
                        if (re.matcher(s).find()) {
                            StringBuilder p = new StringBuilder();
                            for (CharSequence t : m.getParameterTypes()) p.append(t);
                            System.out.println(n + " " + s + "(" + p + ")" + m.getReturnType());
                        }
                    }
                }
            }
        }
    }
}
