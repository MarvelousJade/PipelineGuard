#pragma once

#include "CoreMinimal.h"
#include "Commandlets/Commandlet.h"
#include "PipelineGuardCommandlet.generated.h"

UCLASS()
class PIPELINEGUARD_API UPipelineGuardCommandlet final : public UCommandlet
{
    GENERATED_BODY()

public:
    UPipelineGuardCommandlet();
    virtual int32 Main(const FString& Params) override;
};
