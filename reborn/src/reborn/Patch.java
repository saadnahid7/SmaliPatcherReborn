package reborn;

import java.util.List;

/** A named group of steps with an API window. */
public final class Patch {
    public final String id;
    public final String description;
    public final int minApi;
    public final int maxApi;
    public final List<Step> steps;
    /** Step names of which at least one must match, or the build fails. */
    public final List<String> requireAny;

    public Patch(String id, String description, int minApi, int maxApi, List<String> requireAny, List<Step> steps) {
        this.id = id;
        this.description = description;
        this.minApi = minApi;
        this.maxApi = maxApi;
        this.steps = steps;
        this.requireAny = requireAny;
    }

    public boolean appliesTo(int api) {
        return api >= minApi && api <= maxApi;
    }

    /**
     * One action restricted to a list of candidate classes (exact type descriptors).
     * Several candidates exist because AOSP moved code between packages across releases.
     */
    public static final class Step {
        public final String name;
        public final List<String> classes;
        public final Action action;
        /** If true and the step matches nothing, the build fails. */
        public final boolean required;
        public int hits;

        public Step(String name, List<String> classes, Action action, boolean required) {
            this.name = name;
            this.classes = classes;
            this.action = action;
            this.required = required;
        }
    }
}
