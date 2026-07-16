#pragma once

#include "CoreMinimal.h"
#include "Modules/ModuleManager.h"

DECLARE_LOG_CATEGORY_EXTERN(LogPipelineGuard, Log, All);

class FPipelineGuardModule final : public IModuleInterface
{
public:
    virtual void StartupModule() override;
    virtual void ShutdownModule() override;

private:
    void RegisterMenus();
    void OpenPipelineGuardTab();
    TSharedRef<class SDockTab> SpawnPipelineGuardTab(const class FSpawnTabArgs& Args);

    static const FName TabName;
};
