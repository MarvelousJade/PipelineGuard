#include "PipelineGuardRuleEvaluator.h"

#include "Internationalization/Regex.h"

namespace PipelineGuardRuleIds
{
    static const FString AssetNamePattern = TEXT("PG.Naming.Pattern");
    static const FString AssetClassPrefix = TEXT("PG.Naming.ClassPrefix");
    static const FString AllowedPath = TEXT("PG.Path.AllowedRoot");
    static const FString TexturePowerOfTwo = TEXT("PG.Texture.PowerOfTwo");
    static const FString TextureMaximumDimension = TEXT("PG.Texture.MaximumDimension");
    static const FString DataTableMissingRowStruct = TEXT("PG.DataTable.MissingRowStruct");
    static const FString DataTableAllowedRowStruct = TEXT("PG.DataTable.AllowedRowStruct");
}

void FPipelineGuardRuleEvaluator::Evaluate(
    const FPipelineGuardAssetFacts& Facts,
    const FPipelineGuardRuleConfig& Config,
    TArray<FPipelineGuardIssue>& OutIssues)
{
    if (Config.bValidateAssetNames)
    {
        const FRegexPattern Pattern(Config.AssetNameRegex);
        FRegexMatcher Matcher(Pattern, Facts.AssetName);
        const bool bMatched = Matcher.FindNext()
            && Matcher.GetMatchBeginning() == 0
            && Matcher.GetMatchEnding() == Facts.AssetName.Len();

        if (!bMatched)
        {
            AddIssue(
                Facts,
                PipelineGuardRuleIds::AssetNamePattern,
                FString::Printf(TEXT("Asset name '%s' does not match '%s'."), *Facts.AssetName, *Config.AssetNameRegex),
                TEXT("Rename the asset to match the configured naming expression."),
                OutIssues);
        }

        if (Config.bEnforceClassPrefixes)
        {
            const FString ShortClassName = GetShortClassName(Facts.AssetClassPath);
            const FString* RequiredPrefix = Config.RequiredPrefixesByClass.Find(Facts.AssetClassPath);
            if (RequiredPrefix == nullptr)
            {
                RequiredPrefix = Config.RequiredPrefixesByClass.Find(ShortClassName);
            }

            if (RequiredPrefix != nullptr && !Facts.AssetName.StartsWith(*RequiredPrefix, ESearchCase::CaseSensitive))
            {
                AddIssue(
                    Facts,
                    PipelineGuardRuleIds::AssetClassPrefix,
                    FString::Printf(TEXT("%s assets must start with '%s'."), *ShortClassName, **RequiredPrefix),
                    FString::Printf(TEXT("Rename the asset with the '%s' prefix."), **RequiredPrefix),
                    OutIssues);
            }
        }
    }

    if (Config.bValidateAllowedPaths)
    {
        const bool bAllowed = Config.AllowedPackagePathPrefixes.ContainsByPredicate(
            [&Facts](const FString& Prefix)
            {
                return IsUnderPathPrefix(Facts.PackagePath, Prefix);
            });

        if (!bAllowed)
        {
            AddIssue(
                Facts,
                PipelineGuardRuleIds::AllowedPath,
                FString::Printf(TEXT("Package path '%s' is outside every allowed path root."), *Facts.PackagePath),
                TEXT("Move the asset under an allowed root or update Pipeline Guard settings."),
                OutIssues);
        }
    }

    if (Facts.bIsTexture2D)
    {
        if (Config.bValidateTexturePowerOfTwo
            && (!FMath::IsPowerOfTwo(Facts.TextureWidth) || !FMath::IsPowerOfTwo(Facts.TextureHeight)))
        {
            AddIssue(
                Facts,
                PipelineGuardRuleIds::TexturePowerOfTwo,
                FString::Printf(TEXT("Texture dimensions %dx%d are not powers of two."), Facts.TextureWidth, Facts.TextureHeight),
                TEXT("Resize the source texture or disable this rule for the project."),
                OutIssues);
        }

        if (Config.bValidateTextureMaximumDimension
            && (Facts.TextureWidth > Config.MaximumTextureDimension || Facts.TextureHeight > Config.MaximumTextureDimension))
        {
            AddIssue(
                Facts,
                PipelineGuardRuleIds::TextureMaximumDimension,
                FString::Printf(
                    TEXT("Texture dimensions %dx%d exceed the configured maximum of %d."),
                    Facts.TextureWidth,
                    Facts.TextureHeight,
                    Config.MaximumTextureDimension),
                TEXT("Reduce the source dimensions or raise the configured limit deliberately."),
                OutIssues);
        }
    }

    if (Facts.bIsDataTable && Config.bValidateDataTableRowStruct)
    {
        if (Facts.DataTableRowStructPath.IsEmpty())
        {
            AddIssue(
                Facts,
                PipelineGuardRuleIds::DataTableMissingRowStruct,
                TEXT("DataTable has no row structure."),
                TEXT("Assign a valid UScriptStruct-derived row structure."),
                OutIssues);
        }
        else if (!Config.AllowedDataTableRowStructPaths.IsEmpty())
        {
            const bool bAllowed = Config.AllowedDataTableRowStructPaths.ContainsByPredicate(
                [&Facts](const FString& AllowedPath)
                {
                    return Facts.DataTableRowStructPath.Equals(AllowedPath, ESearchCase::IgnoreCase);
                });

            if (!bAllowed)
            {
                AddIssue(
                    Facts,
                    PipelineGuardRuleIds::DataTableAllowedRowStruct,
                    FString::Printf(TEXT("Row structure '%s' is not in the allow-list."), *Facts.DataTableRowStructPath),
                    TEXT("Use an approved row structure or update the allow-list."),
                    OutIssues);
            }
        }
    }
}

