Import-Module PKI -ErrorAction SilentlyContinue
$cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=TrumpBsAlert' -KeyUsage DigitalSignature -FriendlyName 'TrumpBsAlert Dev Cert' -CertStoreLocation 'Cert:\CurrentUser\My' -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
$pw = ConvertTo-SecureString 'TrumpBsAlert2026' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "$PSScriptRoot\src\TrumpBsAlert\TrumpBsAlert.pfx" -Password $pw
Export-Certificate -Cert $cert -FilePath "$PSScriptRoot\TrumpBsAlert.cer" -Type CERT
Write-Host "Thumbprint: $($cert.Thumbprint)"
