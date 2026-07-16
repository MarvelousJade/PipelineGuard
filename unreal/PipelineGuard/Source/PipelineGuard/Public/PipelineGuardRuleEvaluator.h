#pragma once

#include "CoreMinimal.h"
#include "PipelineGuardTypes.h"

class PIPELINEGUARD_API FPipelineGuardRuleEvaluator
{
public:
    static void Evaluate(
        const FPipelineGuardAssetFacts& Facts,
        const FPipelineGuardRuleConfig& Config,
        TArray<FPipelineGuardIssue>& OutIssues);

    static bool ValidateConfiguration(const FPipelineGuardRuleConfig& Config, FString& OutError);
    static int32 CountEnabledRuleIds(const FPipelineGuardRuleConfig& Config);

private:
    static bool IsUnderPathPrefix(const FString& PackagePath, const FString& Prefix);
    static FString GetShortClassName(const FString& ClassPath);
    static void AddIssue(
        const FPipelineGuardAssetFacts& Facts,
        const FString& RuleId,
        const FString& Message,
        const FString& SuggestedFix,
        TArray<FPipelineGuardIssue>& OutIssues);
};
