[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'prod')]
    [string]$Environment,

    [Parameter(Mandatory = $true)]
    [ValidateSet('validate', 'create', 'validate-create')]
    [string]$Operation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$templateFile = Join-Path $scriptRoot 'main.bicep'
$parameterFile = Join-Path $scriptRoot ("main.{0}.parameters.json" -f $Environment)

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI ('az') is not installed or not available in PATH."
}

if (-not (Test-Path -Path $templateFile -PathType Leaf)) {
    throw "Template file not found: $templateFile"
}

if (-not (Test-Path -Path $parameterFile -PathType Leaf)) {
    throw "Parameter file not found for environment '$Environment': $parameterFile"
}

$parameterFileContent = Get-Content -Raw -Path $parameterFile | ConvertFrom-Json
$deploymentLocation = $parameterFileContent.parameters.location.value

if ([string]::IsNullOrWhiteSpace($deploymentLocation)) {
    throw "The parameter file '$parameterFile' does not define parameters.location.value."
}

# Replace these placeholder defaults before running a real deployment.
$secretParameters = [ordered]@{
    staywellDbAppPassword      = 'PROVIDE_STAYWELL_DB_PASSWORD'
    idoBookingApiPassword      = 'PROVIDE_IDOBOOKING_API_PASSWORD'
    rentoomAppDbPassword       = 'PROVIDE_RENTOOM_APP_DB_PASSWORD'
    tpayClientSecret           = 'PROVIDE_TPAY_CLIENT_SECRET'
    tpayMerchantSecurityCode   = 'PROVIDE_TPAY_MERCHANT_SECURITY_CODE'
    ttlockClientSecret         = 'PROVIDE_TTLOCK_CLIENT_SECRET'
    ttlockPassword             = 'PROVIDE_TTLOCK_PASSWORD'
    staywellGithubRepositoryToken = 'PROVIDE_STAYWELL_GITHUB_REPOSITORY_TOKEN'
}

$placeholderValues = @(
    '<STAYWELL_DB_PASSWORD>',
    '<IDOBOOKING_API_PASSWORD>',
    '<RENTOOM_APP_DB_PASSWORD>',
    '<TPAY_CLIENT_SECRET>',
    '<TPAY_MERCHANT_SECURITY_CODE>',
    '<TTLOCK_CLIENT_SECRET>',
    '<TTLOCK_PASSWORD>',
    '<STAYWELL_GITHUB_REPOSITORY_TOKEN>',
    'STAYWELL_DB_PASSWORD',
    'IDOBOOKING_API_PASSWORD',
    'RENTOOM_APP_DB_PASSWORD',
    'TPAY_CLIENT_SECRET',
    'TPAY_MERCHANT_SECURITY_CODE',
    'TTLOCK_CLIENT_SECRET',
    'TTLOCK_PASSWORD',
    'STAYWELL_GITHUB_REPOSITORY_TOKEN'
)

$placeholderKeys = @(
    $secretParameters.GetEnumerator() |
    Where-Object { $placeholderValues -contains $_.Value } |
    ForEach-Object { $_.Key }
)

if ($placeholderKeys.Count -gt 0) {
    throw "Replace the placeholder secret values in deploy.ps1 before running deployment. Missing values: $($placeholderKeys -join ', ')"
}

$secretParameterDocument = [ordered]@{
    '$schema' = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters = [ordered]@{}
}

foreach ($entry in $secretParameters.GetEnumerator()) {
    $secretParameterDocument.parameters[$entry.Key] = @{
        value = $entry.Value
    }
}

$secretParameterFile = Join-Path ([System.IO.Path]::GetTempPath()) ("rentoom.{0}.secrets.{1}.parameters.json" -f $Environment, [Guid]::NewGuid().ToString('N'))
$secretParameterDocument | ConvertTo-Json -Depth 10 | Set-Content -Path $secretParameterFile -Encoding UTF8

$deploymentName = "rentoom-$Environment-bootstrap"

$validateArguments = @(
    'deployment', 'sub', 'validate',
    '--location', $deploymentLocation,
    '--template-file', $templateFile,
    '--parameters', "@$parameterFile", "@$secretParameterFile"
)

$createArguments = @(
    'deployment', 'sub', 'create',
    '--name', $deploymentName,
    '--location', $deploymentLocation,
    '--template-file', $templateFile,
    '--parameters', "@$parameterFile", "@$secretParameterFile"
)

Write-Host "Environment: $Environment"
Write-Host "Template file: $templateFile"
Write-Host "Parameter file: $parameterFile"
Write-Host "Deployment location: $deploymentLocation"
Write-Host "Deployment name: $deploymentName"
Write-Host "Operation: $Operation"

try {
    if ($Operation -in @('validate', 'validate-create')) {
        Write-Host ''
        Write-Host 'Validating deployment...'
        & az @validateArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Azure deployment validation failed."
        }
    }

    if ($Operation -in @('create', 'validate-create')) {
        Write-Host ''
        Write-Host 'Creating deployment...'
        & az @createArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Azure deployment creation failed."
        }
    }
}
finally {
    if (Test-Path -Path $secretParameterFile -PathType Leaf) {
        Remove-Item -Path $secretParameterFile -Force
    }
}
