#!/system/bin/sh
# Smali Patcher Reborn - install-time patching on the device itself.
SKIPUNZIP=1

ui_print "*******************************************"
ui_print "          Smali Patcher Reborn             "
ui_print "   on-device patcher - Android 10 to 17    "
ui_print "*******************************************"
ui_print "  by saadnahid7 - droidrooter.com"
ui_print "  Thanks to fOmey (Smali Patcher) and"
ui_print "  sabpprook (SmaliPatcherEx)"
ui_print ""

unzip -o "$ZIPFILE" -x 'META-INF/*' -d "$MODPATH" >&2
. "$MODPATH/lib.sh"

API=$(getprop ro.build.version.sdk)
[ "$API" -ge 29 ] 2>/dev/null || abort "! Android 10+ (API 29+) required, this is API $API"
ui_print "- Device API $API"
ui_print "- $(getprop ro.build.fingerprint)"

if [ -f "$MODPATH/orig.sha256" ]; then
  # ---- module built on a PC from a jar copied off a phone: apply only if this phone has that exact jar ----
  ui_print "- Pre-patched module (made on a PC)"
  find_source || abort "! services.jar not found or hidden by the active module"
  have=$(sha256sum "$SRC" | cut -d' ' -f1)
  want=$(cat "$MODPATH/orig.sha256")
  if [ "$have" != "$want" ]; then
    ui_print "  this phone : $have"
    ui_print "  module for : $want"
    abort "! This module was made from a different services.jar (other ROM or build). Nothing was changed."
  fi
  PATCHES=$(cat "$MODPATH/patches.applied")
  ui_print "- services.jar matches. Patches: $PATCHES"
  [ -s "$MODPATH/system/framework/services.jar" ] || abort "! Module has no patched jar"
  finish_module "$PATCHES" || abort "! Nothing was changed on your system"
else
  # ---- choose patches: volume keys, unless a config already exists (WebUI writes the same file) ----
  mkdir -p "$CFG"
  if [ -f "$CFG/patches.conf" ]; then
    ui_print "- Using existing $CFG/patches.conf"
  else
    . "$MODPATH/prompt.sh"
    choose_patches
  fi

  PATCHES=$(selected_patches)
  report_skipped "$PATCHES"
  [ -n "$PATCHES" ] || abort "! No patch selected"
  ui_print ""
  ui_print "- Patches: $PATCHES"

  find_source || abort "! services.jar not found or hidden by the active module"
  run_patch "$PATCHES" || abort "! Nothing was changed on your system"


fi

set_perm_recursive "$MODPATH" 0 0 0755 0644
ui_print ""
ui_print "- Done. Reboot to apply."
