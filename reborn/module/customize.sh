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

# ---- choose patches: volume keys, unless a config already exists (WebUI writes the same file) ----
ask() { # ask "<question>" <default y|n>   Vol+ = yes, Vol- = no, 10 s timeout = default
  ui_print ""
  ui_print "  $1"
  ui_print "  [Vol+] yes   [Vol-] no   (default: $2, 10s)"
  local end=$(( $(date +%s) + 10 )) ev
  while [ "$(date +%s)" -lt "$end" ]; do
    ev=$(timeout 1 getevent -lqc 1 2>/dev/null | grep -m1 'KEY_VOLUME.*DOWN')
    case "$ev" in
      *KEY_VOLUMEUP*)   return 0 ;;
      *KEY_VOLUMEDOWN*) return 1 ;;
    esac
  done
  [ "$2" = y ]
}

mkdir -p "$CFG"
if [ -f "$CFG/patches.conf" ]; then
  ui_print "- Using existing $CFG/patches.conf"
else
  ui_print "- Choose patches (do nothing to keep the defaults)"
  : > "$CFG/patches.conf"
  if ask "Hide mock-location flag (Location.isMock)?" y;              then echo "mock-hide=1"       >> "$CFG/patches.conf"; else echo "mock-hide=0"       >> "$CFG/patches.conf"; fi
  if ask "Allow mock apps without the mock-app developer setting?" y; then echo "mock-permission=1" >> "$CFG/patches.conf"; else echo "mock-permission=0" >> "$CFG/patches.conf"; fi
  if ask "Allow screenshots in secure (FLAG_SECURE) windows?" n;      then echo "secure-flag=1"     >> "$CFG/patches.conf"; else echo "secure-flag=0"     >> "$CFG/patches.conf"; fi
fi

PATCHES=$(selected_patches)
[ -n "$PATCHES" ] || abort "! No patch selected"
ui_print ""
ui_print "- Patches: $PATCHES"

find_source || abort "! services.jar not found or hidden by the active module"
run_patch "$PATCHES" || abort "! Nothing was changed on your system"

set_perm_recursive "$MODPATH" 0 0 0755 0644
ui_print ""
ui_print "- Done. Reboot to apply."
