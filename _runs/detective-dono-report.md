# Detective Dono Report — CrossWindow v1.0 Production Readiness Audit
**Date:** 2026-05-26
**Auditor:** detective-dono (claude-sonnet-4-6)

---

## Overall Production Readiness Score: 74 / 100

**Verdict: SHIP_WITH_CONCERNS**

The core engine is solid, the build pipeline exists, and the six phases are complete. Three gaps block a clean SHIP verdict: no single-instance guard (critical for a hotkey app), PDB files shipped in dist-standalone, and the first-run registry sentinel is stored in the wrong key path. All three are fixable in under an hour.

---

## Section Scores

| Dimension | Score | Notes |
|-----------|-------|-------|
| Build pipeline | 16/20 | Publish profile exists; no publish script / release checklist |
| Distribution artifacts | 11/15 | PDB files in both dist dirs; .gitignore covers bin/obj |
| Code quality | 17/20 | One inaccurate comment; no warnings-as-errors; no test project |
| README completeness | 11/15 | Missing uninstall, upgrade, and prerequisites sections |
| Error resilience | 8/15 | No unhandled-exception handler; no single-instance guard |
| Security | 7/10 | No duplicate-ID validation in user config; first-run sentinel in wrong registry path |
| Feature completeness | 4/5 | All roadmap items done; CLI app.manifest lacks UAC requestedExecutionLevel |

---

## Findings

### P0 — Ship blockers

#### P0-1: No single-instance guard
**File:** `C:/Za/CrossWindow/CrossWindow.Tray/Program.cs`

There is no named `Mutex` check at startup. Running `crosswindow-tray.exe` twice launches two instances. The second instance calls `RegisterHotKey` with the same IDs (1–39); all registrations fail silently because the first instance already owns them. The second instance shows a tray icon, reports "0 hotkeys active" to the user, and silently does nothing — a confusing failure mode. This is the standard critical bug for any Windows hotkey utility.

**Fix:**
```csharp
// In Program.Main(), before creating TrayApp:
using var mutex = new System.Threading.Mutex(true, "Global\\CrossWindowTrayApp", out bool created);
if (!created)
{
    System.Windows.Forms.MessageBox.Show(
        "CrossWindow is already running.",
        "CrossWindow", System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Information);
    return;
}
var app = new TrayApp();
app.Run();
// mutex held until app exits
```

---

### P1 — Should fix before broad distribution

#### P1-1: PDB files shipped in dist-standalone/
**Files:** `C:/Za/CrossWindow/dist-standalone/crosswindow-tray.pdb`, `C:/Za/CrossWindow/dist-standalone/CrossWindow.Core.pdb`

Both PDB files are present in dist-standalone. PDBs expose full source paths, local variable names, and internal symbol layout. For a public release they should be excluded from the shipped zip/folder. They can be retained separately for crash symbol lookup.

**Fix — add to `win-x64-standalone.pubxml`:**
```xml
<ExcludeFilesFromPublish>
  *.pdb
</ExcludeFilesFromPublish>
```
Or strip PDBs from the distribution zip at packaging time.

---

#### P1-2: First-run sentinel stored in the wrong registry key
**File:** `C:/Za/CrossWindow/CrossWindow.Tray/TrayApp.cs`, lines 200–211

`CrossWindowFirstRunDone` is stored as a value under `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`. That key is for auto-start programs — enumerating startup entries via Task Manager, Process Explorer, or autoruns.exe will surface a stale-looking `CrossWindowFirstRunDone` value that has no associated executable path. This can trigger AV/security tool false positives and confuse the user.

**Fix:** Use a dedicated sub-key for app state, matching the config path convention:
```csharp
private const string RegAppState = @"SOFTWARE\CrossWindow";
// IsFirstRun / MarkFirstRunDone use Registry.CurrentUser.CreateSubKey(RegAppState)
```

---

#### P1-3: No unhandled-exception handler
**File:** `C:/Za/CrossWindow/CrossWindow.Tray/Program.cs`

If a dispatch exception escapes `DispatchBinding`'s try/catch (e.g., WPF Dispatcher exception, Sentry-style crash in `TrayApp.OnStartup`), the process terminates silently with no user-visible error. For a background utility there should be a last-resort handler that logs to `%APPDATA%\CrossWindow\crash.log` and shows a balloon tip before exiting.

