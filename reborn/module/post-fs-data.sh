#!/system/bin/sh
# Runs before the module is mounted: boot-loop guard, ROM-update guard, ART cache cleanup.
MODDIR=${0%/*}
CFG=/data/adb/smalipatcher
mkdir -p "$CFG"

disable_now() {
  # Magisk mounts whatever is in $MODDIR/system after this script, so moving it away takes effect this boot.
  mv "$MODDIR/system" "$MODDIR/system.disabled" 2>/dev/null
  touch "$MODDIR/disable"
  echo "$(date) $1" >> "$CFG/guard.log"
}

# 0. integrity guard: never mount a jar that is missing, empty or not the one that was built (an install cut short by
#    a power loss can leave a 0-byte file; mounted, it would make system_server fail to start, and a crash loop never
#    reboots the phone, so the boot counter below cannot catch it)
want=$(cat "$MODDIR/patched.sha256" 2>/dev/null)
have=$(sha256sum "$MODDIR/system/framework/services.jar" 2>/dev/null | cut -d' ' -f1)
if [ -d "$MODDIR/system" ] && { [ -z "$want" ] || [ "$want" != "$have" ]; }; then
  disable_now "patched services.jar is missing or damaged - module disabled, re-install it"
  exit 0
fi

# 1. boot-loop guard: service.sh resets the counter once the system has fully booted
n=$(cat "$CFG/boot_count" 2>/dev/null); n=${n:-0}; n=$((n + 1))
echo "$n" > "$CFG/boot_count"
if [ "$n" -ge 3 ]; then
  echo 0 > "$CFG/boot_count"
  disable_now "boot did not complete $((n - 1)) times in a row - module disabled"
  exit 0
fi

# 2. ROM update guard: patched jar only matches the build it was made on
cur=$(getprop ro.build.fingerprint)
if [ -f "$MODDIR/fingerprint" ] && [ "$cur" != "$(cat "$MODDIR/fingerprint")" ]; then
  disable_now "build fingerprint changed, re-install the module to re-patch"
  exit 0
fi

# 3. after (re)install, drop compiled code of the old services.jar once
if [ "$(cat "$MODDIR/patched.sha256" 2>/dev/null)" != "$(cat "$CFG/applied.sha256" 2>/dev/null)" ]; then
  for f in /data/dalvik-cache/*/*services.jar* /data/misc/apexdata/com.android.art/dalvik-cache/*/*services.jar*; do
    [ -e "$f" ] && rm -f "$f"
  done
  cp "$MODDIR/patched.sha256" "$CFG/applied.sha256" 2>/dev/null
fi
