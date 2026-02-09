using System;

using Dalamud.Game.ClientState.Conditions;

using FFXIVClientStructs.FFXIV.Client.Game.Character;

using Visibility.Configuration;

namespace Visibility.Utils.EntityHandlers;

/// <summary>
/// Handles visibility logic for hrothgar entities
/// </summary>
public class HrothgarHandler
{
	private readonly ContainerManager containerManager;
	private readonly VoidListManager voidListManager;
	private readonly ObjectVisibilityManager visibilityManager;

	private static VisibilityConfiguration Configuration => VisibilityPlugin.Instance.Configuration;
	private static TerritoryConfig CurrentConfig => VisibilityPlugin.Instance.Configuration.CurrentConfig;

	public HrothgarHandler(
		ContainerManager containerManager,
		VoidListManager voidListManager,
		ObjectVisibilityManager visibilityManager)
	{
		this.containerManager = containerManager;
		this.voidListManager = voidListManager;
		this.visibilityManager = visibilityManager;
	}

	/// <summary>
	/// Process a hrothgar entity and determine its visibility
	/// </summary>
	public unsafe void ProcessHrothgar(Character* characterPtr, Character* localPlayer, bool isBound)
	{

		if (characterPtr->GameObject.EntityId == 0xE0000000 ||
			this.visibilityManager.ShowGameObject(characterPtr))
			return;

		// Add to containers
		this.UpdateContainers(characterPtr, localPlayer);

		// Check territory whitelist
		if (isBound && !Configuration.TerritoryTypeWhitelist.Contains(
				Service.ClientState.TerritoryType))
			return;

		// Check void list
		if (this.voidListManager.CheckAndProcessVoidList(characterPtr))
		{
			this.visibilityManager.HideGameObject(characterPtr);
			return;
		}

		// Check whitelist
		if (this.voidListManager.CheckAndProcessWhitelist(characterPtr))
			return;

		// Check visibility conditions
		if (this.ShouldShowHrothgar(characterPtr))
		{
			this.visibilityManager.MarkObjectToShow(characterPtr->GameObject.EntityId);
			return;
		}

		this.visibilityManager.HideGameObject(characterPtr);
	}

	/// <summary>
	/// Update container memberships for the hrothgar
	/// </summary>
	private unsafe void UpdateContainers(Character* characterPtr, Character* localPlayer)
	{
		// All players container
		this.containerManager.AddToContainer(UnitType.Hrothgars, ContainerType.All, characterPtr->GameObject.EntityId);

		// Friend container
		if (characterPtr->IsFriend)
		{
			this.containerManager.AddToContainer(UnitType.Hrothgars, ContainerType.Friend,
				characterPtr->GameObject.EntityId);
		}
		else
		{
			this.containerManager.RemoveFromContainer(UnitType.Hrothgars, ContainerType.Friend,
				characterPtr->GameObject.EntityId);
		}

		// Party container
		bool isObjectIdInParty = FrameworkHandler.IsObjectIdInParty(characterPtr->GameObject.EntityId);
		if (isObjectIdInParty)
		{
			this.containerManager.AddToContainer(UnitType.Hrothgars, ContainerType.Party,
				characterPtr->GameObject.EntityId);
		}
		else
		{
			this.containerManager.RemoveFromContainer(UnitType.Hrothgars, ContainerType.Party,
				characterPtr->GameObject.EntityId);
		}

		// Company container
		if (localPlayer->FreeCompanyTag[0] != 0
			&& localPlayer->CurrentWorld == localPlayer->HomeWorld
			&& characterPtr->FreeCompanyTag.SequenceEqual(localPlayer->FreeCompanyTag))
		{
			this.containerManager.AddToContainer(UnitType.Hrothgars, ContainerType.Company,
				characterPtr->GameObject.EntityId);
		}
		else
		{
			this.containerManager.RemoveFromContainer(UnitType.Hrothgars, ContainerType.Company,
				characterPtr->GameObject.EntityId);
		}
	}

	/// <summary>
	/// Determine Hrothgars should be shown based on configuration settings
	/// </summary>
	private unsafe bool ShouldShowHrothgar(Character* characterPtr)
	{
		// Check if plugin is disabled or hrothgar hiding is disabled
		if (!Configuration.Enabled ||
			!CurrentConfig.HideHrothgar)
			return true;

		// Check if hrothgar is dead and show dead hrothgars is enabled
		if (CurrentConfig.ShowDeadHrothgar &&
			characterPtr->GameObject.IsDead())
			return true;

		// Check if hrothgar is a friend and show friends is enabled
		if (CurrentConfig.ShowFriendHrothgar &&
			this.containerManager.IsInContainer(UnitType.Hrothgars, ContainerType.Friend,
				characterPtr->GameObject.EntityId))
			return true;

		// Check if hrothgar is in the same company and show company members is enabled
		if (CurrentConfig.ShowCompanyHrothgar &&
			this.containerManager.IsInContainer(UnitType.Hrothgars, ContainerType.Company,
				characterPtr->GameObject.EntityId))
			return true;

		// Check if hrothgar is in the party and show party members is enabled
		if (CurrentConfig.ShowPartyHrothgar &&
			this.containerManager.IsInContainer(UnitType.Hrothgars, ContainerType.Party,
				characterPtr->GameObject.EntityId))
			return true;

		// Check if hrothgar is the target of the target
		if (FrameworkHandler.CheckTargetOfTarget(characterPtr))
			return true;

		// Check if local hrothgar is in combat and hide hrothgars in combat is enabled
		return CurrentConfig.HideHrothgarInCombat &&
			   !Service.Condition[ConditionFlag.InCombat];
	}
}
