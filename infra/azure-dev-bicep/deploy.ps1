[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'prod')]
    [string]$Environment,

    [Parameter(Mandatory = $true)]
    [ValidateSet('validate', 'create', 'validate-create')]
    [string]$Operation,

    [Parameter(Mandatory = $true)]
    [string]$SecretParameterFile
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

$resolvedSecretParameterFile = Resolve-Path -LiteralPath $SecretParameterFile -ErrorAction SilentlyContinue
if (-not $resolvedSecretParameterFile) {
    throw "Secret parameter file not found: $SecretParameterFile"
}

$secretParameterFile = $resolvedSecretParameterFile.Path

$parameterFileContent = Get-Content -Raw -Path $parameterFile | ConvertFrom-Json
$secretParameterFileContent = Get-Content -Raw -Path $secretParameterFile | ConvertFrom-Json
$deploymentLocation = $parameterFileContent.parameters.location.value
$resourceGroupName = $parameterFileContent.parameters.resourceGroupName.value
$functionAppName = $parameterFileContent.parameters.staywellApiFunctionName.value

$expectedSubscriptionByEnvironment = @{
    dev  = 'c079185e-8eeb-40dc-90b4-01cee2fa7e21'
    prod = '687d8cbd-fea7-4ae4-a70f-8cb4629c43c6'
}

$expectedSubscriptionId = $expectedSubscriptionByEnvironment[$Environment]

if ([string]::IsNullOrWhiteSpace($deploymentLocation)) {
    throw "The parameter file '$parameterFile' does not define parameters.location.value."
}

if ([string]::IsNullOrWhiteSpace($resourceGroupName)) {
    throw "The parameter file '$parameterFile' does not define parameters.resourceGroupName.value."
}

if ([string]::IsNullOrWhiteSpace($functionAppName)) {
    throw "The parameter file '$parameterFile' does not define parameters.staywellApiFunctionName.value."
}

$requiredSecretParameters = @(
    'staywellDbAppPassword',
    'idoBookingApiPassword',
    'rentoomAppDbPassword',
    'tpayClientSecret',
    'tpayMerchantSecurityCode',
    'ttlockClientSecret',
    'ttlockPassword',
    'staywellGithubRepositoryToken'
)

$missingSecretParameters = @(
    foreach ($parameterName in $requiredSecretParameters) {
        $parameterValue = $secretParameterFileContent.parameters.$parameterName.value
        if ([string]::IsNullOrWhiteSpace([string]$parameterValue)) {
            $parameterName
        }
    }
)

if ($missingSecretParameters.Count -gt 0) {
    throw "The secret parameter file '$secretParameterFile' is missing values for: $($missingSecretParameters -join ', ')"
}

$currentAccount = az account show -o json | ConvertFrom-Json

Write-Host ''
Write-Host 'Available Azure subscriptions:'
& az account list -o table
if ($LASTEXITCODE -ne 0) {
    throw "Unable to list Azure subscriptions."
}

Write-Host ''
Write-Host "Current Azure subscription: $($currentAccount.name) [$($currentAccount.id)]"
Write-Host "Expected Azure subscription for '$Environment': $expectedSubscriptionId"

if ($currentAccount.id -ne $expectedSubscriptionId) {
    throw "Wrong Azure subscription selected. Run: az account set --subscription $expectedSubscriptionId"
}

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
Write-Host "Secret parameter file: $secretParameterFile"
Write-Host "Deployment location: $deploymentLocation"
Write-Host "Subscription: $expectedSubscriptionId"
Write-Host "Deployment name: $deploymentName"
Write-Host "Resource group: $resourceGroupName"
Write-Host "Function App: $functionAppName"
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

    Write-Host ''
    Write-Host "Restarting Function App '$functionAppName' to refresh auth settings..."
    & az functionapp restart --resource-group $resourceGroupName --name $functionAppName
    if ($LASTEXITCODE -ne 0) {
        throw "Azure Function App restart failed."
    }
}
