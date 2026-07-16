#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "PipelineGuardTypes.h"
#include "PipelineGuardSettings.generated.h"

UCLASS(Config=PipelineGuard, DefaultConfig, meta=(DisplayName="Pipeline Guard"))
class PIPELINEGUARD_API UPipelineGuardSettings final : public UDeveloperSettings
{
    GENERATED_BODY()

public:
    UPipelineGuardSettings();

    virtual FName GetCategoryName() const override;
    virtual FText GetSectionText() const override;

    FPipelineGuardRuleConfig MakeRuleConfig() const;

    UPROPERTY(EditAnywhere, Config, Category="Scan", meta=(ToolTip="Virtual package roots to scan recursively, for example /Game."))
    TArray<FString> ScanPackagePathRoots;

    UPROPERTY(EditAnywhere, Config, Category="Asset Naming")
    bool bValidateAssetNames = true;

    UPROPERTY(EditAnywhere, Config, Category="Asset Naming", meta=(EditCondition="bValidateAssetNames"))
    FString AssetNameRegex;

    UPROPERTY(EditAnywhere, Config, Category="Asset Naming", meta=(EditCondition="bValidateAssetNames"))
    bool bEnforceClassPrefixes = false;

    UPROPERTY(EditAnywhere, Config, Category="Asset Naming", meta=(EditCondition="bValidateAssetNames && bEnforceClassPrefixes", ToolTip="Map a full class path or short class name to its required asset-name prefix."))
    TMap<FString, FString> RequiredPrefixesByClass;

    UPROPERTY(EditAnywhere, Config, Category="Allowed Paths")
    bool bValidateAllowedPaths = true;

    UPROPERTY(EditAnywhere, Config, Category="Allowed Paths", meta=(EditCondition="bValidateAllowedPaths"))
    TArray<FString> AllowedPackagePathPrefixes;

    UPROPERTY(EditAnywhere, Config, Category="Textures")
    bool bValidateTexturePowerOfTwo = true;

    UPROPERTY(EditAnywhere, Config, Category="Textures")
    bool bValidateTextureMaximumDimension = true;

    UPROPERTY(EditAnywhere, Config, Category="Textures", meta=(EditCondition="bValidateTextureMaximumDimension", ClampMin="1"))
    int32 MaximumTextureDimension = 8192;

    UPROPERTY(EditAnywhere, Config, Category="Data Tables")
    bool bValidateDataTableRowStruct = true;

    UPROPERTY(EditAnywhere, Config, Category="Data Tables", meta=(EditCondition="bValidateDataTableRowStruct", ToolTip="If empty, every non-null row struct is accepted. Values are object paths such as /Script/MyGame.MyRow."))
    TArray<FString> AllowedDataTableRowStructPaths;
};
