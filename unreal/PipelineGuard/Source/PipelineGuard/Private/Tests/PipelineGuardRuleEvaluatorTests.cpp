#include "Misc/AutomationTest.h"
#include "PipelineGuardRuleEvaluator.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace
{
    FPipelineGuardRuleConfig MakeBaseConfig()
    {
        FPipelineGuardRuleConfig Config;
        Config.ScanPackagePathRoots = {TEXT("/Game")};
        Config.AssetNameRegex = TEXT("^[A-Za-z][A-Za-z0-9_]*$");
        Config.AllowedPackagePathPrefixes = {TEXT("/Game")};
        Config.bValidateAssetNames = false;
        Config.bEnforceClassPrefixes = false;
        Config.bValidateAllowedPaths = false;
        Config.bValidateTexturePowerOfTwo = false;
        Config.bValidateTextureMaximumDimension = false;
        Config.bValidateDataTableRowStruct = false;
        return Config;
    }

    bool HasRule(const TArray<FPipelineGuardIssue>& Issues, const FString& RuleId)
    {
        return Issues.ContainsByPredicate(
            [&RuleId](const FPipelineGuardIssue& Issue)
            {
                return Issue.RuleId == RuleId;
            });
    }
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(
    FPipelineGuardNamingAndPathTest,
    "PipelineGuard.Rules.NamingAndAllowedPath",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPipelineGuardNamingAndPathTest::RunTest(const FString& Parameters)
{
    (void)Parameters;
    FPipelineGuardRuleConfig Config = MakeBaseConfig();
    Config.bValidateAssetNames = true;
    Config.bEnforceClassPrefixes = true;
    Config.RequiredPrefixesByClass.Add(TEXT("Texture2D"), TEXT("T_"));
    Config.bValidateAllowedPaths = true;
    Config.AllowedPackagePathPrefixes = {TEXT("/Game/Production")};

    FPipelineGuardAssetFacts Facts;
    Facts.AssetName = TEXT("bad texture");
    Facts.AssetClassPath = TEXT("/Script/Engine.Texture2D");
    Facts.PackagePath = TEXT("/Game/Developers/Test");
    Facts.ObjectPath = TEXT("/Game/Developers/Test/bad texture.bad texture");

    TArray<FPipelineGuardIssue> Issues;
    FPipelineGuardRuleEvaluator::Evaluate(Facts, Config, Issues);

    TestTrue(TEXT("Pattern rule fires"), HasRule(Issues, TEXT("PG.Naming.Pattern")));
    TestTrue(TEXT("Class-prefix rule fires"), HasRule(Issues, TEXT("PG.Naming.ClassPrefix")));
    TestTrue(TEXT("Allowed-path rule fires"), HasRule(Issues, TEXT("PG.Path.AllowedRoot")));
    TestEqual(TEXT("Exactly three deterministic findings"), Issues.Num(), 3);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(
    FPipelineGuardTextureTest,
    "PipelineGuard.Rules.TextureDimensions",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPipelineGuardTextureTest::RunTest(const FString& Parameters)
{
    (void)Parameters;
    FPipelineGuardRuleConfig Config = MakeBaseConfig();
    Config.bValidateTexturePowerOfTwo = true;
    Config.bValidateTextureMaximumDimension = true;
    Config.MaximumTextureDimension = 256;

    FPipelineGuardAssetFacts Facts;
    Facts.AssetName = TEXT("T_Test");
    Facts.AssetClassPath = TEXT("/Script/Engine.Texture2D");
    Facts.PackagePath = TEXT("/Game");
    Facts.ObjectPath = TEXT("/Game/T_Test.T_Test");
    Facts.bIsTexture2D = true;
    Facts.TextureWidth = 300;
    Facts.TextureHeight = 1024;

    TArray<FPipelineGuardIssue> Issues;
    FPipelineGuardRuleEvaluator::Evaluate(Facts, Config, Issues);

    TestTrue(TEXT("Power-of-two rule fires"), HasRule(Issues, TEXT("PG.Texture.PowerOfTwo")));
    TestTrue(TEXT("Maximum-dimension rule fires"), HasRule(Issues, TEXT("PG.Texture.MaximumDimension")));
    TestEqual(TEXT("Exactly two texture findings"), Issues.Num(), 2);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(
    FPipelineGuardDataTableTest,
    "PipelineGuard.Rules.DataTableRowStruct",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPipelineGuardDataTableTest::RunTest(const FString& Parameters)
{
    (void)Parameters;
    FPipelineGuardRuleConfig Config = MakeBaseConfig();
    Config.bValidateDataTableRowStruct = true;
    Config.AllowedDataTableRowStructPaths = {TEXT("/Script/TestProject.ApprovedRow")};

    FPipelineGuardAssetFacts Facts;
    Facts.AssetName = TEXT("DT_Test");
    Facts.AssetClassPath = TEXT("/Script/Engine.DataTable");
    Facts.PackagePath = TEXT("/Game");
    Facts.ObjectPath = TEXT("/Game/DT_Test.DT_Test");
    Facts.bIsDataTable = true;
    Facts.DataTableRowStructPath = TEXT("/Script/TestProject.UnapprovedRow");

    TArray<FPipelineGuardIssue> Issues;
    FPipelineGuardRuleEvaluator::Evaluate(Facts, Config, Issues);
    TestTrue(
        TEXT("Unapproved row-structure rule fires"),
        HasRule(Issues, TEXT("PG.DataTable.AllowedRowStruct")));
    TestEqual(TEXT("Exactly one DataTable finding"), Issues.Num(), 1);

    Issues.Reset();
    Facts.DataTableRowStructPath.Reset();
    FPipelineGuardRuleEvaluator::Evaluate(Facts, Config, Issues);
    TestTrue(
        TEXT("Missing row-structure rule fires"),
        HasRule(Issues, TEXT("PG.DataTable.MissingRowStruct")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(
    FPipelineGuardConfigurationTest,
    "PipelineGuard.Rules.Configuration",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)

bool FPipelineGuardConfigurationTest::RunTest(const FString& Parameters)
{
    (void)Parameters;
    FPipelineGuardRuleConfig Config = MakeBaseConfig();
    Config.ScanPackagePathRoots.Reset();

    FString Error;
    TestFalse(
        TEXT("An empty scan-root list is rejected"),
        FPipelineGuardRuleEvaluator::ValidateConfiguration(Config, Error));
    TestFalse(TEXT("Configuration error is actionable"), Error.IsEmpty());

    Config = MakeBaseConfig();
    Config.ScanPackagePathRoots = {TEXT("/Game"), TEXT("/Game")};
    TestFalse(
        TEXT("Duplicate scan roots are rejected"),
        FPipelineGuardRuleEvaluator::ValidateConfiguration(Config, Error));
    TestTrue(TEXT("Duplicate error is explicit"), Error.Contains(TEXT("Duplicate scan root")));

    Config = MakeBaseConfig();
    Config.ScanPackagePathRoots.Reset();
    for (int32 Index = 0; Index <= FPipelineGuardReport::MaximumScopeCount; ++Index)
    {
        Config.ScanPackagePathRoots.Add(FString::Printf(TEXT("/Game/Scope%d"), Index));
    }
    TestFalse(
        TEXT("More than 32 scan roots are rejected"),
        FPipelineGuardRuleEvaluator::ValidateConfiguration(Config, Error));

    Config = MakeBaseConfig();
    FString OversizedRoot = FString::ChrN(
        FPipelineGuardReport::MaximumScopeEntryLength + 1,
        TEXT('A'));
    OversizedRoot[0] = TEXT('/');
    Config.ScanPackagePathRoots = {OversizedRoot};
    TestFalse(
        TEXT("A 513-character scan root is rejected"),
        FPipelineGuardRuleEvaluator::ValidateConfiguration(Config, Error));

    Config = MakeBaseConfig();
    Config.bValidateAssetNames = true;
    Config.AssetNameRegex = TEXT("[");
    TestFalse(
        TEXT("Malformed regex syntax is rejected"),
        FPipelineGuardRuleEvaluator::ValidateConfiguration(Config, Error));
    TestTrue(TEXT("Regex error is explicit"), Error.Contains(TEXT("invalid syntax")));

    Config.AssetNameRegex = TEXT("^[A-Za-z][A-Za-z0-9_]*$");
    TestTrue(
        TEXT("The default regex remains valid"),
        FPipelineGuardRuleEvaluator::ValidateConfiguration(Config, Error));

    Config = MakeBaseConfig();
    Config.bValidateDataTableRowStruct = true;
    TestEqual(
        TEXT("DataTable validation counts its two distinct rule IDs"),
        FPipelineGuardRuleEvaluator::CountEnabledRuleIds(Config),
        2);

    FPipelineGuardIssue FirstUnicodeIssue;
    FirstUnicodeIssue.RuleId = TEXT("PG.Test.Unicode");
    FirstUnicodeIssue.ObjectPath = TEXT("/Game/\u4E00.Asset");
    FPipelineGuardIssue SecondUnicodeIssue = FirstUnicodeIssue;
    SecondUnicodeIssue.ObjectPath = TEXT("/Game/\u4E01.Asset");
    const FString FirstId = FPipelineGuardReport::MakeFindingId(FirstUnicodeIssue);
    TestEqual(
        TEXT("Unicode finding IDs are stable"),
        FirstId,
        FPipelineGuardReport::MakeFindingId(FirstUnicodeIssue));
    TestNotEqual(
        TEXT("Distinct Unicode paths produce distinct finding IDs"),
        FirstId,
        FPipelineGuardReport::MakeFindingId(SecondUnicodeIssue));

    FPipelineGuardReport CappedReport;
    CappedReport.Issues.SetNum(FPipelineGuardReport::MaximumFindingCount - 1);
    TArray<FPipelineGuardIssue> OverflowIssues;
    OverflowIssues.SetNum(2);
    TestFalse(
        TEXT("Finding overflow is reported"),
        CappedReport.TryAppendIssues(MoveTemp(OverflowIssues)));
    TestEqual(
        TEXT("Finding output remains schema-bounded"),
        CappedReport.Issues.Num(),
        FPipelineGuardReport::MaximumFindingCount);
    return true;
}

#endif
