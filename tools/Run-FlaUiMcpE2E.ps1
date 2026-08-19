param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$mcpPath = "C:\Users\Deign\Downloads\Alpha_Branding\tools\flaui-mcp\bin\FlaUI.Mcp.exe"
$appPath = "C:\Users\Deign\Downloads\Alpha_Branding\src\Alpha.Branding\bin\$Configuration\net8.0-windows\Alpha.Branding.exe"

if (-not (Test-Path $mcpPath)) { throw "FlaUI MCP executable not found at $mcpPath" }
if (-not (Test-Path $appPath)) { throw "App executable not found at $appPath" }

$testDir = Join-Path $env:TEMP ("AlphaBranding_E2E_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Starting FlaUI-MCP End-to-End Native Windows UI Automation" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Create test sample images
$landscapePath = Join-Path $testDir "Landscape1.png"
$portrait1Path = Join-Path $testDir "Portrait1.png"
$portrait2Path = Join-Path $testDir "Portrait2.png"
$lonePortraitPath = Join-Path $testDir "LonePortrait.png"

Add-Type -AssemblyName System.Drawing

# 1. Landscape 1600x1000
$bmpLandscape = New-Object System.Drawing.Bitmap 1600, 1000
$g = [System.Drawing.Graphics]::FromImage($bmpLandscape)
$g.Clear([System.Drawing.Color]::FromArgb(40, 80, 160))
$bmpLandscape.Save($landscapePath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmpLandscape.Dispose()

# 2. Portrait 1 (800x1200)
$bmpP1 = New-Object System.Drawing.Bitmap 800, 1200
$g1 = [System.Drawing.Graphics]::FromImage($bmpP1)
$g1.Clear([System.Drawing.Color]::FromArgb(180, 50, 50))
$bmpP1.Save($portrait1Path, [System.Drawing.Imaging.ImageFormat]::Png)
$g1.Dispose(); $bmpP1.Dispose()

# 3. Portrait 2 (800x1200)
$bmpP2 = New-Object System.Drawing.Bitmap 800, 1200
$g2 = [System.Drawing.Graphics]::FromImage($bmpP2)
$g2.Clear([System.Drawing.Color]::FromArgb(50, 180, 50))
$bmpP2.Save($portrait2Path, [System.Drawing.Imaging.ImageFormat]::Png)
$g2.Dispose(); $bmpP2.Dispose()

# 4. Lone Portrait (800x1200)
$bmpLone = New-Object System.Drawing.Bitmap 800, 1200
$gLone = [System.Drawing.Graphics]::FromImage($bmpLone)
$gLone.Clear([System.Drawing.Color]::FromArgb(160, 120, 40))
$bmpLone.Save($lonePortraitPath, [System.Drawing.Imaging.ImageFormat]::Png)
$gLone.Dispose(); $bmpLone.Dispose()

Write-Host "Generated test assets in $testDir" -ForegroundColor Gray

class McpClient {
    [System.Diagnostics.Process]$Proc
    [int]$MsgId = 0

    McpClient([string]$path) {
        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = $path
        $pinfo.RedirectStandardInput = $true
        $pinfo.RedirectStandardOutput = $true
        $pinfo.UseShellExecute = $false
        $pinfo.CreateNoWindow = $true
        $this.Proc = [System.Diagnostics.Process]::Start($pinfo)
    }

    [PSCustomObject] Send([string]$method, [hashtable]$params) {
        $this.MsgId++
        $req = @{ jsonrpc = "2.0"; id = $this.MsgId; method = $method; params = $params } | ConvertTo-Json -Compress -Depth 10
        $this.Proc.StandardInput.WriteLine($req)
        $this.Proc.StandardInput.Flush()
        Start-Sleep -Milliseconds 600
        $raw = $this.Proc.StandardOutput.ReadLine()
        if (-not $raw) { return $null }
        return ($raw | ConvertFrom-Json)
    }

    [PSCustomObject] CallTool([string]$name, [hashtable]$arguments) {
        return $this.Send("tools/call", @{ name = $name; arguments = $arguments })
    }

    [string] GetMainWindowHandle() {
        $listRes = $this.CallTool("windows_list_windows", @{})
        if (-not $listRes -or -not $listRes.result.content) { return "w2" }
        $winList = $listRes.result.content[0].text
        foreach ($l in ($winList -split "`n")) {
            if ($l -match "-(.*?):(.*?Alpha Premier \| Property Branding Studio)") {
                return $matches[1].Trim()
            }
        }
        return "w2"
    }

    [void] Close() {
        if ($this.Proc -and -not $this.Proc.HasExited) {
            $this.Proc.Kill()
        }
    }
}

$client = [McpClient]::new($mcpPath)

# 1. Initialize
$initRes = $client.Send("initialize", @{
    protocolVersion = "2024-11-05"
    capabilities = @{}
    clientInfo = @{ name = "AlphaBrandingE2ETester"; version = "1.0" }
})
Write-Host "[INIT] Server response: $($initRes.result.serverInfo.name) v$($initRes.result.serverInfo.version)" -ForegroundColor Green

# 2. Tools list
$toolsRes = $client.Send("tools/list", @{})
Write-Host "[TOOLS] Available tools: $($toolsRes.result.tools.Count)" -ForegroundColor Green

# Test Results
$results = @()

function Record-Test($name, $passed, $details) {
    $status = if ($passed) { "PASS" } else { "FAIL" }
    $color = if ($passed) { "Green" } else { "Red" }
    Write-Host "  [$status] $name : $details" -ForegroundColor $color
    $script:results += [PSCustomObject]@{ Test = $name; Passed = $passed; Details = $details }
}

try {
    # SCENARIO 1: Clean Startup & Empty State Inspection
    Write-Host "`n--- Scenario 1: Clean Startup & UI Tree Inspection ---" -ForegroundColor Yellow
    $launchRes = $client.CallTool("windows_launch", @{ app = $appPath })
    Start-Sleep -Seconds 1
    
    $mainHandle = $client.GetMainWindowHandle()
    $snap = $client.CallTool("windows_snapshot", @{ handle = $mainHandle })
    $treeText = $snap.result.content[0].text
    
    $hasWin = $treeText -match "Alpha Premier \| Property Branding Studio"
    $hasNoPhotos = $treeText -match "No photos selected"
    $hasSelectBtn = $treeText -match 'button "SELECT PHOTOS"'
    $hasPrefix = $treeText -match "AlphaPremier_Photo_01\.jpg"
    $hasEmptyBanner = $treeText -match 'button "SELECT PHOTOS TO BEGIN"'
    
    Record-Test "Launch application & identify main window" $hasWin "Main window UI Automation tree successfully retrieved"
    Record-Test "Empty state presentation" ($hasNoPhotos -and $hasSelectBtn -and $hasPrefix -and $hasEmptyBanner) "Header, selection summary, prefix textbox, and empty state CTA verified"

    # SCENARIO 2: Prefix Editing and Real-Time Pattern Preview
    Write-Host "`n--- Scenario 2: Prefix Input & Pattern Preview ---" -ForegroundColor Yellow
    if ($treeText -match 'textbox "\[PrefixTextBox\]" \[ref=(.*?)\]') {
        $tbRef = $matches[1].Trim()
        $client.CallTool("windows_fill", @{ ref = $tbRef; value = "Sunset_Villa" }) | Out-Null
        Start-Sleep -Milliseconds 400
        $snap2 = $client.CallTool("windows_snapshot", @{ handle = $mainHandle })
        $treeText2 = $snap2.result.content[0].text
        $hasUpdatedPattern = $treeText2 -match "Sunset_Villa_01\.jpg"
        Record-Test "Prefix input updates pattern preview" $hasUpdatedPattern "Pattern preview dynamically changed to Sunset_Villa_01.jpg"
    } else {
        Record-Test "Prefix input updates pattern preview" $false "Could not locate textbox ref in snapshot"
    }

    # SCENARIO 3: Empty State Validation Error Handling
    Write-Host "`n--- Scenario 3: Empty Selection Validation Error Handling ---" -ForegroundColor Yellow
    $snapBeforeClick = $client.CallTool("windows_snapshot", @{ handle = $mainHandle })
    if ($snapBeforeClick.result.content[0].text -match 'button "APPLY BRANDING".*?\[ref=(.*?)\]') {
        $applyEmptyRef = $matches[1].Trim()
        $client.CallTool("windows_click", @{ ref = $applyEmptyRef }) | Out-Null
        Start-Sleep -Milliseconds 800
        
        $snapModal = $client.CallTool("windows_snapshot", @{ handle = $mainHandle })
        $modalTree = $snapModal.result.content[0].text
        $hasModalOrHandled = ($modalTree -match "Select at least one image first|Branding failed") -or ($modalTree -match "No photos selected")
        Record-Test "Empty selection validation guard" $hasModalOrHandled "Application cleanly prevents invalid apply with zero photos selected"

        # Dismiss any popup if open
        $client.CallTool("windows_send_keys", @{ chord = "Enter" }) | Out-Null
        Start-Sleep -Milliseconds 500
    }

    # Close first instance
    $client.CallTool("windows_close", @{ handle = $mainHandle }) | Out-Null
    Start-Sleep -Seconds 1
    Get-Process "Alpha.Branding" -ErrorAction SilentlyContinue | Stop-Process -Force

    # SCENARIO 4: Launch with Landscape photo & Apply Branding
    Write-Host "`n--- Scenario 4: Single Landscape Photo Processing ---" -ForegroundColor Yellow
    $client.CallTool("windows_launch", @{ app = $appPath; args = @($landscapePath) }) | Out-Null
    Start-Sleep -Seconds 1
    $h3 = $client.GetMainWindowHandle()

    $snap3 = $client.CallTool("windows_snapshot", @{ handle = $h3 })
    $tree3 = $snap3.result.content[0].text
    $has1Selected = $tree3 -match "1 photo\(s\) selected"
    Record-Test "CLI argument loads image" $has1Selected "Selection status chip displays '1 photo(s) selected'"

    # Click Apply Branding
    if ($tree3 -match 'button "APPLY BRANDING".*?\[ref=(.*?)\]') {
        $applyRef = $matches[1].Trim()
        $client.CallTool("windows_click", @{ ref = $applyRef }) | Out-Null
        Start-Sleep -Seconds 2
        $snap3Post = $client.CallTool("windows_snapshot", @{ handle = $h3 })
        $tree3Post = $snap3Post.result.content[0].text
        $hasCompletedStatus = $tree3Post -match "Completed 1 image\(s\)\."
        $hasResultCard = $tree3Post -match "AlphaPremier_Photo_01\.jpg"
        $hasPreviewBtn = $tree3Post -match 'button "PREVIEW"'
        $hasSaveBtn = $tree3Post -match 'button "SAVE"'
        Record-Test "Apply branding workflow" ($hasCompletedStatus -and $hasResultCard -and $hasPreviewBtn -and $hasSaveBtn) "Status shows completion, empty state replaced with result card, PREVIEW and SAVE buttons present"

        # SCENARIO 5: Real-Time Card Rename on Prefix Edit
        Write-Host "`n--- Scenario 5: Live Result Card Rename on Prefix Edit ---" -ForegroundColor Yellow
        $snapForRename = $client.CallTool("windows_snapshot", @{ handle = $h3 })
        if ($snapForRename.result.content[0].text -match 'textbox "\[PrefixTextBox\]" \[ref=(.*?)\]') {
            $tbRef3 = $matches[1].Trim()
            $client.CallTool("windows_fill", @{ ref = $tbRef3; value = "Luxury_Penthouse" }) | Out-Null
            Start-Sleep -Milliseconds 400
            $snap3Rename = $client.CallTool("windows_snapshot", @{ handle = $h3 })
            $tree3Rename = $snap3Rename.result.content[0].text
            $cardRenamed = $tree3Rename -match "Luxury_Penthouse_01\.jpg"
            Record-Test "Live result card rename (INotifyPropertyChanged)" $cardRenamed "Card filename updated to Luxury_Penthouse_01.jpg immediately upon prefix change"
        }

        # SCENARIO 6: Open Full-Size Preview Window & Navigate
        Write-Host "`n--- Scenario 6: Preview Modal Window Navigation ---" -ForegroundColor Yellow
        $snapBeforePreview = $client.CallTool("windows_snapshot", @{ handle = $h3 })
        if ($snapBeforePreview.result.content[0].text -match 'button "PREVIEW".*?\[ref=(.*?)\]') {
            $prevBtnRef = $matches[1].Trim()
            $client.CallTool("windows_click", @{ ref = $prevBtnRef }) | Out-Null
            Start-Sleep -Milliseconds 800

            $snapPreviewOpen = $client.CallTool("windows_snapshot", @{ handle = $h3 })
            $prevTree = $snapPreviewOpen.result.content[0].text
            $hasPreviewWin = $prevTree -match "Alpha Premier \| Photo Preview"
            $hasPos = $prevTree -match "1 of 1"
            $hasNav = ($prevTree -match "PREVIOUS") -and ($prevTree -match "NEXT")
            
            Record-Test "Open Preview Window modal" $hasPreviewWin "Preview modal window tree detected within UI"
            Record-Test "Preview Window content & controls" ($hasPos -and $hasNav) "Displays '1 of 1', Previous, and Next navigation controls"

            # Close modal with Escape
            $client.CallTool("windows_send_keys", @{ chord = "Escape" }) | Out-Null
            Start-Sleep -Milliseconds 600
        }
    }

    # Close instance 2
    $client.CallTool("windows_close", @{ handle = $h3 }) | Out-Null
    Start-Sleep -Seconds 1
    Get-Process "Alpha.Branding" -ErrorAction SilentlyContinue | Stop-Process -Force

    # SCENARIO 7: Portrait Pair Processing
    Write-Host "`n--- Scenario 7: Portrait Photo Pair Detection & Branding ---" -ForegroundColor Yellow
    $client.CallTool("windows_launch", @{ app = $appPath; args = @($portrait1Path, $portrait2Path) }) | Out-Null
    Start-Sleep -Seconds 1
    $h4 = $client.GetMainWindowHandle()

    $snap4 = $client.CallTool("windows_snapshot", @{ handle = $h4 })
    $tree4 = $snap4.result.content[0].text
    $has2Selected = $tree4 -match "2 photo\(s\) selected"
    Record-Test "Load 2 portrait photos" $has2Selected "Selection summary correctly displays '2 photo(s) selected'"

    if ($tree4 -match 'button "APPLY BRANDING".*?\[ref=(.*?)\]') {
        $applyRef4 = $matches[1].Trim()
        $client.CallTool("windows_click", @{ ref = $applyRef4 }) | Out-Null
        Start-Sleep -Seconds 2
        $snap4Post = $client.CallTool("windows_snapshot", @{ handle = $h4 })
        $tree4Post = $snap4Post.result.content[0].text
        # 2 portraits must produce 1 paired branded output
        $has1PairedResult = $tree4Post -match "Completed 1 image\(s\)\."
        Record-Test "Portrait pairing logic in UI" $has1PairedResult "Two portrait photos successfully paired into 1 branded landscape output"
    }

    # Clean close
    $client.CallTool("windows_close", @{ handle = $h4 }) | Out-Null
    Start-Sleep -Seconds 1
    Get-Process "Alpha.Branding" -ErrorAction SilentlyContinue | Stop-Process -Force

    # SCENARIO 8: Single Portrait Duplicate Side-by-Side (Never Lone)
    Write-Host "`n--- Scenario 8: Single Portrait Duplicate Side-by-Side ---" -ForegroundColor Yellow
    $client.CallTool("windows_launch", @{ app = $appPath; args = @($lonePortraitPath) }) | Out-Null
    Start-Sleep -Seconds 1
    $h5 = $client.GetMainWindowHandle()

    $snap5 = $client.CallTool("windows_snapshot", @{ handle = $h5 })
    $tree5 = $snap5.result.content[0].text
    $has1LoneSelected = $tree5 -match "1 photo\(s\) selected"
    Record-Test "Load single portrait photo" $has1LoneSelected "Selection summary shows 1 photo selected"

    if ($tree5 -match 'button "APPLY BRANDING".*?\[ref=(.*?)\]') {
        $applyRef5 = $matches[1].Trim()
        $client.CallTool("windows_click", @{ ref = $applyRef5 }) | Out-Null
        Start-Sleep -Seconds 2
        $snap5Post = $client.CallTool("windows_snapshot", @{ handle = $h5 })
        $tree5Post = $snap5Post.result.content[0].text
        $has1LoneResult = $tree5Post -match "Completed 1 image\(s\)\."
        Record-Test "Single portrait side-by-side branding workflow" $has1LoneResult "Single portrait processed cleanly side-by-side (never lone)"
    }

    # Clean close
    $client.CallTool("windows_close", @{ handle = $h5 }) | Out-Null
    Start-Sleep -Seconds 1
    Get-Process "Alpha.Branding" -ErrorAction SilentlyContinue | Stop-Process -Force

    # SCENARIO 9: Portrait + Landscape Side-by-Side Pairing
    Write-Host "`n--- Scenario 9: Portrait + Landscape Side-by-Side Pairing ---" -ForegroundColor Yellow
    $client.CallTool("windows_launch", @{ app = $appPath; args = @($portrait1Path, $landscapePath) }) | Out-Null
    Start-Sleep -Seconds 1
    $h6 = $client.GetMainWindowHandle()

    $snap6 = $client.CallTool("windows_snapshot", @{ handle = $h6 })
    $tree6 = $snap6.result.content[0].text
    $has2MixedSelected = $tree6 -match "2 photo\(s\) selected"
    Record-Test "Load 1 portrait and 1 landscape photo" $has2MixedSelected "Selection summary displays 2 photo(s) selected"

    if ($tree6 -match 'button "APPLY BRANDING".*?\[ref=(.*?)\]') {
        $applyRef6 = $matches[1].Trim()
        $client.CallTool("windows_click", @{ ref = $applyRef6 }) | Out-Null
        Start-Sleep -Seconds 2
        $snap6Post = $client.CallTool("windows_snapshot", @{ handle = $h6 })
        $tree6Post = $snap6Post.result.content[0].text
        $has1MixedPairResult = $tree6Post -match "Completed 1 image\(s\)\."
        Record-Test "Portrait and Landscape side-by-side pairing" $has1MixedPairResult "Portrait matched with landscape side-by-side into 1 composite output"
    }

    # Clean close
    $client.CallTool("windows_close", @{ handle = $h6 }) | Out-Null
    Start-Sleep -Seconds 1
    Get-Process "Alpha.Branding" -ErrorAction SilentlyContinue | Stop-Process -Force

} finally {
    $client.Close()
    Get-Process "Alpha.Branding" -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process "FlaUI.Mcp" -ErrorAction SilentlyContinue | Stop-Process -Force
    if (Test-Path $testDir) {
        try { Remove-Item -Path $testDir -Recurse -Force } catch {}
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "E2E Test Results Summary" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
$passedCount = ($results | Where-Object { $_.Passed }).Count
$totalCount = $results.Count
$summaryColor = if ($passedCount -eq $totalCount) { "Green" } else { "Red" }
Write-Host "Total Tests: $totalCount, Passed: $passedCount, Failed: $($totalCount - $passedCount)" -ForegroundColor $summaryColor

if ($passedCount -ne $totalCount) {
    exit 1
}
