#!/system/bin/sh
# Used by the WebUI: prints the ids of the patches that exist on this Android version.
MODPATH=${0%/*}
. "$MODPATH/lib.sh"
applicable_patches
