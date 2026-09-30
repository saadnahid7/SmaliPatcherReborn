package reborn;

import com.android.tools.smali.dexlib2.iface.Method;
import com.android.tools.smali.dexlib2.iface.MethodImplementation;

/** One low-level edit applied to matching methods. */
public interface Action {
    /** Does this action want to look at the method at all? */
    boolean matches(Method m);

    /** Returns a rewritten implementation, or null if nothing changed. */
    MethodImplementation apply(Method m, MethodImplementation impl);

    /** Short human readable description for reports. */
    String describe();
}