**Fix:**
```csharp
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    var msg = (e.ExceptionObject as Exception)?.ToString() ?? e.ExceptionObject.ToString();
    var logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrossWindow", "crash.log");
    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
    File.AppendAllText(logPath, $"[{DateTime.Now:u}] {msg}\n\n");
};
```

---

#### P1-4: PDB files also present in dist/ (framework-dependent build)
**Files:** `C:/Za/CrossWindow/dist/crosswindow-tray.pdb`, `C:/Za/CrossWindow/dist/CrossWindow.Core.pdb`

Same concern as P1-1, applies to the framework-dependent build as well. If `dist/` is shipped to anyone, strip PDBs.

---

### P2 — Polish / correctness issues

#### P2-1: Inaccurate comment in FindTopmostWindowOnMonitor
**File:** `C:/Za/CrossWindow/CrossWindow.Core/Services/MovementEngine.cs`, line 125

```csharp
return false; // stop — GetForegroundWindow ordering means first valid is topmost
```

`EnumWindows` does **not** enumerate windows in Z-order. The Windows documentation states the enumeration order is arbitrary. The comment is misleading. The actual behavior is "first visible window belonging to the target monitor encountered by EnumWindows". In practice this often yields the most recently active window but it is not guaranteed. The behavior is acceptable for the use case but the comment should not claim a guarantee that doesn't exist.

**Fix:** Update comment to: `return false; // take first visible window found on target monitor (EnumWindows order is unspecified)`

---

#### P2-2: No duplicate-ID or ID=0 validation in ParseFile
**File:** `C:/Za/CrossWindow/CrossWindow.Core/Services/HotkeyConfig.cs`, `ParseFile` method

If a user edits `crosswindow.json` and introduces duplicate `id` values (e.g., two bindings with `id: 5`), the lookup dictionary in `HotkeyListener.Run()` will silently drop the second one because `lookup[b.Id] = b` overwrites without warning, and `RegisterHotKey` with a duplicate ID will fail (Win32 error 1409) for the second registration attempt. The WARN log covers the registration failure but the user has no indication the config has a duplicate.

Additionally `id = 0` passes through silently. Win32 `RegisterHotKey` accepts ID 0 but the behavior is undefined on some Windows versions.

**Fix (in `ParseFile`):**
```csharp
if (id <= 0) { Console.Error.WriteLine($"[WARN] Skipping binding with invalid id={id}"); continue; }
var seen = new HashSet<int>();
// ... after parsing each binding:
if (!seen.Add(id)) { Console.Error.WriteLine($"[WARN] Duplicate hotkey id={id} in config — skipping"); continue; }
```

---

#### P2-3: `TreatWarningsAsErrors` not enabled
**Files:** All three `.csproj` files

All projects have `<Nullable>enable</Nullable>` (good), but none set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. For a v1.0 release with stated "0 warnings" the enforcement should be codified in the project file so a future regression is caught by the build, not by manual inspection.

---

#### P2-4: README missing: uninstall, upgrade, and prerequisites sections
**File:** `C:/Za/CrossWindow/README.md`

