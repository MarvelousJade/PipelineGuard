#pragma once

#include "CoreMinimal.h"
#include "PipelineGuardTypes.h"

class PIPELINEGUARD_API FPipelineGuardRunner
{
public:
    static EPipelineGuardRunStatus Run(
        const FPipelineGuardRuleConfig& Config,
        FPipelineGuardReport& OutReport,
        FString& OutError);

    static void InitializeReport(FPipelineGuardReport& OutReport);
};
