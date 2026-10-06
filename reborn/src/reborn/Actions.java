package reborn;

import com.android.tools.smali.dexlib2.Opcode;
import com.android.tools.smali.dexlib2.iface.Method;
import com.android.tools.smali.dexlib2.iface.MethodImplementation;
import com.android.tools.smali.dexlib2.iface.instruction.Instruction;
import com.android.tools.smali.dexlib2.iface.instruction.OneRegisterInstruction;
import com.android.tools.smali.dexlib2.iface.instruction.ReferenceInstruction;
import com.android.tools.smali.dexlib2.iface.reference.MethodReference;
import com.android.tools.smali.dexlib2.iface.reference.StringReference;
import com.android.tools.smali.dexlib2.immutable.instruction.ImmutableInstruction21c;
import com.android.tools.smali.dexlib2.immutable.instruction.ImmutableInstruction31c;
import com.android.tools.smali.dexlib2.immutable.reference.ImmutableStringReference;
import com.android.tools.smali.dexlib2.immutable.ImmutableMethodImplementation;
import com.android.tools.smali.dexlib2.immutable.instruction.ImmutableInstruction;
import com.android.tools.smali.dexlib2.immutable.instruction.ImmutableInstruction10x;
import com.android.tools.smali.dexlib2.immutable.instruction.ImmutableInstruction11n;
import com.android.tools.smali.dexlib2.immutable.instruction.ImmutableInstruction11x;
import com.android.tools.smali.dexlib2.util.ReferenceUtil;

import java.util.ArrayList;
import java.util.List;
import java.util.Set;

/** The small set of bytecode edits the patch definitions are built from. */
public final class Actions {
    private Actions() {}

    static int paramRegisters(Method m) {
        int n = (m.getAccessFlags() & 0x8) != 0 ? 0 : 1;
        for (CharSequence p : m.getParameterTypes()) {
            n += (p.charAt(0) == 'J' || p.charAt(0) == 'D') ? 2 : 1;
        }
        return n;
    }

    static boolean paramsMatch(Method m, String paramDesc) {
        if (paramDesc == null) return true;
        StringBuilder sb = new StringBuilder();
        for (CharSequence p : m.getParameterTypes()) sb.append(p);
        return sb.toString().equals(paramDesc);
    }

    static boolean isInvokeOf(Instruction i, String targetDescriptor) {
        return isInvokeMatching(i, d -> d.equals(targetDescriptor));
    }

    static boolean isInvokeMatching(Instruction i, java.util.function.Predicate<String> descriptor) {
        if (!(i instanceof ReferenceInstruction)) return false;
        if (!i.getOpcode().name.startsWith("invoke-")) return false;
        Object ref = ((ReferenceInstruction) i).getReference();
        return ref instanceof MethodReference
                && descriptor.test(ReferenceUtil.getMethodDescriptor((MethodReference) ref));
    }

    static List<Instruction> toList(Iterable<? extends Instruction> it) {
        List<Instruction> l = new ArrayList<>();
        for (Instruction t : it) l.add(t);
        return l;
    }

    /**
     * Replace the whole body of name(params) with "return value".
     * Supports V, Z, B, S, C, I (value must fit a signed nibble) and object returns (null).
     */
    public static Action returnConst(String name, String paramDesc, int value) {
        return new Action() {
            @Override public boolean matches(Method m) {
                return m.getName().equals(name) && paramsMatch(m, paramDesc) && m.getImplementation() != null;
            }

            @Override public MethodImplementation apply(Method m, MethodImplementation impl) {
                char r = m.getReturnType().charAt(0);
                List<ImmutableInstruction> ins = new ArrayList<>();
                int regs = paramRegisters(m);
                if (r == 'V') {
                    ins.add(new ImmutableInstruction10x(Opcode.RETURN_VOID));
                } else if (r == 'J' || r == 'D') {
                    throw new IllegalStateException("wide return not supported: " + m);
                } else {
                    regs += 1;
                    ins.add(new ImmutableInstruction11n(Opcode.CONST_4, 0, value));
                    boolean obj = r == 'L' || r == '[';
                    ins.add(new ImmutableInstruction11x(obj ? Opcode.RETURN_OBJECT : Opcode.RETURN, 0));
                }
                return new ImmutableMethodImplementation(regs, ins, null, null);
            }

            @Override public String describe() {
                return "return " + value + " from " + name + (paramDesc == null ? "(*)" : "(" + paramDesc + ")");
            }
        };
    }

