#!/system/bin/sh
# Re-patch from the current $CFG/patches.conf without reflashing (called by the WebUI / action button).
MODDIR=${0%/*}
MODPATH=$MODDIR
. "$MODDIR/lib.sh"

ensure_conf
PATCHES=$(selected_patches)
[ -n "$PATCHES" ] || { echo "No patch selected"; exit 1; }
echo "Patches: $PATCHES"
find_source || exit 1
[ -d "$MODDIR/system.disabled" ] && rm -rf "$MODDIR/system.disabled"
rm -f "$MODDIR/disable"
run_patch "$PATCHES" || exit 1
echo "Done. Reboot to apply."
