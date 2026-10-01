# Changelog

## v0.4.0-dev
- Three more patches, all off by default: **High volume warning off**, **Recovery reboot** (power-menu Restart goes to recovery) and **GNSS updates off**. They match on Android 10 to 17.
- Install screen: a new question "Show the 3 extra options?" opens them. The WebUI and desktop app list them as switches.
- A signature-verification patch was built and tested, then pulled before release: an early build crashed Bluetooth at boot on Android 14. It needs a narrower rewrite before it ships.
- Fix: **High volume warning off** did the opposite of its name on Android 14+ (a popup on every volume change, instead of none). AOSP flipped the meaning of the check's return value between releases; found by real-device testing on a OnePlus 12R, fixed and verified against every supported version.
- Fix: **Recovery reboot** also silently changed what *Power off* does, not just *Restart* - both menu items share one string in the method the patch edits. Narrowed the patch to the Restart path only; verified on every supported version that Power off is untouched.

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
