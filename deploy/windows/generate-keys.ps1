$ErrorActionPreference = 'Stop'

Write-Host "=== Lo Revival - RSA Keypair Generator ===" -ForegroundColor Cyan

$storageDir = "C:\lo\storage"
if (-not (Test-Path $storageDir)) {
    New-Item -ItemType Directory -Path $storageDir -Force | Out-Null
}

$tmp = Join-Path $env:TEMP "lo-keygen-$(Get-Random)"
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

try {
    Push-Location $tmp
    & dotnet new console --force | Out-Null

    $csharp = @'
using System;
using System.IO;
using System.Security.Cryptography;

var storageDir = @"C:\lo\storage";
Directory.CreateDirectory(storageDir);

using var rsa = RSA.Create(1024);

var pkcs1 = rsa.ExportRSAPrivateKey();
var pem = "-----BEGIN RSA PRIVATE KEY-----\r\n" +
          Convert.ToBase64String(pkcs1, Base64FormattingOptions.InsertLineBreaks) +
          "\r\n-----END RSA PRIVATE KEY-----\r\n";
File.WriteAllText(Path.Combine(storageDir, "privateKey1024.pem"), pem);

var pubDer = rsa.ExportSubjectPublicKeyInfo();
var pubBlob = Convert.ToBase64String(pubDer);
File.WriteAllText(Path.Combine(storageDir, "publicKeyBlob.txt"), pubBlob);

Console.WriteLine("Keypair generated successfully.");
'@
    Set-Content -Path "Program.cs" -Value $csharp

    & dotnet run --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet run failed with exit code $LASTEXITCODE"
    }
} finally {
    Pop-Location
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

$privFile = Join-Path $storageDir "privateKey1024.pem"
$pubFile = Join-Path $storageDir "publicKeyBlob.txt"

Write-Host ""
Write-Host "Private key written to: $privFile" -ForegroundColor Green
Write-Host "  Size: $((Get-Item $privFile).Length) bytes"
Write-Host "Public blob written to: $pubFile" -ForegroundColor Green
Write-Host "  Size: $((Get-Item $pubFile).Length) bytes"

$blob = Get-Content $pubFile -Raw
$blobClean = $blob.Trim()
Write-Host "  Blob (first 60 chars): $($blobClean.Substring(0, [Math]::Min(60, $blobClean.Length)))..."
Write-Host ""
Write-Host "Verify private key header:" -ForegroundColor Yellow
Get-Content $privFile -TotalCount 1
