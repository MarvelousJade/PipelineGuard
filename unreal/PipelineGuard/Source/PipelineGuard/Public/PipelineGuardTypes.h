#pragma once

#include "CoreMinimal.h"

enum class EPipelineGuardIssueSeverity : uint8
{
    Warning,
    Error
};

enum class EPipelineGuardRunStatus : uint8
{
    Completed,
    InvalidConfiguration,
    ExecutionFailure
};

enum class EPipelineGuardExitCode : int32
{
    Clean = 0,
    ValidationFailed = 1,
    InvalidConfiguration = 2,
    ExecutionFailure = 3,
    ReportWriteFailure = 4
};

struct PIPELINEGUARD_API FPipelineGuardIssue
{
    EPipelineGuardIssueSeverity Severity = EPipelineGuardIssueSeverity::Error;
    FString RuleId;
    FString AssetName;
    FString AssetClassPath;
    FString PackagePath;
    FString ObjectPath;
    FString Message;
    FString SuggestedFix;
};

struct PIPELINEGUARD_API FPipelineGuardReport
{
    static constexpr int32 MaximumScopeCount = 32;
    static constexpr int32 MaximumScopeEntryLength = 512;
    static constexpr int32 MaximumFindingCount = 20000;

    FString SchemaVersion = TEXT("1.0");
    FString RunId;
    FString ToolVersion = TEXT("1.0.0");
    FString StartedUtc;
    FString CompletedUtc;
    FString EngineVersion;
    FString Project;
    FString Profile = TEXT("default");
    TArray<FString> Scope;
    int32 RulesRun = 0;
    int64 DurationMs = 0;

    // Internal commandlet state; intentionally not part of portable report schema v1.
    FString Status = TEXT("completed");
    int32 ScannedAssetCount = 0;
    int32 SuggestedExitCode = static_cast<int32>(EPipelineGuardExitCode::Clean);
    TArray<FPipelineGuardIssue> Issues;

    int32 GetErrorCount() const;
    int32 GetWarningCount() const;
    static FString MakeFindingId(const FPipelineGuardIssue& Issue);
    bool TryAppendIssues(TArray<FPipelineGuardIssue>&& NewIssues);
    void SortIssues();
    FString ToJson() const;
};

struct PIPELINEGUARD_API FPipelineGuardAssetFacts
{
    FString AssetName;
    FString AssetClassPath;
    FString PackagePath;
    FString ObjectPath;

    bool bIsTexture2D = false;
    int32 TextureWidth = 0;
    int32 TextureHeight = 0;

    bool bIsDataTable = false;
    FString DataTableRowStructPath;
};

struct PIPELINEGUARD_API FPipelineGuardRuleConfig
{
    TArray<FString> ScanPackagePathRoots;

    bool bValidateAssetNames = true;
    FString AssetNameRegex;
    bool bEnforceClassPrefixes = false;
    TMap<FString, FString> RequiredPrefixesByClass;

    bool bValidateAllowedPaths = true;
    TArray<FString> AllowedPackagePathPrefixes;

    bool bValidateTexturePowerOfTwo = true;
    bool bValidateTextureMaximumDimension = true;
    int32 MaximumTextureDimension = 8192;

    bool bValidateDataTableRowStruct = true;
    TArray<FString> AllowedDataTableRowStructPaths;
};
