#!/system/bin/sh
# Shared by customize.sh (install) and repatch.sh (WebUI / action button).
# Expects: MODPATH = directory that will hold system/framework/services.jar.

CFG=/data/adb/smalipatcher
LIVE=/data/adb/modules/smalipatcher_reborn
ALL_PATCHES="mock-hide mock-permission secure-flag high-volume gnss-off overlay-any"
DEFAULT_CONF="mock-hide=1
mock-permission=1
secure-flag=0
high-volume=0
gnss-off=0
overlay-any=0"

say() { if command -v ui_print >/dev/null 2>&1; then ui_print "$1"; else echo "$1"; fi; }

ensure_conf() {
  mkdir -p "$CFG"
  [ -f "$CFG/patches.conf" ] || printf '%s\n' "$DEFAULT_CONF" > "$CFG/patches.conf"
}

# The patches that exist on this Android version, asked from the engine's own catalog (the single source of truth).
# Needs $MODPATH/bin/engine.jar. If the engine cannot be started, nothing is filtered here and the engine still skips what does not apply.
applicable_patches() {
  app_process -Djava.class.path="$MODPATH/bin/engine.jar" /system/bin reborn.Main list --api "$(getprop ro.build.version.sdk)" 2>/dev/null
}

selected_patches() {
  local out="" p v ok
  ok=" $(echo $(applicable_patches)) "
  [ "$ok" = "  " ] && ok=" $ALL_PATCHES "
  for p in $ALL_PATCHES; do
    v=$(sed -n "s/^$p=//p" "$CFG/patches.conf" 2>/dev/null | tail -n1)
    [ "$v" = 1 ] || continue
    case "$ok" in *" $p "*) out="${out:+$out,}$p" ;; esac
  done
  echo "$out"
}

# Says which enabled patches were left out (called after selected_patches, which must stay silent: it runs inside $(...)).
report_skipped() {
  local sel=",$1," p v
  for p in $ALL_PATCHES; do
    v=$(sed -n "s/^$p=//p" "$CFG/patches.conf" 2>/dev/null | tail -n1)
    [ "$v" = 1 ] || continue
    case "$sel" in *",$p,"*) ;; *) say "- Skipping $p: it does not exist on this Android version" ;; esac
  done
}

# Finds the ORIGINAL services.jar. A previously installed copy of this module is mounted over /system,
# so prefer the root manager's untouched mirror of the system partition.
find_source() {
  SRC=""
  local tmp c
  tmp=$(magisk --path 2>/dev/null)
  for c in "$tmp/.magisk/mirror/system/framework/services.jar" \
           "$tmp/.magisk/mirror/system_root/system/framework/services.jar" \
           "/debug_ramdisk/.magisk/mirror/system/framework/services.jar" \
           /system/framework/services.jar; do
    [ -f "$c" ] && { SRC="$c"; break; }
  done
  [ -n "$SRC" ] || return 1
  if [ "$SRC" = /system/framework/services.jar ] && [ -f "$LIVE/system/framework/services.jar" ] && [ ! -f "$LIVE/disable" ] \
     && cmp -s "$LIVE/system/framework/services.jar" "$SRC"; then
    say "! Original services.jar is hidden behind the active module (no mirror available)."
    say "! Disable the module, reboot, then patch again."
    return 2
  fi
  return 0
}


# Placeholders for stale AOT artifacts, plus the files the boot-time guards rely on.
finish_module() {
  local patches="$1" out="$MODPATH/system/framework/services.jar" fp f
  fp=$(getprop ro.build.fingerprint)
  # stale ahead-of-time artifacts of the original jar must not be used with the patched one
  for f in /system/framework/services.jar.prof /system/framework/services.jar.bprof            /system/framework/services.jar.fsv_meta            /system/framework/oat/*/services.odex /system/framework/oat/*/services.vdex /system/framework/oat/*/services.art            /system/framework/oat/*/services.odex.fsv_meta /system/framework/oat/*/services.vdex.fsv_meta; do
    [ -f "$f" ] || continue
    mkdir -p "$MODPATH$(dirname "$f")"
    : > "$MODPATH$f"
  done
  echo "$fp" > "$MODPATH/fingerprint"
  echo "$patches" > "$MODPATH/patches.applied"
  sha256sum "$out" | cut -d' ' -f1 > "$MODPATH/patched.sha256"
  mkdir -p "$CFG"
  echo 0 > "$CFG/boot_count"
  return 0
}

# run_patch <csv patches>  -> writes $MODPATH/system/framework/services.jar
run_patch() {
  local patches="$1" api fp out log rc
  api=$(getprop ro.build.version.sdk)
  fp=$(getprop ro.build.fingerprint)
  out="$MODPATH/system/framework/services.jar"
  log="$CFG/last-patch.log"
  mkdir -p "$MODPATH/system/framework"
  say "- Source: $SRC"
  say "- Patching (this can take a minute)..."
  app_process -Djava.class.path="$MODPATH/bin/engine.jar" /system/bin reborn.Main patch \
      --in "$SRC" --out "$out.new" --api "$api" --patches "$patches" > "$log" 2>&1
  rc=$?
  tail -n 3 "$log" | while read -r l; do say "  $l"; done
  if [ $rc -ne 0 ] || [ ! -s "$out.new" ]; then
    rm -f "$out.new"
    say "! Patch failed. Full log: $log"
    grep ERROR "$log" | head -n 5 | while read -r l; do say "  $l"; done
    return 1
  fi
  mv -f "$out.new" "$out"

  finish_module "$patches" || return 1
  return 0
}
