#pragma once

#include "CoreMinimal.h"
#include "Widgets/SCompoundWidget.h"
#include "Widgets/Views/SListView.h"

struct FPipelineGuardIssue;

class SPipelineGuardPanel final : public SCompoundWidget
{
public:
    SLATE_BEGIN_ARGS(SPipelineGuardPanel)
    {
    }
    SLATE_END_ARGS()

    void Construct(const FArguments& InArgs);

private:
    FReply RunValidation();
    FReply OpenSettings();
    FText GetSummaryText() const;
    TSharedRef<ITableRow> GenerateIssueRow(
        TSharedPtr<FPipelineGuardIssue> Item,
        const TSharedRef<STableViewBase>& OwnerTable) const;
    void NavigateToIssue(TSharedPtr<FPipelineGuardIssue> Item) const;

    TArray<TSharedPtr<FPipelineGuardIssue>> Issues;
    TSharedPtr<SListView<TSharedPtr<FPipelineGuardIssue>>> IssueListView;
    int32 ScannedAssetCount = 0;
    FString LastRunError;
};