    /** Replace every call to targetDescriptor (a void method) with nops, inside the named methods. */
    public static Action nopInvoke(Set<String> inMethods, String targetDescriptor) {
        return new Action() {
            @Override public boolean matches(Method m) {
                return m.getImplementation() != null && (inMethods == null || inMethods.contains(m.getName()));
            }

            @Override public MethodImplementation apply(Method m, MethodImplementation impl) {
                ImmutableMethodImplementation b = ImmutableMethodImplementation.of(impl);
                List<ImmutableInstruction> out = new ArrayList<>();
                boolean changed = false;
                for (Instruction i : b.getInstructions()) {
                    if (isInvokeOf(i, targetDescriptor)) {
                        for (int k = 0; k < i.getCodeUnits(); k++) out.add(new ImmutableInstruction10x(Opcode.NOP));
                        changed = true;
                    } else {
                        out.add(ImmutableInstruction.of(i));
                    }
                }
                if (!changed) return null;
                return new ImmutableMethodImplementation(b.getRegisterCount(), out, b.getTryBlocks(), b.getDebugItems());
            }

            @Override public String describe() {
                return "nop calls to " + targetDescriptor + (inMethods == null ? "" : " in " + inMethods);
            }
        };
    }

    /** After every call to targetDescriptor, overwrite the move-result register with a constant. */
    public static Action forceResult(Set<String> inMethods, String targetDescriptor, int value) {
        return forceResultMatching(inMethods, d -> d.equals(targetDescriptor), targetDescriptor, value);
    }

    /** Same, for every call whose descriptor matches (a method that changed its parameter types across releases). */
    public static Action forceResultMatching(Set<String> inMethods, java.util.function.Predicate<String> descriptor, String what, int value) {
        final String targetDescriptor = what;
        return new Action() {
            @Override public boolean matches(Method m) {
                return m.getImplementation() != null && (inMethods == null || inMethods.contains(m.getName()));
            }

            @Override public MethodImplementation apply(Method m, MethodImplementation impl) {
                ImmutableMethodImplementation b = ImmutableMethodImplementation.of(impl);
                List<Instruction> src = toList(b.getInstructions());
                List<ImmutableInstruction> out = new ArrayList<>();
                boolean changed = false;
                for (int idx = 0; idx < src.size(); idx++) {
                    Instruction i = src.get(idx);
                    boolean callThenResult = isInvokeMatching(i, descriptor) && idx + 1 < src.size()
                            && src.get(idx + 1).getOpcode() == Opcode.MOVE_RESULT;
                    if (callThenResult) {
                        // The call itself may throw (AppOps SecurityException), so drop it and set the result directly.
                        int reg = ((OneRegisterInstruction) src.get(idx + 1)).getRegisterA();
                        if (reg > 15) throw new IllegalStateException("v" + reg + " too high for const/4 in " + m);
                        for (int k = 0; k < i.getCodeUnits(); k++) out.add(new ImmutableInstruction10x(Opcode.NOP));
                        out.add(new ImmutableInstruction11n(Opcode.CONST_4, reg, value));
                        idx++;
                        changed = true;
                    } else {
                        out.add(ImmutableInstruction.of(i));
                    }
                }
                if (!changed) return null;
                return new ImmutableMethodImplementation(b.getRegisterCount(), out, b.getTryBlocks(), b.getDebugItems());
            }

            @Override public String describe() {
                return "replace call to " + targetDescriptor + " with constant " + value
                        + (inMethods == null ? "" : " in " + inMethods);
            }
        };
    }

    /** Replace the string constant `from` with `to` in every method that loads it. */
    public static Action replaceString(java.util.function.Predicate<String> methodFilter, String from, String to) {
        return new Action() {
            @Override public boolean matches(Method m) {
                return m.getImplementation() != null && (methodFilter == null || methodFilter.test(m.getName()));
            }

            @Override public MethodImplementation apply(Method m, MethodImplementation impl) {
                ImmutableMethodImplementation b = ImmutableMethodImplementation.of(impl);
                List<ImmutableInstruction> out = new ArrayList<>();
                boolean changed = false;
                for (Instruction i : b.getInstructions()) {
                    Opcode op = i.getOpcode();
                    if ((op == Opcode.CONST_STRING || op == Opcode.CONST_STRING_JUMBO)
                            && ((ReferenceInstruction) i).getReference() instanceof StringReference
                            && ((StringReference) ((ReferenceInstruction) i).getReference()).getString().equals(from)) {
                        int reg = ((OneRegisterInstruction) i).getRegisterA();
                        ImmutableStringReference ref = new ImmutableStringReference(to);
                        out.add(op == Opcode.CONST_STRING ? new ImmutableInstruction21c(op, reg, ref)
                                : new ImmutableInstruction31c(op, reg, ref));
                        changed = true;
                    } else {
                        out.add(ImmutableInstruction.of(i));
                    }
                }
                if (!changed) return null;
                return new ImmutableMethodImplementation(b.getRegisterCount(), out, b.getTryBlocks(), b.getDebugItems());
            }

            @Override public String describe() {
                return "replace string \"" + from + "\" with \"" + to + "\""
                        + (methodFilter == null ? "" : " in matching methods");
            }
        };
    }
}