The README (148 lines) covers install, config, and roadmap well. Missing:
- **Uninstall** — user has no documented path to fully remove CrossWindow. Steps needed: (1) Exit from tray, (2) delete `%APPDATA%\CrossWindow\`, (3) remove `HKCU\...\Run\CrossWindow` if startup was enabled, (4) remove `HKCU\...\Run\CrossWindowFirstRunDone` (once P1-2 is fixed, this goes away).
- **Upgrade** — what to do when a new version is released (copy over the .exe, startup registry auto-updates to new path because it stores the full path).
- **Prerequisites for dist/** — `dist/` is framework-dependent. The README says "no .NET install required" only for `dist-standalone/`, but doesn't clearly label `dist/` as requiring .NET 8 Desktop Runtime.

---

#### P2-5: Snap hotkeys conflict with Windows 11 built-in Snap Assist
**Note:** This is a user-experience concern, not a code defect.

`Ctrl+Shift+Win+Left/Right/Up/Down` (IDs 29–32) are the default CrossWindow snap bindings. Windows 11 does not use this exact chord (Win+Arrow is the system snap; Win+Z triggers Snap Layouts). However some third-party tools (DisplayFusion, FancyZones) may claim the same chords. The README could mention this and document how to remap.

---

#### P2-6: `dist/` and `dist-standalone/` committed to git despite .gitignore
**File:** `C:/Za/CrossWindow/.gitignore`

`.gitignore` lists `dist/` and `dist-standalone/` as excluded, but both directories exist in the working tree and contain compiled artifacts. If these were committed before the `.gitignore` entry was added, the files are tracked. Verify with `git ls-files dist/ dist-standalone/` — if they show up, run `git rm -r --cached dist/ dist-standalone/` to stop tracking them.

---

## Quality Gates Audit

| Gate | Status | Notes |
|------|--------|-------|
| Build passes 0 warnings | PASS | Confirmed by user |
| Nullable enable on all projects | PASS | All 3 csproj |
| app.manifest DPI + UAC (Tray) | PASS | PerMonitorV2 + asInvoker |
| app.manifest DPI (CLI) | PARTIAL | CLI has DPI awareness but no UAC requestedExecutionLevel block |
| Version metadata in Tray csproj | PASS | 1.0.0.0, product, copyright |
| Version metadata in Core/CLI csproj | FAIL | No Version/AssemblyVersion in Core or CLI csproj |
| .gitignore covers bin/obj | PASS | |
| .gitignore covers dist artifacts | PASS (intent) | But artifacts may be git-tracked already — see P2-6 |
| Publish profile exists | PASS | `win-x64-standalone.pubxml` |
| PDB excluded from dist-standalone | FAIL | Both .pdb files present |
| Single-instance guard | FAIL | No Mutex — see P0-1 |
| Unhandled exception handler | FAIL | None present |
| Config duplicate-ID validation | FAIL | No guard in ParseFile |
| First-run sentinel path correct | FAIL | Stored in Run key — see P1-2 |
| TreatWarningsAsErrors | FAIL | Not set in any project |
| Test project exists | FAIL | No unit tests anywhere |
| README covers uninstall | FAIL | Not documented |

---

## Slowest Jobs (Not Applicable)

No CI pipeline exists. Build is local `dotnet publish`. The standalone single-file build (~170MB) takes 20–40 seconds on first publish due to ReadyToRun compilation — acceptable for a release cadence.

---

## Next Steps (prioritized)

1. **[P0]** Add named Mutex to `Program.cs` — single-instance guard. 10 lines of code.
2. **[P1]** Add `<ExcludeFilesFromPublish>*.pdb</ExcludeFilesFromPublish>` to `win-x64-standalone.pubxml`, and strip PDBs from `dist/` before sharing.
3. **[P1]** Move first-run sentinel to `HKCU\SOFTWARE\CrossWindow` instead of the `Run` key.
4. **[P1]** Add `AppDomain.CurrentDomain.UnhandledException` handler that writes to `%APPDATA%\CrossWindow\crash.log`.
5. **[P2]** Add duplicate-ID and ID≤0 validation in `HotkeyConfig.ParseFile`.
6. **[P2]** Set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in all 3 csproj files.
7. **[P2]** Add Uninstall + Upgrade + Prerequisites (dist/ vs dist-standalone/) sections to README.
8. **[P2]** Fix misleading comment in `MovementEngine.FindTopmostWindowOnMonitor` (line 125).
9. **[P2]** Add Version/AssemblyVersion to `CrossWindow.Core.csproj` and `CrossWindow.CLI.csproj`.
10. **[P2]** Verify `git ls-files dist/ dist-standalone/` — run `git rm -r --cached` if artifacts are tracked.

---

## Risks and Mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| Two instances launch → second instance silent no-op | High | P0-1 Mutex fix |
| AV tool flags CrossWindowFirstRunDone in Run key | Medium | P1-2 registry path fix |
| Shipped PDBs expose internal symbol layout | Medium | P1-1 exclude PDBs from publish |
| Crash in hotkey pump → tray icon stuck, no feedback | Medium | P1-3 unhandled exception handler + crash log |
| User with corrupt JSON → falls back to defaults silently | Low | Acceptable; [WARN] is logged to stderr |
| Conflicting hotkeys with third-party tools | Low | Document in README; already gracefully degraded by WARN logging |
| EnumWindows returns unexpected window for swap | Low | Behavior is acceptable; fix comment accuracy only |
