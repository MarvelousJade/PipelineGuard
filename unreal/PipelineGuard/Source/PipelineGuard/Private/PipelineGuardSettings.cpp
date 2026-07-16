#include "PipelineGuardSettings.h"

#define LOCTEXT_NAMESPACE "PipelineGuardSettings"

UPipelineGuardSettings::UPipelineGuardSettings()
{
    ScanPackagePathRoots = {TEXT("/Game")};
    AssetNameRegex = TEXT("^[A-Za-z][A-Za-z0-9_]*$");
    AllowedPackagePathPrefixes = {TEXT("/Game")};

    RequiredPrefixesByClass.Add(TEXT("Texture2D"), TEXT("T_"));
    RequiredPrefixesByClass.Add(TEXT("Material"), TEXT("M_"));
    RequiredPrefixesByClass.Add(TEXT("MaterialInstanceConstant"), TEXT("MI_"));
    RequiredPrefixesByClass.Add(TEXT("Blueprint"), TEXT("BP_"));
    RequiredPrefixesByClass.Add(TEXT("StaticMesh"), TEXT("SM_"));
    RequiredPrefixesByClass.Add(TEXT("SkeletalMesh"), TEXT("SK_"));
    RequiredPrefixesByClass.Add(TEXT("DataTable"), TEXT("DT_"));
}

FName UPipelineGuardSettings::GetCategoryName() const
{
    return TEXT("Plugins");
}

FText UPipelineGuardSettings::GetSectionText() const
{
    return LOCTEXT("SectionText", "Pipeline Guard");
}

FPipelineGuardRuleConfig UPipelineGuardSettings::MakeRuleConfig() const
{
    FPipelineGuardRuleConfig Config;
    Config.ScanPackagePathRoots = ScanPackagePathRoots;
    Config.bValidateAssetNames = bValidateAssetNames;
    Config.AssetNameRegex = AssetNameRegex;
    Config.bEnforceClassPrefixes = bEnforceClassPrefixes;
    Config.RequiredPrefixesByClass = RequiredPrefixesByClass;
    Config.bValidateAllowedPaths = bValidateAllowedPaths;
    Config.AllowedPackagePathPrefixes = AllowedPackagePathPrefixes;
    Config.bValidateTexturePowerOfTwo = bValidateTexturePowerOfTwo;
    Config.bValidateTextureMaximumDimension = bValidateTextureMaximumDimension;
    Config.MaximumTextureDimension = MaximumTextureDimension;
    Config.bValidateDataTableRowStruct = bValidateDataTableRowStruct;
    Config.AllowedDataTableRowStructPaths = AllowedDataTableRowStructPaths;
    return Config;
}

#undef LOCTEXT_NAMESPACE
