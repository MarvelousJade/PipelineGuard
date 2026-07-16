#include "PipelineGuardRunner.h"

#include "AssetRegistry/AssetRegistryModule.h"
#include "Engine/DataTable.h"
#include "Engine/Texture2D.h"
#include "HAL/PlatformTime.h"
#include "Misc/App.h"
#include "Misc/DateTime.h"
#include "Misc/EngineVersion.h"
#include "Misc/Guid.h"
#include "Modules/ModuleManager.h"
#include "PipelineGuardRuleEvaluator.h"

namespace
{
    FPipelineGuardIssue MakeLoadFailureIssue(const FPipelineGuardAssetFacts& Facts)
    {
        FPipelineGuardIssue Issue;
        Issue.Severity = EPipelineGuardIssueSeverity::Error;
        Issue.RuleId = TEXT("PG.Asset.LoadFailure");
        Issue.AssetName = Facts.AssetName;
        Issue.AssetClassPath = Facts.AssetClassPath;
        Issue.PackagePath = Facts.PackagePath;
        Issue.ObjectPath = Facts.ObjectPath;
        Issue.Message = TEXT("Asset Registry found the asset, but Pipeline Guard could not load it.");
        Issue.SuggestedFix = TEXT("Open the asset, repair missing dependencies, and resave it.");
        return Issue;
    }
}

void FPipelineGuardRunner::InitializeReport(FPipelineGuardReport& OutReport)
{
    OutReport = FPipelineGuardReport();
    OutReport.RunId = FGuid::NewGuid().ToString(EGuidFormats::DigitsWithHyphensLower);
    OutReport.StartedUtc = FDateTime::UtcNow().ToIso8601();
    OutReport.EngineVersion = FEngineVersion::Current().ToString();
    OutReport.Project = FApp::GetProjectName();
}

EPipelineGuardRunStatus FPipelineGuardRunner::Run(
    const FPipelineGuardRuleConfig& Config,
    FPipelineGuardReport& OutReport,
    FString& OutError)
{
    InitializeReport(OutReport);
    OutError.Reset();
    const double StartSeconds = FPlatformTime::Seconds();
    OutReport.RulesRun = FPipelineGuardRuleEvaluator::CountEnabledRuleIds(Config);

    const auto CompleteReport = [&OutReport, StartSeconds]()
    {
        OutReport.CompletedUtc = FDateTime::UtcNow().ToIso8601();
        OutReport.DurationMs = FMath::Max<int64>(
            0,
            FMath::RoundToInt64((FPlatformTime::Seconds() - StartSeconds) * 1000.0));
    };

    if (!FPipelineGuardRuleEvaluator::ValidateConfiguration(Config, OutError))
    {
        OutReport.Status = TEXT("invalid_configuration");
        OutReport.SuggestedExitCode = static_cast<int32>(EPipelineGuardExitCode::InvalidConfiguration);
        CompleteReport();
        return EPipelineGuardRunStatus::InvalidConfiguration;
    }

    OutReport.Scope = Config.ScanPackagePathRoots;

    FAssetRegistryModule* AssetRegistryModule = FModuleManager::LoadModulePtr<FAssetRegistryModule>(TEXT("AssetRegistry"));
    if (AssetRegistryModule == nullptr)
    {
        OutError = TEXT("AssetRegistry module could not be loaded.");
        OutReport.Status = TEXT("execution_failure");
        OutReport.SuggestedExitCode = static_cast<int32>(EPipelineGuardExitCode::ExecutionFailure);
        CompleteReport();
        return EPipelineGuardRunStatus::ExecutionFailure;
    }

    IAssetRegistry& AssetRegistry = AssetRegistryModule->Get();
    AssetRegistry.WaitForCompletion();

    FARFilter Filter;
    Filter.bRecursivePaths = true;
    for (const FString& ScanRoot : Config.ScanPackagePathRoots)
    {
        Filter.PackagePaths.Add(FName(*ScanRoot));
    }

    TArray<FAssetData> Assets;
    AssetRegistry.GetAssets(Filter, Assets);
    Assets.Sort(
        [](const FAssetData& Left, const FAssetData& Right)
        {
            return Left.GetSoftObjectPath().ToString().Compare(
                Right.GetSoftObjectPath().ToString(),
                ESearchCase::CaseSensitive) < 0;
        });

    for (const FAssetData& AssetData : Assets)
    {
        if (AssetData.AssetClassPath.ToString().Equals(
            TEXT("/Script/CoreUObject.ObjectRedirector"),
            ESearchCase::IgnoreCase))
        {
            continue;
        }

        FPipelineGuardAssetFacts Facts;
        Facts.AssetName = AssetData.AssetName.ToString();
        Facts.AssetClassPath = AssetData.AssetClassPath.ToString();
        Facts.PackagePath = AssetData.PackagePath.ToString();
        Facts.ObjectPath = AssetData.GetSoftObjectPath().ToString();

        TArray<FPipelineGuardIssue> AssetIssues;
        UObject* Asset = AssetData.GetAsset();
        if (Asset == nullptr)
        {
            AssetIssues.Add(MakeLoadFailureIssue(Facts));
        }
        else
        {
            if (const UTexture2D* Texture = Cast<UTexture2D>(Asset))
            {
                Facts.bIsTexture2D = true;
                Facts.TextureWidth = Texture->GetSizeX();
                Facts.TextureHeight = Texture->GetSizeY();
            }

            if (const UDataTable* DataTable = Cast<UDataTable>(Asset))
            {
                Facts.bIsDataTable = true;
                if (const UScriptStruct* RowStruct = DataTable->GetRowStruct())
                {
                    Facts.DataTableRowStructPath = RowStruct->GetPathName();
                }
            }
        }

        FPipelineGuardRuleEvaluator::Evaluate(Facts, Config, AssetIssues);
        ++OutReport.ScannedAssetCount;

        if (!OutReport.TryAppendIssues(MoveTemp(AssetIssues)))
        {
            OutError = FString::Printf(
                TEXT("Finding limit of %d exceeded; the report contains a deterministic prefix."),
                FPipelineGuardReport::MaximumFindingCount);
            OutReport.Status = TEXT("execution_failure");
            OutReport.SuggestedExitCode = static_cast<int32>(EPipelineGuardExitCode::ExecutionFailure);
            OutReport.SortIssues();
            CompleteReport();
            return EPipelineGuardRunStatus::ExecutionFailure;
        }
    }

    OutReport.SortIssues();
    OutReport.SuggestedExitCode = OutReport.GetErrorCount() > 0
        ? static_cast<int32>(EPipelineGuardExitCode::ValidationFailed)
        : static_cast<int32>(EPipelineGuardExitCode::Clean);
    CompleteReport();
    return EPipelineGuardRunStatus::Completed;
}
