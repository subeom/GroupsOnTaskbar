[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

$subject = 'CN=Taskbar Groups Development'
$friendlyName = 'Taskbar Groups Development'
$minimumExpiration = (Get-Date).AddDays(30)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactRoot = Join-Path $repoRoot 'artifacts\package'
$project = Join-Path $repoRoot 'src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj'
$releaseRoot = Join-Path $repoRoot 'src\GroupsOnTaskbar.App\bin\Release'
$appPackagesRoot = Join-Path $repoRoot 'src\GroupsOnTaskbar.App\AppPackages'
$outputPackage = Join-Path $artifactRoot 'TaskbarGroups_0.1.0.0_x64.msix'
$cerPath = Join-Path $artifactRoot 'TaskbarGroupsDevelopment.cer'

function Test-DevelopmentCertificate {
    param(
        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate
    )

    if (($Certificate.Subject -ne $subject) -or ($Certificate.FriendlyName -ne $friendlyName) -or (-not $Certificate.HasPrivateKey) -or ($Certificate.NotAfter -le $minimumExpiration)) {
        return $false
    }

    $keyUsage = $Certificate.Extensions |
        Where-Object { $_ -is [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension] } |
        Select-Object -First 1
    if ((-not $keyUsage) -or (-not $keyUsage.KeyUsages.HasFlag([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature))) {
        return $false
    }

    $enhancedKeyUsage = $Certificate.Extensions |
        Where-Object { $_ -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension] } |
        Select-Object -First 1
    if (-not $enhancedKeyUsage) {
        return $false
    }

    $hasCodeSigningUsage = $false
    foreach ($oid in $enhancedKeyUsage.EnhancedKeyUsages) {
        if ($oid.Value -eq '1.3.6.1.5.5.7.3.3') {
            $hasCodeSigningUsage = $true
            break
        }
    }

    if (-not $hasCodeSigningUsage) {
        return $false
    }

    $basicConstraints = $Certificate.Extensions |
        Where-Object { $_ -is [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension] } |
        Select-Object -First 1
    if ((-not $basicConstraints) -or $basicConstraints.CertificateAuthority) {
        return $false
    }

    return $true
}

function Ensure-CertificateInStore {
    param(
        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.X509Certificates.StoreName] $StoreName,

        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate,

        [System.Security.Cryptography.X509Certificates.StoreLocation] $StoreLocation = [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    )

    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        $StoreName,
        $StoreLocation)

    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $matches = $store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $Certificate.Thumbprint,
            $false)

        if ($matches.Count -eq 0) {
            $store.Add($Certificate)
        }
    }
    finally {
        $store.Close()
    }
}

function Test-CertificateInStore {
    param(
        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.X509Certificates.StoreName] $StoreName,

        [Parameter(Mandatory = $true)]
        [string] $Thumbprint,

        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.X509Certificates.StoreLocation] $StoreLocation
    )

    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        $StoreName,
        $StoreLocation)

    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        return $store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $Thumbprint,
            $false).Count -gt 0
    }
    catch {
        return $false
    }
    finally {
        $store.Close()
    }
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$identity).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

if (Test-Path $appPackagesRoot) {
    Get-ChildItem $appPackagesRoot -Recurse -Force | ForEach-Object {
        if ($_.Attributes.HasFlag([System.IO.FileAttributes]::ReadOnly)) {
            $_.Attributes = $_.Attributes -band (-bnot [System.IO.FileAttributes]::ReadOnly)
        }
    }

    Remove-Item $appPackagesRoot -Recurse -Force
}

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { Test-DevelopmentCertificate -Certificate $_ } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -FriendlyName $friendlyName `
        -KeyUsage DigitalSignature `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -NotAfter (Get-Date).AddYears(2) `
        -TextExtension @(
            '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
            '2.5.29.19={text}'
        )
}

Export-Certificate -Cert $certificate -FilePath $cerPath -Force | Out-Null

$publicCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($cerPath)
Ensure-CertificateInStore -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople) -Certificate $publicCertificate
Ensure-CertificateInStore -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::Root) -Certificate $publicCertificate

# Windows validates MSIX signatures against machine-scoped trust, so CurrentUser
# trust alone is not enough for Add-AppxPackage to succeed.
$machineTrusted = Test-CertificateInStore `
    -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople) `
    -Thumbprint $certificate.Thumbprint `
    -StoreLocation ([System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)

if (-not $machineTrusted) {
    if (Test-IsAdministrator) {
        Ensure-CertificateInStore `
            -StoreName ([System.Security.Cryptography.X509Certificates.StoreName]::TrustedPeople) `
            -Certificate $publicCertificate `
            -StoreLocation ([System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
        $machineTrusted = $true
    }
    else {
        Write-Warning @"
The signing certificate is not trusted for the local machine, so installation will fail with 0x800B0109.
Run this command once from an elevated PowerShell prompt:
    Import-Certificate -FilePath '$cerPath' -CertStoreLocation Cert:\LocalMachine\TrustedPeople
"@
    }
}

& dotnet publish $project `
    -c Release `
    -r win-x64 `
    '-p:Platform=x64' `
    '-p:GenerateAppxPackageOnBuild=true' `
    '-p:AppxPackageSigningEnabled=true' `
    "-p:PackageCertificateThumbprint=$($certificate.Thumbprint)"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$package = Get-ChildItem @($appPackagesRoot, $releaseRoot) -Recurse -Filter *.msix -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if (-not $package) {
    throw 'The build completed without producing an MSIX package.'
}

Copy-Item $package.FullName $outputPackage -Force
Write-Output (Resolve-Path $outputPackage).Path
