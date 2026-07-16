#include "PipelineGuardTypes.h"

#include "Containers/StringConv.h"
#include "Dom/JsonObject.h"
#include "Misc/SecureHash.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"

namespace
{
    const TCHAR* SeverityToString(const EPipelineGuardIssueSeverity Severity)
    {
        return Severity == EPipelineGuardIssueSeverity::Error ? TEXT("error") : TEXT("warning");
    }
}

int32 FPipelineGuardReport::GetErrorCount() const
{
    int32 Count = 0;
    for (const FPipelineGuardIssue& Issue : Issues)
    {
        if (Issue.Severity == EPipelineGuardIssueSeverity::Error)
        {
            ++Count;
        }
    }
    return Count;
}

int32 FPipelineGuardReport::GetWarningCount() const
{
    int32 Count = 0;
    for (const FPipelineGuardIssue& Issue : Issues)
    {
        if (Issue.Severity == EPipelineGuardIssueSeverity::Warning)
        {
            ++Count;
        }
    }
    return Count;
}

FString FPipelineGuardReport::MakeFindingId(const FPipelineGuardIssue& Issue)
{
    const FString FindingKey = Issue.RuleId + TEXT("|") + Issue.ObjectPath;
    const FTCHARToUTF8 Utf8FindingKey(*FindingKey);
    return FMD5::HashBytes(
        reinterpret_cast<const uint8*>(Utf8FindingKey.Get()),
        static_cast<uint64>(Utf8FindingKey.Length()));
}

bool FPipelineGuardReport::TryAppendIssues(TArray<FPipelineGuardIssue>&& NewIssues)
{
    const int32 RemainingCapacity = FMath::Max(0, MaximumFindingCount - Issues.Num());
    if (NewIssues.Num() <= RemainingCapacity)
    {
        Issues.Append(MoveTemp(NewIssues));
        return true;
    }

    Issues.Reserve(MaximumFindingCount);
    for (int32 Index = 0; Index < RemainingCapacity; ++Index)
    {
        Issues.Add(MoveTemp(NewIssues[Index]));
    }
    return false;
}

void FPipelineGuardReport::SortIssues()
{
    Issues.Sort(
        [](const FPipelineGuardIssue& Left, const FPipelineGuardIssue& Right)
        {
            const int32 ObjectComparison = Left.ObjectPath.Compare(Right.ObjectPath, ESearchCase::CaseSensitive);
            if (ObjectComparison != 0)
            {
                return ObjectComparison < 0;
            }

            const int32 RuleComparison = Left.RuleId.Compare(Right.RuleId, ESearchCase::CaseSensitive);
            if (RuleComparison != 0)
            {
                return RuleComparison < 0;
            }

            const int32 MessageComparison = Left.Message.Compare(Right.Message, ESearchCase::CaseSensitive);
            if (MessageComparison != 0)
            {
                return MessageComparison < 0;
            }

            return FPipelineGuardReport::MakeFindingId(Left).Compare(
                FPipelineGuardReport::MakeFindingId(Right),
                ESearchCase::CaseSensitive) < 0;
        });
}

FString FPipelineGuardReport::ToJson() const
{
    TSharedRef<FJsonObject> Root = MakeShared<FJsonObject>();
    Root->SetStringField(TEXT("schemaVersion"), SchemaVersion);
    Root->SetStringField(TEXT("runId"), RunId);
    Root->SetStringField(TEXT("toolVersion"), ToolVersion);
    Root->SetStringField(TEXT("startedUtc"), StartedUtc);
    Root->SetStringField(TEXT("completedUtc"), CompletedUtc);
    Root->SetStringField(TEXT("project"), Project);
    Root->SetStringField(TEXT("engineVersion"), EngineVersion);
    Root->SetStringField(TEXT("profile"), Profile);

    TArray<TSharedPtr<FJsonValue>> JsonScope;
    JsonScope.Reserve(Scope.Num());
    for (const FString& ScopeRoot : Scope)
    {
        JsonScope.Add(MakeShared<FJsonValueString>(ScopeRoot));
    }
    Root->SetArrayField(TEXT("scope"), JsonScope);

    TSharedRef<FJsonObject> Summary = MakeShared<FJsonObject>();
    Summary->SetNumberField(TEXT("assetsScanned"), ScannedAssetCount);
    Summary->SetNumberField(TEXT("rulesRun"), RulesRun);
    Summary->SetNumberField(TEXT("errors"), GetErrorCount());
    Summary->SetNumberField(TEXT("warnings"), GetWarningCount());
    Summary->SetNumberField(TEXT("infos"), 0);
    Summary->SetNumberField(TEXT("suppressed"), 0);
    Summary->SetNumberField(TEXT("durationMs"), static_cast<double>(DurationMs));
    Root->SetObjectField(TEXT("summary"), Summary);

    TArray<TSharedPtr<FJsonValue>> JsonFindings;
    JsonFindings.Reserve(Issues.Num());
    for (const FPipelineGuardIssue& Issue : Issues)
    {
        TSharedRef<FJsonObject> Finding = MakeShared<FJsonObject>();
        Finding->SetStringField(TEXT("id"), MakeFindingId(Issue));
        Finding->SetStringField(TEXT("ruleId"), Issue.RuleId);
        Finding->SetStringField(TEXT("severity"), SeverityToString(Issue.Severity));
        Finding->SetStringField(TEXT("assetPath"), Issue.ObjectPath);
        Finding->SetStringField(TEXT("message"), Issue.Message);
        Finding->SetStringField(TEXT("remediation"), Issue.SuggestedFix);
        Finding->SetBoolField(TEXT("suppressed"), false);
        JsonFindings.Add(MakeShared<FJsonValueObject>(Finding));
    }
    Root->SetArrayField(TEXT("findings"), JsonFindings);

    FString Json;
    const TSharedRef<TJsonWriter<TCHAR, TPrettyJsonPrintPolicy<TCHAR>>> Writer =
        TJsonWriterFactory<TCHAR, TPrettyJsonPrintPolicy<TCHAR>>::Create(&Json);
    FJsonSerializer::Serialize(Root, Writer);
    return Json;
}
