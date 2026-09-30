#!/system/bin/sh
# Late boot: the system came up, so the boot-loop counter can be reset.
CFG=/data/adb/smalipatcher
until [ "$(getprop sys.boot_completed)" = 1 ]; do sleep 3; done
sleep 20
echo 0 > "$CFG/boot_count"
