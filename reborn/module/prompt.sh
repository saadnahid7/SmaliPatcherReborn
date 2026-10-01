#!/system/bin/sh
# Patch selection with the volume keys. Sourced by customize.sh.
# Needs: ui_print, CFG. Writes $CFG/patches.conf. SPR_PROMPT_SECS overrides the 10 s per question (used for testing).

SECS=${SPR_PROMPT_SECS:-10}
KEY_YES=${SPR_KEY_YES:-KEY_VOLUMEUP}      # overridable only so the logic can be tested on an emulator
KEY_NO=${SPR_KEY_NO:-KEY_VOLUMEDOWN}
EVENT_CMD=${SPR_EVENT_CMD:-"getevent -lq"}

# ask <n> <title> <hint> <default y|n>   -> sets ANS to y or n and prints what was chosen
ask() {
  local def_txt ev
  [ "$4" = y ] && def_txt=YES || def_txt=NO
  ui_print ""
  ui_print "  ▸ Step $1 of 3  ·  $2"
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
  ui_print "   Volume UP = YES      Volume DOWN = NO"
  ui_print "   Each question waits ${SECS} seconds, then keeps its default."
  ui_print ""
  ui_print "   Starting in 3 seconds..."
  sleep 3

  ask 1 "Hide mock-location flag" "Apps that check Location.isMock() see a normal location." y;   A1=$ANS
  ask 2 "Mock apps without developer setting" "Test providers work without picking a mock location app." y;   A2=$ANS
  ask 3 "Screenshots in secure windows" "Ignore FLAG_SECURE for screenshots and screen recording." n;   A3=$ANS

  mkdir -p "$CFG"
  {
    [ "$A1" = y ] && echo "mock-hide=1" || echo "mock-hide=0"
    [ "$A2" = y ] && echo "mock-permission=1" || echo "mock-permission=0"
    [ "$A3" = y ] && echo "secure-flag=1" || echo "secure-flag=0"
  } > "$CFG/patches.conf"

  ui_print ""
  ui_print "  ┌─ Your selection ─────────────────────────"
  ui_print "  │  [$(onoff $A1)]  Hide mock-location flag"
  ui_print "  │  [$(onoff $A2)]  Mock apps without developer setting"
  ui_print "  │  [$(onoff $A3)]  Screenshots in secure windows"
  ui_print "  └──────────────────────────────────────────"
  ui_print ""
}
