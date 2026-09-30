#!/system/bin/sh
# Action button (Magisk 28+ / KernelSU): show status.
MODDIR=${0%/*}
CFG=/data/adb/smalipatcher
echo "Smali Patcher Reborn"
echo "Patches applied : $(cat "$MODDIR/patches.applied" 2>/dev/null)"
echo "Patches wanted  : $(sed -n 's/=1$//p' "$CFG/patches.conf" 2>/dev/null | tr '\n' ' ')"
echo "Build           : $(cat "$MODDIR/fingerprint" 2>/dev/null)"
echo "Current build   : $(getprop ro.build.fingerprint)"
echo "Boot counter    : $(cat "$CFG/boot_count" 2>/dev/null)"
[ -f "$CFG/guard.log" ] && { echo "Guard log:"; tail -n 5 "$CFG/guard.log"; }
echo
echo "Open the module's WebUI to change patches."
