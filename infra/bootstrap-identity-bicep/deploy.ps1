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

$deploymentName = "rentoom-$Environment-github-oidc-bootstrap"

$validateArguments = @(
    'deployment', 'sub', 'validate',
    '--location', $deploymentLocation,
    '--template-file', $templateFile,
    '--parameters', "@$parameterFile"
)

$createArguments = @(
    'deployment', 'sub', 'create',
    '--name', $deploymentName,
    '--location', $deploymentLocation,
    '--template-file', $templateFile,
    '--parameters', "@$parameterFile"
)

Write-Host "Environment: $Environment"
Write-Host "Template file: $templateFile"
Write-Host "Parameter file: $parameterFile"
Write-Host "Deployment location: $deploymentLocation"
Write-Host "Deployment name: $deploymentName"
Write-Host "Operation: $Operation"

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
