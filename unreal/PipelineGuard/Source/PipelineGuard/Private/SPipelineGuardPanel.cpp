#include "SPipelineGuardPanel.h"

#include "AssetRegistry/AssetRegistryModule.h"
#include "ContentBrowserModule.h"
#include "Editor.h"
#include "IContentBrowserSingleton.h"
#include "ISettingsModule.h"
#include "Modules/ModuleManager.h"
#include "PipelineGuardRunner.h"
#include "PipelineGuardSettings.h"
#include "PipelineGuardTypes.h"
#include "Subsystems/AssetEditorSubsystem.h"
#include "Widgets/Input/SButton.h"
#include "Widgets/SBoxPanel.h"
#include "Widgets/Layout/SBorder.h"
#include "Widgets/Layout/SBox.h"
#include "Widgets/Layout/SScrollBox.h"
#include "Widgets/Text/STextBlock.h"
#include "Widgets/Views/SListView.h"

#define LOCTEXT_NAMESPACE "PipelineGuardPanel"

void SPipelineGuardPanel::Construct(const FArguments& InArgs)
{
    (void)InArgs;
    ChildSlot
    [
        SNew(SVerticalBox)
        + SVerticalBox::Slot()
        .AutoHeight()
        .Padding(8.0f)
        [
            SNew(SHorizontalBox)
            + SHorizontalBox::Slot()
            .AutoWidth()
            .Padding(0.0f, 0.0f, 8.0f, 0.0f)
            [
                SNew(SButton)
                .Text(LOCTEXT("RunValidation", "Run Validation"))
                .OnClicked(this, &SPipelineGuardPanel::RunValidation)
            ]
            + SHorizontalBox::Slot()
            .AutoWidth()
            [
                SNew(SButton)
                .Text(LOCTEXT("OpenSettings", "Settings"))
                .OnClicked(this, &SPipelineGuardPanel::OpenSettings)
            ]
        ]
        + SVerticalBox::Slot()
        .AutoHeight()
        .Padding(8.0f, 0.0f, 8.0f, 8.0f)
        [
            SNew(STextBlock)
            .Text(this, &SPipelineGuardPanel::GetSummaryText)
            .AutoWrapText(true)
        ]
        + SVerticalBox::Slot()
        .FillHeight(1.0f)
        .Padding(8.0f, 0.0f, 8.0f, 8.0f)
        [
            SAssignNew(IssueListView, SListView<TSharedPtr<FPipelineGuardIssue>>)
            .ListItemsSource(&Issues)
            .OnGenerateRow(this, &SPipelineGuardPanel::GenerateIssueRow)
            .OnMouseButtonDoubleClick(this, &SPipelineGuardPanel::NavigateToIssue)
            .SelectionMode(ESelectionMode::Single)
        ]
    ];

    (void)RunValidation();
}

FReply SPipelineGuardPanel::RunValidation()
{
    FPipelineGuardReport Report;
    LastRunError.Reset();
    const FPipelineGuardRuleConfig Config = GetDefault<UPipelineGuardSettings>()->MakeRuleConfig();
    FPipelineGuardRunner::Run(Config, Report, LastRunError);

    ScannedAssetCount = Report.ScannedAssetCount;
    Issues.Reset(Report.Issues.Num());
    for (const FPipelineGuardIssue& Issue : Report.Issues)
    {
        Issues.Add(MakeShared<FPipelineGuardIssue>(Issue));
    }

    if (IssueListView.IsValid())
    {
        IssueListView->RequestListRefresh();
    }
    return FReply::Handled();
}

FReply SPipelineGuardPanel::OpenSettings()
{
    if (ISettingsModule* SettingsModule = FModuleManager::GetModulePtr<ISettingsModule>(TEXT("Settings")))
    {
        SettingsModule->ShowViewer(TEXT("Project"), TEXT("Plugins"), TEXT("PipelineGuard"));
    }
    return FReply::Handled();
}

FText SPipelineGuardPanel::GetSummaryText() const
{
    if (!LastRunError.IsEmpty())
    {
        return FText::Format(
            LOCTEXT("RunError", "Validation could not complete: {0}"),
            FText::FromString(LastRunError));
    }

    int32 ErrorCount = 0;
    int32 WarningCount = 0;
    for (const TSharedPtr<FPipelineGuardIssue>& Issue : Issues)
    {
        if (Issue.IsValid() && Issue->Severity == EPipelineGuardIssueSeverity::Error)
        {
            ++ErrorCount;
        }
        else if (Issue.IsValid())
        {
            ++WarningCount;
        }
    }

    return FText::Format(
        LOCTEXT(
            "Summary",
            "Scanned {0} assets: {1} errors, {2} warnings. Double-click a finding to focus and open its asset."),
        FText::AsNumber(ScannedAssetCount),
        FText::AsNumber(ErrorCount),
        FText::AsNumber(WarningCount));
}

TSharedRef<ITableRow> SPipelineGuardPanel::GenerateIssueRow(
    TSharedPtr<FPipelineGuardIssue> Item,
    const TSharedRef<STableViewBase>& OwnerTable) const
{
    const FString Severity = Item.IsValid() && Item->Severity == EPipelineGuardIssueSeverity::Error
        ? TEXT("ERROR")
        : TEXT("WARNING");
    const FString RowText = Item.IsValid()
        ? FString::Printf(
            TEXT("[%s] %s | %s\n%s\nFix: %s"),
            *Severity,
            *Item->RuleId,
            *Item->ObjectPath,
            *Item->Message,
            *Item->SuggestedFix)
        : TEXT("Invalid finding");

    return SNew(STableRow<TSharedPtr<FPipelineGuardIssue>>, OwnerTable)
    [
        SNew(SBorder)
        .Padding(6.0f)
        [
            SNew(STextBlock)
            .Text(FText::FromString(RowText))
            .AutoWrapText(true)
        ]
    ];
}

void SPipelineGuardPanel::NavigateToIssue(TSharedPtr<FPipelineGuardIssue> Item) const
{
    if (!Item.IsValid() || Item->ObjectPath.IsEmpty())
    {
        return;
    }

    FAssetRegistryModule& AssetRegistryModule =
        FModuleManager::LoadModuleChecked<FAssetRegistryModule>(TEXT("AssetRegistry"));
    const FAssetData AssetData =
        AssetRegistryModule.Get().GetAssetByObjectPath(FSoftObjectPath(Item->ObjectPath));
    if (!AssetData.IsValid())
    {
        return;
    }

    TArray<FAssetData> AssetsToSync;
    AssetsToSync.Add(AssetData);
    FContentBrowserModule& ContentBrowserModule =
        FModuleManager::LoadModuleChecked<FContentBrowserModule>(TEXT("ContentBrowser"));
    ContentBrowserModule.Get().SyncBrowserToAssets(AssetsToSync);

    if (GEditor != nullptr)
    {
        if (UObject* Asset = AssetData.GetAsset())
        {
            if (UAssetEditorSubsystem* AssetEditorSubsystem =
                GEditor->GetEditorSubsystem<UAssetEditorSubsystem>())
            {
                AssetEditorSubsystem->OpenEditorForAsset(Asset);
            }
        }
    }
}

#undef LOCTEXT_NAMESPACE
