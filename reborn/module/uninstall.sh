#!/system/bin/sh
rm -rf /data/adb/smalipatcher
for f in /data/dalvik-cache/*/*services.jar* /data/misc/apexdata/com.android.art/dalvik-cache/*/*services.jar*; do
  [ -e "$f" ] && rm -f "$f"
done
