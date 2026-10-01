#!/system/bin/sh
# Patch selection with the volume keys. Sourced by customize.sh.
# Needs: ui_print, CFG. Writes $CFG/patches.conf. SPR_PROMPT_SECS overrides the 10 s per question (used for testing).

SECS=${SPR_PROMPT_SECS:-10}
TOTAL=3
KEY_YES=${SPR_KEY_YES:-KEY_VOLUMEUP}      # overridable only so the logic can be tested on an emulator
KEY_NO=${SPR_KEY_NO:-KEY_VOLUMEDOWN}
EVENT_CMD=${SPR_EVENT_CMD:-"getevent -lq"}

# ask <n> <title> <hint> <default y|n>   -> sets ANS to y or n and prints what was chosen
ask() {
  local def_txt ev
  [ "$4" = y ] && def_txt=YES || def_txt=NO
  ui_print ""
  if [ "$1" = X ]; then ui_print "  ▸ Extra options  ·  $2"; else ui_print "  ▸ Step $1 of $TOTAL  ·  $2"; fi
  ui_print "    $3"
  ui_print "    [Vol+] YES     [Vol-] NO     ${SECS}s, then default: $def_txt"
  # one listener for the whole wait, so no key press can fall between checks; stderr hidden so no "Terminated" noise
  ev=$( { timeout "$SECS" $EVENT_CMD 2>/dev/null | grep -m1 -E "($KEY_YES|$KEY_NO) +DOWN"; } 2>/dev/null )
  case "$ev" in
    *$KEY_YES*)       ANS=y; ui_print "    ✔ YES   (you pressed Vol+)" ;;
    *$KEY_NO*)        ANS=n; ui_print "    ✔ NO    (you pressed Vol-)" ;;
    *)                ANS=$4; ui_print "    ✔ $def_txt   (no key pressed, default kept)" ;;
  esac
}

onoff() { [ "$1" = y ] && echo "ON " || echo "OFF"; }

choose_patches() {
  ui_print ""
  ui_print "  ╔════════════════════════════════════════╗"
  ui_print "  ║         CHOOSE YOUR PATCHES            ║"
  ui_print "  ╚════════════════════════════════════════╝"
  ui_print ""
  ui_print "   You will be asked 3 questions:"
  ui_print ""
  ui_print "   1 · Hide mock-location flag        (default ON)"
  ui_print "   2 · Mock apps without dev setting  (default ON)"
  ui_print "   3 · Screenshots in secure windows  (default OFF)"
  ui_print ""
  ui_print "   After these you can open 4 extra options (all default OFF):"
  ui_print "   High volume warning · Signature verification"
  ui_print "   Recovery reboot · GNSS (GPS) updates off"
  ui_print ""
  ui_print "   Volume UP = YES      Volume DOWN = NO"
  ui_print "   Each question waits ${SECS} seconds, then keeps its default."
  ui_print ""
  ui_print "   Starting in 3 seconds..."
  sleep 3

  ask 1 "Hide mock-location flag" "Apps that check Location.isMock() see a normal location." y;   A1=$ANS
  ask 2 "Mock apps without developer setting" "Test providers work without picking a mock location app." y;   A2=$ANS
  ask 3 "Screenshots in secure windows" "Ignore FLAG_SECURE for screenshots and screen recording." n;   A3=$ANS

  A4=n; A5=n; A6=n; A7=n
  ask X "Show the 4 extra options?" "Vol+ opens them, Vol- skips them (they stay OFF)." n
  if [ "$ANS" = y ]; then
    TOTAL=7
    ask 4 "High volume warning" "No safe-volume popup when raising headphone volume." n;   A4=$ANS
    ask 5 "Signature verification" "Signature checks always match. Lets differently signed apps replace installed ones." n;   A5=$ANS
    ask 6 "Recovery reboot" "Restart in the power menu reboots into recovery." n;   A6=$ANS
    ask 7 "GNSS updates off" "Real GPS fixes are ignored. Pair with a mock location app." n;   A7=$ANS
  fi

  mkdir -p "$CFG"
  {
    [ "$A1" = y ] && echo "mock-hide=1" || echo "mock-hide=0"
    [ "$A2" = y ] && echo "mock-permission=1" || echo "mock-permission=0"
    [ "$A3" = y ] && echo "secure-flag=1" || echo "secure-flag=0"
    [ "$A4" = y ] && echo "high-volume=1" || echo "high-volume=0"
    [ "$A5" = y ] && echo "sig-verify=1" || echo "sig-verify=0"
    [ "$A6" = y ] && echo "recovery-reboot=1" || echo "recovery-reboot=0"
    [ "$A7" = y ] && echo "gnss-off=1" || echo "gnss-off=0"
  } > "$CFG/patches.conf"

  ui_print ""
  ui_print "  ┌─ Your selection ─────────────────────────"
  ui_print "  │  [$(onoff $A1)]  Hide mock-location flag"
  ui_print "  │  [$(onoff $A2)]  Mock apps without developer setting"
  ui_print "  │  [$(onoff $A3)]  Screenshots in secure windows"
  ui_print "  │  [$(onoff $A4)]  High volume warning"
  ui_print "  │  [$(onoff $A5)]  Signature verification"
  ui_print "  │  [$(onoff $A6)]  Recovery reboot"
  ui_print "  │  [$(onoff $A7)]  GNSS updates off"
  ui_print "  └──────────────────────────────────────────"
  ui_print ""
}
