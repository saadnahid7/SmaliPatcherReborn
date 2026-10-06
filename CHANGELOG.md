# Changelog

## v0.5.0-dev (unreleased)
- New patch **Overlay any** (off by default): overlay apps can override any resource of their target on Android 11 to 17, and a differently signed overlay app can be installed on Android 13 to 17. On Android 10 the restriction is decided in native code, so the patch changes nothing there and is skipped (tested). The signature check on Android 11 and 12 sits in a large method next to an unrelated security check, so it is left alone.
- Patches that do not exist on the phone's Android version are never offered or applied: the install screen skips their question, the WebUI hides them, the desktop app grays them out, and the module drops them from an old config (and says so). If nothing applicable is left, the install is refused and nothing is changed. The patch list comes from the engine's own catalog, so the module, WebUI and app cannot disagree with it.
- Fix: the desktop app reused an old extracted patch engine when a newer build had the same version number.
- Safety: the boot guard now checks that the patched `services.jar` is present and is the file that was built, and disables the module if not. A crash loop in the system server never reboots the phone, so the three-failed-boots guard could not catch a damaged jar.
- Desktop app: a **Donate** card at the bottom, and after every successful patch a short thank-you with the wallets and **Later** / **Never ask again** (Never is remembered). The Magisk WebUI has a compact **Donate** popup, never an automatic prompt. No donation UI is shown unless real wallet addresses were built in.

## v0.4.0-dev
- Two more patches, all off by default: **High volume warning off** and **GNSS updates off**. They match on Android 10 to 17.
- Install screen: a new question "Show the 2 extra options?" opens them. The WebUI and desktop app list them as switches.
- A signature-verification patch was built and tested, then pulled before release: an early build crashed Bluetooth at boot on Android 14. It needs a narrower rewrite before it ships.
- A recovery-reboot patch (power-menu Restart goes to recovery) was also built, fixed after two real bugs, and verified clean on real hardware - then removed by request. It worked exactly as designed, but that design means *every* Restart from the power menu goes to recovery once enabled, with no way to still do a normal restart; too disruptive for a daily-driver phone. Dropped instead of adding a workaround.
- Fix: **High volume warning off** did the opposite of its name on Android 14+ (a popup on every volume change, instead of none). AOSP flipped the meaning of the check's return value between releases; found by real-device testing on a OnePlus 12R, fixed and verified against every supported version.

## v0.3.2-dev
- New app icon (a phoenix between code braces) for the window and the exe. App only; the module is unchanged (still v0.3.1-dev).
- Linux and macOS downloads are now `.tar.gz` archives that keep the executable permission, so no `chmod` is needed.

## v0.3.1-dev
- Install screen: the volume-key questions now start with a 3-second overview of what you will be asked, show what each key press chose, and end with a summary. The stray "Terminated" lines are gone, and a key press now registers at once.

## v0.3.0-dev
- Manual jar patching: pick a `system/framework` folder (or `services.jar`) copied from a phone, and the app builds a ready-to-flash module next to it.
- Module updates through the Magisk / KernelSU / APatch app (`updateJson`).
- Fix: Install button crashed with "Call from invalid thread".
- Module author is shown as saadnahid7.

## v0.2.0-dev
- First release: on-device patching module, WebUI, desktop app for Windows / Linux / macOS.
