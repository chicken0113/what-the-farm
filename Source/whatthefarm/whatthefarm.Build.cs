// Copyright Epic Games, Inc. All Rights Reserved.

using UnrealBuildTool;

public class whatthefarm : ModuleRules
{
	public whatthefarm(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] {
			"Core",
			"CoreUObject",
			"Engine",
			"InputCore",
			"EnhancedInput",
			"AIModule",
			"StateTreeModule",
			"GameplayStateTreeModule",
			"UMG",
			"Slate"
		});

		PrivateDependencyModuleNames.AddRange(new string[] { });

		PublicIncludePaths.AddRange(new string[] {
			"whatthefarm",
			"whatthefarm/Variant_Horror",
			"whatthefarm/Variant_Horror/UI",
			"whatthefarm/Variant_Shooter",
			"whatthefarm/Variant_Shooter/AI",
			"whatthefarm/Variant_Shooter/UI",
			"whatthefarm/Variant_Shooter/Weapons"
		});

		// Uncomment if you are using Slate UI
		// PrivateDependencyModuleNames.AddRange(new string[] { "Slate", "SlateCore" });

		// Uncomment if you are using online features
		// PrivateDependencyModuleNames.Add("OnlineSubsystem");

		// To include OnlineSubsystemSteam, add it to the plugins section in your uproject file with the Enabled attribute set to true
	}
}
