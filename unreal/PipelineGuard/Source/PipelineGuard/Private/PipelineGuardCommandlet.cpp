#include "PipelineGuardCommandlet.h"

#include "HAL/FileManager.h"
#include "Misc/FileHelper.h"
#include "Misc/Parse.h"
#include "Misc/Paths.h"
#include "PipelineGuardModule.h"
#include "PipelineGuardRunner.h"
#include "PipelineGuardSettings.h"

UPipelineGuardCommandlet::UPipelineGuardCommandlet()
{
    IsClient = false;
    IsEditor = true;
    IsServer = false;
    LogToConsole = true;
    ShowErrorCount = true;
}

int32 UPipelineGuardCommandlet::Main(const FString& Params)
{
    FString ReportPath;
    if (!FParse::Value(*Params, TEXT("Report="), ReportPath))
    {
        ReportPath = FPaths::Combine(
            FPaths::ProjectSavedDir(),
            TEXT("PipelineGuard"),
            TEXT("PipelineGuardReport.json"));
    }

    ReportPath.TrimQuotesInline();
    ReportPath.TrimStartAndEndInline();
    if (ReportPath.IsEmpty())
    {
        UE_LOG(LogPipelineGuard, Error, TEXT("-Report must specify a JSON file path."));
        return static_cast<int32>(EPipelineGuardExitCode::InvalidConfiguration);
    }

    if (FPaths::IsRelative(ReportPath))
    {
        ReportPath = FPaths::Combine(FPaths::ProjectDir(), ReportPath);
    }
    ReportPath = FPaths::ConvertRelativePathToFull(ReportPath);

    FPipelineGuardReport Report;
    FString RunError;
    const FPipelineGuardRuleConfig Config = GetDefault<UPipelineGuardSettings>()->MakeRuleConfig();
    const EPipelineGuardRunStatus RunStatus = FPipelineGuardRunner::Run(Config, Report, RunError);

    EPipelineGuardExitCode ExitCode = EPipelineGuardExitCode::Clean;
    switch (RunStatus)
    {
    case EPipelineGuardRunStatus::InvalidConfiguration:
        ExitCode = EPipelineGuardExitCode::InvalidConfiguration;
        break;
    case EPipelineGuardRunStatus::ExecutionFailure:
        ExitCode = EPipelineGuardExitCode::ExecutionFailure;
        break;
    case EPipelineGuardRunStatus::Completed:
    default:
        ExitCode = Report.GetErrorCount() > 0
            ? EPipelineGuardExitCode::ValidationFailed
            : EPipelineGuardExitCode::Clean;
        break;
    }
    Report.SuggestedExitCode = static_cast<int32>(ExitCode);

    if (!RunError.IsEmpty())
    {
        UE_LOG(LogPipelineGuard, Error, TEXT("%s"), *RunError);
    }

    if (RunStatus == EPipelineGuardRunStatus::InvalidConfiguration)
    {
        UE_LOG(LogPipelineGuard, Error, TEXT("No report was written because the validation configuration is invalid."));
        return static_cast<int32>(ExitCode);
    }

    const FString ReportDirectory = FPaths::GetPath(ReportPath);
    if (!IFileManager::Get().MakeDirectory(*ReportDirectory, true)
        || !FFileHelper::SaveStringToFile(
            Report.ToJson(),
            *ReportPath,
            FFileHelper::EEncodingOptions::ForceUTF8WithoutBOM))
    {
        UE_LOG(LogPipelineGuard, Error, TEXT("Could not write report '%s'."), *ReportPath);
        return static_cast<int32>(EPipelineGuardExitCode::ReportWriteFailure);
    }

    UE_LOG(
        LogPipelineGuard,
        Display,
        TEXT("Pipeline Guard scanned %d assets: %d errors, %d warnings. Report: %s"),
        Report.ScannedAssetCount,
        Report.GetErrorCount(),
        Report.GetWarningCount(),
        *ReportPath);
    return static_cast<int32>(ExitCode);
}
