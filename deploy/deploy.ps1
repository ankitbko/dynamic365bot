<#
 .SYNOPSIS
    Deploys a template to Azure

 .DESCRIPTION
    Deploys an Azure Resource Manager template

 .PARAMETER subscriptionId
    The subscription id where the template will be deployed.

 .PARAMETER resourceGroupName
    The resource group where the template will be deployed. Can be the name of an existing or a new resource group.

 .PARAMETER resourceGroupLocation
    Optional, a resource group location. If specified, will try to create a new resource group in this location. If not specified, assumes resource group is existing.

 .PARAMETER deploymentName
    The deployment name.

 .PARAMETER templateFilePath
    Optional, path to the template file. Defaults to template.json.

 .PARAMETER parametersFilePath
    Optional, path to the parameters file. Defaults to parameters.json. If file is not found, will prompt for parameter values based on template.
#>

param(
 [Parameter(Mandatory=$True)]
 [string]
 $subscriptionId,

 [Parameter(Mandatory=$True)]
 [string]
 $resourceGroupName,

 [Parameter(Mandatory=$True)]
 [string]
 $resourceGroupLocation,

 [Parameter(Mandatory=$True)]
 [string]
 $deploymentName,
 
 [Parameter(Mandatory=$True)]
 [string]
 $adAppName,
 
 [Parameter(Mandatory=$True)]
 [object]
 $templateParameters
)

Set-ExecutionPolicy -Scope Process -ExecutionPolicy Unrestricted -Force

if(!(Get-PackageProvider -Name Nuget) -Or ((Get-PackageProvider -Name Nuget).Version -lt [System.Version]"2.8.5.208")) {
	Write-Host "Nuget does not exist or version is less than required" -ForegroundColor DarkYellow
	Install-PackageProvider -Name Nuget -MinimumVersion 2.8.5.208 -Scope CurrentUser -Force
    Write-Host "Nuget Installed" -ForegroundColor DarkYellow
}

if(!(Get-PSRepository -Name "PSGallery")) {
    Write-Host "Setting PSGallery as repository" -ForegroundColor DarkYellow
	Set-PSRepository -Name PSGallery -InstallationPolicy Trusted
}

if(!(Get-Module -Listavailable -Name AzureRM)) {
    Write-Host "Installing AzureRM" -ForegroundColor DarkYellow
	Install-Module -Name AzureRM -AllowClobber
}

if(!(Get-Module -Listavailable -Name AzureRM.Profile)) {
    Write-Host "Installing AzureRM.Profile" -ForegroundColor DarkYellow
	Install-Module -Name AzureRM.Profile -AllowClobber
}

if(!(Get-Module -Listavailable -Name AzureAD)) {
    Write-Host "Installing AzureAD" -ForegroundColor DarkYellow
	Install-Module -Name AzureAD -AllowClobber
}

$ScriptPath = Split-Path $MyInvocation.MyCommand.Path

# Deploy Resources
	$deploymentResult = & "$ScriptPath\deployArm.ps1" $subscriptionId $resourceGroupName $resourceGroupLocation $deploymentName $templateParameters

Write-Host "Deployment Done. Deployment Result:" -ForegroundColor Green
Write-Host ($deploymentResult | Out-String) -ForegroundColor Green

$luisSubscriptionKey = $deploymentResult.Outputs.luisKey.Value
$botSiteObject = $deploymentResult.Outputs.item("botSite").Value.ToString() | ConvertFrom-Json
$botHostName = $botSiteObject.properties.defaultHostname
$botCallbackUrl = "https://$botHostName/api/OAuthCallback"

# Register AzureAD V1 App.
$adAppResult = & "$ScriptPath\adappregister.ps1" $adAppName $botCallbackUrl

Write-Host "Azure App created"
Write-Host ($adAppResult | Out-String) -ForegroundColor Green

# Update WebApp Config
Write-Host "Trying to update AppSettings"
$botApp = Get-AzureRmWebapp -Name $botSiteObject.properties.name -ResourceGroup $botSiteObject.resourceGroupName
$AppSettings =	@{
	"ActiveDirectory.RedirectUrl" = "$botCallbackUrl";
	"ActiveDirectory.ClientId" = "$($adAppResult.appId)";
	"ActiveDirectory.ClientSecret" = "$($adAppResult.appSecret)" 
}
foreach ($pair in $botApp.SiteConfig.AppSettings) { $AppSettings[$pair.Name] = $pair.Value }

Set-AzureRmWebapp -Name $botSiteObject.properties.name -ResourceGroup $botSiteObject.resourceGroupName -AppSettings $AppSettings

Write-Host "Appsettings Updated"
Write-Host ($AppSettings | Out-String) -ForegroundColor Green

# Create a App Insight API key and set it to Bot Registration
Write-Host "Generating AppInsight API Key"
$apiKeyDescription="DynamicsBotkey"
$permissions = @("ReadTelemetry", "WriteAnnotations")
$appInsightName = $deploymentResult.Outputs.appInsightName.Value
$appInsightApiKey = New-AzureRmApplicationInsightsApiKey -ResourceGroupName $resourceGroupName -Name $appInsightName -Description $apiKeyDescription -Permissions $permissions
Write-Host ($appInsightApiKey | Out-String) -ForegroundColor Green

Write-Host "Setting AppInsight API Key in Bot Registration"
$botRegistrationName = $deploymentResult.Outputs.botRegistrationName.Value
$botRegistration = Get-AzureRmResource -Name $botRegistrationName -ResourceGroup $resourceGroupName -ExpandProperties
$botRegistration.Properties | Add-Member developerAppInsightsApiKey $appInsightApiKey.ApiKey
Write-Host "New Bot Registration Property: $($botRegistration.Properties | Out-String)" -ForegroundColor Green
$botRegistration | Set-AzureRmResource -Force
Write-Host "AppInsight API Key Set"

Write-Host "---------------------------------" -ForegroundColor Green
Write-Host "LUIS Key: $luisSubscriptionKey" -ForegroundColor Green
Write-Host "---------------------------------" -ForegroundColor Green