bool FPipelineGuardRuleEvaluator::ValidateConfiguration(const FPipelineGuardRuleConfig& Config, FString& OutError)
{
    TArray<FString> Errors;

    if (Config.ScanPackagePathRoots.IsEmpty())
    {
        Errors.Add(TEXT("ScanPackagePathRoots must contain at least one virtual package path."));
    }
    else if (Config.ScanPackagePathRoots.Num() > FPipelineGuardReport::MaximumScopeCount)
    {
        Errors.Add(FString::Printf(
            TEXT("ScanPackagePathRoots cannot contain more than %d entries."),
            FPipelineGuardReport::MaximumScopeCount));
    }

    TSet<FString> SeenScanRoots;
    for (const FString& Root : Config.ScanPackagePathRoots)
    {
        if (Root.IsEmpty() || !Root.StartsWith(TEXT("/")))
        {
            Errors.Add(FString::Printf(TEXT("Invalid scan root '%s'; virtual paths must start with '/'."), *Root));
        }
        if (Root.Len() > FPipelineGuardReport::MaximumScopeEntryLength)
        {
            Errors.Add(FString::Printf(
                TEXT("Scan root exceeds the %d-character report-contract limit."),
                FPipelineGuardReport::MaximumScopeEntryLength));
        }
        if (SeenScanRoots.Contains(Root))
        {
            Errors.Add(FString::Printf(TEXT("Duplicate scan root '%s' is not allowed."), *Root));
        }
        SeenScanRoots.Add(Root);
    }

    if (Config.bValidateAssetNames)
    {
        if (Config.AssetNameRegex.IsEmpty())
        {
            Errors.Add(TEXT("AssetNameRegex cannot be empty while asset-name validation is enabled."));
        }
        else if (Config.AssetNameRegex.Len() > FPipelineGuardReport::MaximumScopeEntryLength)
        {
            Errors.Add(FString::Printf(
                TEXT("AssetNameRegex cannot exceed %d characters."),
                FPipelineGuardReport::MaximumScopeEntryLength));
        }
        else
        {
            const FRegexPattern Pattern(Config.AssetNameRegex);
            FRegexMatcher Matcher(Pattern, TEXT(""));
            if (Matcher.GetBeginLimit() == INDEX_NONE)
            {
                Errors.Add(TEXT("AssetNameRegex contains invalid syntax for Unreal's regex engine."));
            }
        }
    }

    if (Config.bValidateAllowedPaths && Config.AllowedPackagePathPrefixes.IsEmpty())
    {
        Errors.Add(TEXT("AllowedPackagePathPrefixes cannot be empty while allowed-path validation is enabled."));
    }

    for (const FString& Root : Config.AllowedPackagePathPrefixes)
    {
        if (Root.IsEmpty() || !Root.StartsWith(TEXT("/")))
        {
            Errors.Add(FString::Printf(TEXT("Invalid allowed root '%s'; virtual paths must start with '/'."), *Root));
        }
        if (Root.Len() > FPipelineGuardReport::MaximumScopeEntryLength)
        {
            Errors.Add(FString::Printf(
                TEXT("Allowed path root exceeds %d characters."),
                FPipelineGuardReport::MaximumScopeEntryLength));
        }
    }

    if (Config.bValidateTextureMaximumDimension && Config.MaximumTextureDimension <= 0)
    {
        Errors.Add(TEXT("MaximumTextureDimension must be greater than zero."));
    }

    for (const TPair<FString, FString>& PrefixPair : Config.RequiredPrefixesByClass)
    {
        if (PrefixPair.Key.IsEmpty() || PrefixPair.Value.IsEmpty())
        {
            Errors.Add(TEXT("RequiredPrefixesByClass cannot contain empty class names or prefixes."));
            break;
        }
    }

    for (const FString& RowStructPath : Config.AllowedDataTableRowStructPaths)
    {
        if (RowStructPath.IsEmpty())
        {
            Errors.Add(TEXT("AllowedDataTableRowStructPaths cannot contain empty entries."));
            break;
        }
    }

    OutError = FString::Join(Errors, TEXT("\n"));
    return Errors.IsEmpty();
}

