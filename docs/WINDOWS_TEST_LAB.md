[English](WINDOWS_TEST_LAB.md) | [简体中文](zh-CN/WINDOWS_TEST_LAB.md)

# Windows VM compatibility testing

Since 2026-10-28, local test-lab files are stored in `E:\workspace\istart\.istart-testlab`; the former workspace-adjacent path is a compatibility junction. The VM, disks, images, private credentials, setup state and reports remain local and Git-ignored. Relocating the lab does not establish new compatibility-test results.

Preparation snapshot: 2026-10-16. Windows 10 was installed and reached its desktop, but application acceptance had not run. This does not establish compatibility. The fixed test baseline is `0.2.0-preview.21`, source `cb445edcdf758debc5a139b75b9d8632414e827a`; this is not a claim about the latest public release.

VirtualBox 7.2.20 was installed and a basic VM booted without rebooting the host. The dedicated VM had 4 vCPUs, 8 GiB RAM, and an 80 GiB dynamic disk. The official Chinese evaluation image passed Microsoft's SHA256 check. Windows 10 installation reached the dedicated test account's desktop, and Guest Additions driver logs identified build 19045. Remote execution was still being checked; app acceptance had not started. The verified package was placed on a read-only ISO. Windows PowerShell 5.1 read-only preflight confirmed that the interactive-test guard rejects the Windows 11 host.

At that snapshot, background preparation was waiting for graceful Windows shutdown/finalization before reboot, remote-execution checks, snapshot creation, test-ISO mounting, and standard-user acceptance. State/errors were recorded in the workspace-adjacent `.istart-testlab/setup-state.json` and `setup-error.txt`; results were intended for `.istart-testlab/win10-results`. An unready Windows license state stops acceptance and records a preflight failure rather than a product compatibility failure. Snapshot/app testing was incomplete; bounded-wait failure preserves evidence. This dated record does not claim the preparation process is still running.

## Environment and isolation

- Use the current Windows 11 PC as host, with Windows 10 22H2 x64 as the first guest target and LTSC 2021 (19044) later. One VM does not replace tests across hardware vendors.
- The selected setup uses 4 vCPUs, 8 GiB RAM, an 80 GiB dynamic disk, and basic VirtualBox NAT. No Extension Pack, bridged networking, shared clipboard, drag-and-drop, or personal-folder shares.
- Use an official Microsoft evaluation image or an appropriately licensed installation image. Follow its license and evaluation term; do not extend/bypass expiry or assume the host's Windows license covers the VM.
- Use a dedicated local test account without real configuration, personal files, browser history, or accounts. Transfer the public installer/scripts through a read-only ISO.
- Create `clean-os` after OS/Guest Additions setup, and `istart-baseline` after installing the public baseline. Retain both recovery points and snapshots for active investigations; no default bulk cleanup.

## Installer baseline

Download the installer and `release-manifest.json` from the [preview.21 release](https://github.com/morrowfactory/istart/releases/tag/v0.2.0-preview.21). Verify version, source SHA, length, and SHA256.

Baseline: `iStart-Setup-0.2.0-preview.21.exe`, 53,635,225 bytes, SHA256:

```text
2734c997d0ffb8533da87c4b9f9aeb4ac86723316e308e64f06b63850492d23e
```

Windows 10 22H2 Chinese x64 Enterprise Evaluation ISO checksum from [Microsoft's hash document](https://download.microsoft.com/download/c/1/1/c11d2ca5-967c-45c0-bc7d-2d9ca3f1fe07/Windows10Enterprise22H2HashValues.pdf):

```text
B586B4A2B936B44CEC5A785E0E2696B4816A66A40D287D4203CE18F56970D1C4
```

## Automated checks

After installing the public baseline inside the VM, exit everyday iStart from its tray and keep the guest desktop signed in and unlocked. Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-WindowsVm.ps1 -Rounds 3
```

The script rejects input injection on the host, outside a Windows 10 19044/19045 x64 VM, or in administrator/service sessions. Interactive checks run as a standard user to avoid elevated rights masking problems. `-PreflightOnly` is read-only on any Windows system and records environment/program version without integrating Start.

The automated sequence uses existing acceptance entry points: button discovery; data/layout regression; Windows key, Start button, Win+R, native switching, abnormal test-child recovery; and presentation over a maximized background, search focus, blur dismissal, minimized restore, and actual integration enablement. Configuration/output use dedicated test directories, preserving everyday settings. New result directories are retained under the guest desktop's `iStart-test-results`. An automated pass still reports `AutomatedPass-ManualPending`.

`-Rounds` repeats window checks using a new process each round; it does not establish long-running stability of one instance. Before guest execution, report only syntax checks and host-preflight results.

## Manual matrix and acceptance gates

| Scenario | Steps and acceptance |
| --- | --- |
| The two reported problems | Maximize Notepad; separately press Windows, click system Start, and open the iStart shortcut. Menu reaches foreground, direct English/Chinese search works, and native Start does not appear simultaneously. |
| Repeated use | In the same instance, alternate Windows-key and Start-button opens 50 times each; idle 45 minutes, then repeat 10 times. No lost integration, obscured menu, or duplicate opening. |
| Native behavior and shortcuts | Win+R and Win+E work; temporary native access, disable, and exit restore native Start. |
| Taskbar | Test auto-hide off/on and bottom/left/right/top individually. Do not guess coordinates; detection failure keeps native behavior. |
| Display | Test 100%, 150%, and 200% scaling and retry after resolution changes. Record multi-monitor work separately; single-monitor testing does not cover it. |
| Recovery | Restart Explorer, lock/unlock, sleep/wake, and reboot/sign-in inside the VM only. Do not run this group on the host. |
| Installation maintenance | First install defaults to native; opt-in enables integration; upgrade preserves test configuration; native Start works after uninstall. |

For each case record OS/patch, iStart source SHA, display settings, steps, actual result, and Pass/Fail/NotRun. Captures contain test data only. On a core failure, preserve logs, fix it, then rerun that case and previously passed core checks. Record Windows 10 22H2 and LTSC separately rather than claiming every Windows 10 build passed.

The GitHub-hosted Windows CI environment described in this record uses a Windows Server image and cannot replace desktop Start-menu acceptance. See [GitHub runner images](https://github.com/actions/runner-images#available-images) for image definitions.
