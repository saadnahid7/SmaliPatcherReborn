package reborn;

import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** Built-in patch definitions. Class lists are the places AOSP has kept this logic across releases. */
public final class Catalog {
    private Catalog() {}

    @SafeVarargs private static <T> List<T> L(T... a) { return Arrays.asList(a); }
    @SafeVarargs private static <T> Set<T> S(T... a) { return new java.util.HashSet<>(Arrays.asList(a)); }

    private static final String APPOPS_NOTEOP =
            "Lcom/android/server/location/injector/AppOpsHelper;->noteOp(ILandroid/location/util/identity/CallerIdentity;)Z";
    private static final String SET_MOCK =
            "Landroid/location/Location;->setIsFromMockProvider(Z)V";

    public static Map<String, Patch> all() {
        Map<String, Patch> m = new LinkedHashMap<>();

        m.put("mock-hide", new Patch("mock-hide",
                "Locations from mock providers are not flagged as mock (Location.isMock() stays false)", 28, 99,
                L("MockLocationProvider", "LocationManagerService"),
                L(
                        new Patch.Step("MockLocationProvider", L(
                                "Lcom/android/server/location/provider/MockLocationProvider;",
                                "Lcom/android/server/location/MockProvider;",
                                "Lcom/android/server/location/MockLocationProvider;"),
                                Actions.nopInvoke(null, SET_MOCK), false),
                        new Patch.Step("LocationManagerService", L(
                                "Lcom/android/server/location/LocationManagerService;",
                                "Lcom/android/server/LocationManagerService;"),
                                Actions.nopInvoke(null, SET_MOCK), false))));

        m.put("mock-permission", new Patch("mock-permission",
                "Apps can register test providers without being selected as the mock location app", 28, 99,
                L("testProviderAppOp", "canCallerAccessMockLocation", "noteMockLocationAccess"),
                L(
                        new Patch.Step("testProviderAppOp", L(
                                "Lcom/android/server/location/LocationManagerService;"),
                                Actions.forceResult(S("addTestProvider", "removeTestProvider",
                                        "setTestProviderEnabled", "setTestProviderLocation"), APPOPS_NOTEOP, 1), false),
                        new Patch.Step("canCallerAccessMockLocation", L(
                                "Lcom/android/server/LocationManagerService;",
                                "Lcom/android/server/location/LocationManagerService;"),
                                Actions.returnConst("canCallerAccessMockLocation", null, 1), false),
                        new Patch.Step("noteMockLocationAccess", L(
                                "Lcom/android/server/location/AppOpsHelper;"),
                                Actions.returnConst("noteMockLocationAccess", null, 1), false))));

        m.put("secure-flag", new Patch("secure-flag",
                "Screenshots and screen recording allowed in FLAG_SECURE windows", 28, 99,
                L("isSecureLocked", "wmIsSecureLocked", "setSecure"),
                L(
                        new Patch.Step("isSecureLocked", L("Lcom/android/server/wm/WindowState;"),
                                Actions.returnConst("isSecureLocked", "", 0), false),
                        new Patch.Step("wmIsSecureLocked", L("Lcom/android/server/wm/WindowManagerService;"),
                                Actions.returnConst("isSecureLocked", null, 0), false),
                        new Patch.Step("setSecure", L("Lcom/android/server/wm/WindowSurfaceController;"),
                                Actions.returnConst("setSecure", "Z", 0), false),
                        new Patch.Step("dpmAllowed", L("Lcom/android/server/devicepolicy/DevicePolicyCacheImpl;"),
                                Actions.returnConst("isScreenCaptureAllowed", null, 1), false),
                        new Patch.Step("dpmDisabled", L("Lcom/android/server/devicepolicy/DevicePolicyManagerService;"),
                                Actions.returnConst("getScreenCaptureDisabled", null, 0), false))));
        return m;
    }

    public static List<Patch> select(String csv) {
        Map<String, Patch> all = all();
        if (csv == null || csv.trim().isEmpty()) return new java.util.ArrayList<>(all.values());
        List<Patch> out = new java.util.ArrayList<>();
        for (String raw : csv.split(",")) {
            String id = raw.trim();
            Patch p = all.get(id);
            if (p == null) throw new IllegalArgumentException("unknown patch: " + id + " (known: " + all.keySet() + ")");
            out.add(p);
        }
        return out;
    }
}