int32 FPipelineGuardRuleEvaluator::CountEnabledRuleIds(const FPipelineGuardRuleConfig& Config)
{
    return (Config.bValidateAssetNames ? 1 : 0)
        + (Config.bValidateAssetNames && Config.bEnforceClassPrefixes ? 1 : 0)
        + (Config.bValidateAllowedPaths ? 1 : 0)
        + (Config.bValidateTexturePowerOfTwo ? 1 : 0)
        + (Config.bValidateTextureMaximumDimension ? 1 : 0)
        + (Config.bValidateDataTableRowStruct ? 2 : 0);
}

bool FPipelineGuardRuleEvaluator::IsUnderPathPrefix(const FString& PackagePath, const FString& Prefix)
{
    FString NormalizedPrefix = Prefix;
    while (NormalizedPrefix.Len() > 1 && NormalizedPrefix.EndsWith(TEXT("/")))
    {
        NormalizedPrefix.LeftChopInline(1, EAllowShrinking::No);
    }

    return PackagePath.Equals(NormalizedPrefix, ESearchCase::IgnoreCase)
        || PackagePath.StartsWith(NormalizedPrefix + TEXT("/"), ESearchCase::IgnoreCase);
}

FString FPipelineGuardRuleEvaluator::GetShortClassName(const FString& ClassPath)
{
    int32 SeparatorIndex = INDEX_NONE;
    if (ClassPath.FindLastChar(TEXT('.'), SeparatorIndex))
    {
        return ClassPath.Mid(SeparatorIndex + 1);
    }
    return ClassPath;
}

void FPipelineGuardRuleEvaluator::AddIssue(
    const FPipelineGuardAssetFacts& Facts,
    const FString& RuleId,
    const FString& Message,
    const FString& SuggestedFix,
    TArray<FPipelineGuardIssue>& OutIssues)
{
    FPipelineGuardIssue& Issue = OutIssues.AddDefaulted_GetRef();
    Issue.Severity = EPipelineGuardIssueSeverity::Error;
    Issue.RuleId = RuleId;
    Issue.AssetName = Facts.AssetName;
    Issue.AssetClassPath = Facts.AssetClassPath;
    Issue.PackagePath = Facts.PackagePath;
    Issue.ObjectPath = Facts.ObjectPath;
    Issue.Message = Message;
    Issue.SuggestedFix = SuggestedFix;
}
