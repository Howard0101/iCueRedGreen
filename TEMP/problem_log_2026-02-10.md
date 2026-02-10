# Problem Log (Draft)

- IMG-001 | Tray icon missing | Source: chat image (2026-02-10 18:27).
- IMG-002 | Settings form layout | Source: chat image (2026-02-10 18:30).
- IMG-003 | Configuration missing dialog | Source: chat image (2026-02-10 18:31).
- PROB-005 | Restart worker with missing configuration shows an information message box; it should be warning or error. | Screenshot: IMG-003 | Source: D:\\Source\\Repos\\Privat\\iCueRedGreen\\TEMP\\problem_log_2026-02-10.md
- IMG-004 | Dev mode fields missing (host/AIN) | Source: chat image (2026-02-10 18:37).
- PROB-006-ENV | FRITZ_HOST=192.168.178.1; FRITZ_AIN=11630 0474403; FRITZ_USERNAME=smarthome; FRITZ_PASSWORD=[REDACTED] | Source: D:\\Source\\Repos\\Privat\\iCueRedGreen\\TEMP\\problem_log_2026-02-10.md
- PROB-007 | Toggle-on-keypress has a long lag before execution, causing inconsistent Scroll Lock indicator state on the keyboard (NumLock-style LED) and the iCUE LED. | Source: D:\Source\Repos\Privat\iCueRedGreen\TEMP\problem_log_2026-02-10.md
- PROB-008 | Settings label "Toggle on" is too unspecific; users will not understand what it does. | Source: D:\Source\Repos\Privat\iCueRedGreen\TEMP\problem_log_2026-02-10.md
- PROB-009 | Tray icon does not change on state change. | Source: D:\Source\Repos\Privat\iCueRedGreen\TEMP\problem_log_2026-02-10.md | Related: PROB-011
- PROB-010 | Menu label "Open Logs" should be "Open Log"; it should open the single log file with the OS-associated app (not the folder). | Source: D:\Source\Repos\Privat\iCueRedGreen\TEMP\problem_log_2026-02-10.md
- PROB-011 | Tray state icons are identical; OFF should be mainly green, ON mainly red, UNKNOWN mainly blue, and the default icon must exactly match the current on-disk icon. | Related: PROB-009 | Source: D:\Source\Repos\Privat\iCueRedGreen\TEMP\problem_log_2026-02-10.md

## Fixed
- PROB-001 | Tray icon not visible. | Screenshot: IMG-001 | Source: D:\\Source\\Repos\\Privat\\iCueRedGreen\\TEMP\\problem_log_2026-02-10.md | Status: Fixed (v1.1.0.7)
- PROB-002 | Settings form: validation error indicators (red markers) are not visible after the first four textboxes. | Screenshot: IMG-002 | Source: D:\\Source\\Repos\\Privat\\iCueRedGreen\\TEMP\\problem_log_2026-02-10.md | Status: Fixed (v1.1.0.8)
- PROB-003 | Settings form: not enough vertical padding between the last label and the checkbox area. | Screenshot: IMG-002 | Source: D:\\Source\\Repos\\Privat\\iCueRedGreen\\TEMP\\problem_log_2026-02-10.md | Status: Fixed (v1.1.0.8)
- PROB-004 | Settings form: checkboxes should align with the label/textbox grid (checkbox right of description, aligned under textboxes, text vertically centered). | Screenshot: IMG-002 | Source: D:\\Source\\Repos\\Privat\\iCueRedGreen\\TEMP\\problem_log_2026-02-10.md | Status: Fixed (v1.1.0.8)
- PROB-006 | Dev mode enabled with no config: FRITZ host and AIN fields are not shown even though environment variables are set. | Screenshot: IMG-004 | Source: D:\\Source\\Repos\\Privat\\iCueRedGreen\\TEMP\\problem_log_2026-02-10.md | Status: Fixed (v1.1.0.9)
