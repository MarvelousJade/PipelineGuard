#include "PipelineGuardModule.h"

#include "Framework/Commands/UIAction.h"
#include "Framework/Docking/TabManager.h"
#include "ISettingsModule.h"
#include "PipelineGuardSettings.h"
#include "SPipelineGuardPanel.h"
#include "ToolMenus.h"
#include "Widgets/Docking/SDockTab.h"

#define LOCTEXT_NAMESPACE "PipelineGuardModule"

DEFINE_LOG_CATEGORY(LogPipelineGuard);

const FName FPipelineGuardModule::TabName(TEXT("PipelineGuard"));

void FPipelineGuardModule::StartupModule()
{
    if (IsRunningCommandlet())
    {
        return;
    }

    FGlobalTabmanager::Get()->RegisterNomadTabSpawner(
        TabName,
        FOnSpawnTab::CreateRaw(this, &FPipelineGuardModule::SpawnPipelineGuardTab))
        .SetDisplayName(LOCTEXT("TabTitle", "Pipeline Guard"))
        .SetTooltipText(LOCTEXT("TabTooltip", "Validate project assets against configurable pipeline rules."))
        .SetMenuType(ETabSpawnerMenuType::Hidden);

    if (ISettingsModule* SettingsModule =
        FModuleManager::LoadModulePtr<ISettingsModule>(TEXT("Settings")))
    {
        SettingsModule->RegisterSettings(
            TEXT("Project"),
            TEXT("Plugins"),
            TEXT("PipelineGuard"),
            LOCTEXT("SettingsName", "Pipeline Guard"),
            LOCTEXT("SettingsDescription", "Configure generic content validation rules."),
            GetMutableDefault<UPipelineGuardSettings>());
    }

    UToolMenus::RegisterStartupCallback(
        FSimpleMulticastDelegate::FDelegate::CreateRaw(this, &FPipelineGuardModule::RegisterMenus));
}

void FPipelineGuardModule::ShutdownModule()
{
    if (IsRunningCommandlet())
    {
        return;
    }

    UToolMenus::UnRegisterStartupCallback(this);
    UToolMenus::UnregisterOwner(this);
    FGlobalTabmanager::Get()->UnregisterNomadTabSpawner(TabName);

    if (ISettingsModule* SettingsModule =
        FModuleManager::GetModulePtr<ISettingsModule>(TEXT("Settings")))
    {
        SettingsModule->UnregisterSettings(
            TEXT("Project"),
            TEXT("Plugins"),
            TEXT("PipelineGuard"));
    }
}

void FPipelineGuardModule::RegisterMenus()
{
    FToolMenuOwnerScoped OwnerScoped(this);
    UToolMenu* ToolsMenu = UToolMenus::Get()->ExtendMenu(TEXT("LevelEditor.MainMenu.Tools"));
    FToolMenuSection& Section = ToolsMenu->FindOrAddSection(TEXT("PipelineGuard"));
    Section.AddMenuEntry(
        TEXT("OpenPipelineGuard"),
        LOCTEXT("OpenMenuLabel", "Pipeline Guard"),
        LOCTEXT("OpenMenuTooltip", "Open the Pipeline Guard validation panel."),
        FSlateIcon(),
        FUIAction(FExecuteAction::CreateRaw(this, &FPipelineGuardModule::OpenPipelineGuardTab)));
}

void FPipelineGuardModule::OpenPipelineGuardTab()
{
    FGlobalTabmanager::Get()->TryInvokeTab(TabName);
}

TSharedRef<SDockTab> FPipelineGuardModule::SpawnPipelineGuardTab(const FSpawnTabArgs& Args)
{
    (void)Args;
    return SNew(SDockTab)
        .TabRole(ETabRole::NomadTab)
        [
            SNew(SPipelineGuardPanel)
        ];
}

IMPLEMENT_MODULE(FPipelineGuardModule, PipelineGuard)

#undef LOCTEXT_NAMESPACE
