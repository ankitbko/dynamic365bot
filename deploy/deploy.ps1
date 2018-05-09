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

 .PARAMETER adAppName
    Name of the Azure AD application that will be created. Native app will have _native suffix

 .PARAMETER luisAuthoringKey
    LUIS authoring key.

 .PARAMETER templateParameters
    Parameters to be passed to ARM template.
#>

[cmdletbinding()]
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
 [string]
 $luisAuthoringKey,
 
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

Import-Module AzureRM
Import-Module AzureRM.Profile
Import-Module AzureAD
$ScriptPath = Split-Path $MyInvocation.MyCommand.Path

# Deploy Resources
$deploymentResult = & "$ScriptPath\deployArm.ps1" $subscriptionId $resourceGroupName $resourceGroupLocation $deploymentName $templateParameters

Write-Verbose "Deployment Result:"
Write-Verbose ($deploymentResult | Out-String)

$luisSubscriptionKey = $deploymentResult.Outputs.luisKey.Value
$botSiteObject = $deploymentResult.Outputs.item("botSite").Value.ToString() | ConvertFrom-Json
$botHostName = $botSiteObject.properties.defaultHostname
$botCallbackUrl = "https://$botHostName/Callback"

# Register AzureAD V1 App.
Write-Host "Creating Azure App"
$adAppResult = & "$ScriptPath\adappregister.ps1" $adAppName $botCallbackUrl $true "ms-dynamics-app://luis"

Write-Verbose "Azure App created"
Write-Verbose ($adAppResult | Out-String)

# Create and train luis app
Write-Host "Training LUIS"
Write-Host "Login using CRM Credentials" -ForegroundColor Yellow
Write-Verbose "Calling LUIS Trainer with following parameters: 
--crmurl $($templateParameters.crmurl) 
--redirecturl $($adAppResult.nativeReplyUrl)
--clientid $($adAppResult.nativeAppId)`
--templatepath $("$ScriptPath\luistemplate\d365bot.json")
--authoringkey $($luisAuthoringKey)"

Start-Sleep -m 2000
$luisAppId = & "$ScriptPath\trainluis\Microsoft.Dynamics.BotFramework.Luis.exe" `
    --crmurl $templateParameters.crmurl`
    --redirecturl $adAppResult.nativeReplyUrl `
    --clientid $adAppResult.nativeAppId `
    --templatepath "$ScriptPath\luistemplate\d365bot.json" `
    --authoringkey $luisAuthoringKey

#update luis appid, ad app clientid and client secret in keyvault
Write-Host "Setting Luis AppId, AD App ClientId and Secret in Key Vault"
$userId = (Get-AzureRmContext).Account.Id
$userObjectId = (Get-AzureADUser -ObjectId $userId).ObjectId
$accessPolicyResult = Set-AzureRmKeyVaultAccessPolicy -VaultName $deploymentResult.Outputs.keyVaultName.Value -ObjectId $userObjectId -PermissionsToSecrets set,get,list -PassThru
Write-Verbose ($accessPolicyResult | Out-String)
$luisSecret = ConvertTo-SecureString -String $luisAppId -AsPlainText -Force
Set-AzureKeyVaultSecret -VaultName $deploymentResult.Outputs.keyVaultName.Value -Name 'LuisModelId' -SecretValue $luisSecret

$clientId = ConvertTo-SecureString -String $luisAppId -AsPlainText -Force
Set-AzureKeyVaultSecret -VaultName $deploymentResult.Outputs.keyVaultName.Value -Name 'ActiveDirectoryClientId' -SecretValue $clientId

$clientSecret = ConvertTo-SecureString -String $luisAppId -AsPlainText -Force
Set-AzureKeyVaultSecret -VaultName $deploymentResult.Outputs.keyVaultName.Value -Name 'ActiveDirectoryClientSecret' -SecretValue $clientSecret

# Update WebApp Config
Write-Host "Updating AppSettings with redirect url"
$botApp = Get-AzureRmWebapp -Name $botSiteObject.properties.name -ResourceGroup $botSiteObject.resourceGroupName
$AppSettings =	@{
	"ActiveDirectory.RedirectUrl" = "$botCallbackUrl";
}
foreach ($pair in $botApp.SiteConfig.AppSettings) { $AppSettings[$pair.Name] = $pair.Value }

Set-AzureRmWebapp -Name $botSiteObject.properties.name -ResourceGroup $botSiteObject.resourceGroupName -AppSettings $AppSettings

Write-Verbose "Appsettings Updated"
Write-Verbose ($AppSettings | Out-String)

# Create a App Insight API key and set it to Bot Registration
Write-Host "Generating AppInsight API Key"
$apiKeyDescription="DynamicsBotkey"
$permissions = @("ReadTelemetry", "WriteAnnotations")
$appInsightName = $deploymentResult.Outputs.appInsightName.Value
$appInsightApiKey = New-AzureRmApplicationInsightsApiKey -ResourceGroupName $resourceGroupName -Name $appInsightName -Description $apiKeyDescription -Permissions $permissions
Write-Verbose ($appInsightApiKey | Out-String)

Write-Host "Setting AppInsight API Key in Bot Registration"
$botRegistrationName = $deploymentResult.Outputs.botRegistrationName.Value
$botRegistration = Get-AzureRmResource -Name $botRegistrationName -ResourceGroup $resourceGroupName -ExpandProperties
$botRegistration.Properties | Add-Member developerAppInsightsApiKey $appInsightApiKey.ApiKey
Write-Verbose "New Bot Registration Property: $($botRegistration.Properties | Out-String)"
$botRegistration | Set-AzureRmResource -Force
Write-Host "AppInsight API Key Set"

Write-Host "---------------------------------" -ForegroundColor Green
Write-Host "LUIS Key: $luisSubscriptionKey" -ForegroundColor Green
Write-Host "---------------------------------" -ForegroundColor Green
